import { expect, request, type APIRequestContext } from '@playwright/test'
import { ADMIN, APP_URL, SEED_PASSWORD } from './env.ts'

/** Every call goes through the Vite proxy (`/api`), exactly like the browser — so cookie paths and the proxy are exercised too. */
const CSRF = { 'X-Requested-With': 'fynovio' }

export const newApi = () => request.newContext({ baseURL: APP_URL })

export const uniqueEmail = (prefix: string) => `${prefix}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 7)}@e2e.example`

export async function login(api: APIRequestContext, email: string, password: string) {
  const response = await api.post('/api/auth/login', { data: { email, password }, headers: CSRF })
  return { response, body: response.ok() ? await response.json() : null }
}

/** A tenant administrator's access token for `tenantId` (the seeded admin belongs to tenants 1 and 2). */
export async function adminToken(tenantId: number): Promise<string> {
  const api = await newApi()
  const { body } = await login(api, ADMIN, SEED_PASSWORD)
  expect(body?.status).toBe('tenant_selection_required')
  const select = await api.post('/api/auth/tenants/select', { data: { tenantId }, headers: CSRF })
  expect(select.status()).toBe(200)
  const token = (await select.json()).accessToken as string
  await api.dispose()
  return token
}

export async function invite(token: string, tenantId: number, email: string, displayName?: string) {
  const api = await newApi()
  const response = await api.post(`/api/tenants/${tenantId}/invitations`, {
    data: { email, displayName, locale: 'tr' },
    headers: { ...CSRF, Authorization: `Bearer ${token}` },
  })
  expect(response.status()).toBe(202)
  await api.dispose()
}

export interface MailboxEntry {
  to: string
  templateId: string
  link: string
  token: string
}

/** The newest message of `template` for `to`, from the Development mailbox. */
export async function mailFor(to: string, template: 'invite' | 'password_reset'): Promise<MailboxEntry> {
  const api = await newApi()
  const response = await api.get(`/api/dev/mailbox?to=${encodeURIComponent(to)}`)
  expect(response.ok()).toBe(true)
  const entries = (await response.json()) as MailboxEntry[]
  await api.dispose()
  const entry = entries.find((e) => e.templateId === template)
  expect(entry, `no ${template} mail for ${to}`).toBeDefined()
  return entry!
}

/** A brand-new member of `tenantId` with a known password, created the way real users are: invite, then accept. */
export async function createMember(tenantId: number, password: string, adminTokenValue?: string) {
  const token = adminTokenValue ?? (await adminToken(tenantId))
  const email = uniqueEmail('member')
  await invite(token, tenantId, email, 'E2E Member')
  const mail = await mailFor(email, 'invite')
  const api = await newApi()
  const accept = await api.post('/api/auth/invitations/accept', { data: { token: mail.token, password, displayName: 'E2E Member' }, headers: CSRF })
  expect(accept.status()).toBe(200)
  await api.dispose()
  return email
}
