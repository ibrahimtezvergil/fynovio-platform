import { expect, test, type APIRequestContext } from '@playwright/test'
import { login, newApi } from './support/api.ts'
import { SEED_PASSWORD, SINGLE } from './support/env.ts'

const CSRF = { 'X-Requested-With': 'fynovio' }

/** A write the seeded single-tenant member holds no grant for (reads return an empty scope instead of refusing). */
const createOpportunity = (api: APIRequestContext, token?: string) =>
  api.post('/api/opportunities', {
    data: { partyId: 1, currency: 'EUR', estimatedAmount: 100 },
    headers: { 'Idempotency-Key': crypto.randomUUID(), ...(token ? { Authorization: `Bearer ${token}` } : {}) },
  })

/** Base64url-decode / re-encode the payload of a JWT (no signing key is needed to *edit* one — only to make it valid). */
function withClaim(token: string, claim: string, value: unknown): string {
  const [header, payload, signature] = token.split('.')
  const claims = JSON.parse(Buffer.from(payload, 'base64url').toString('utf8'))
  claims[claim] = value
  return [header, Buffer.from(JSON.stringify(claims)).toString('base64url'), signature].join('.')
}

test.describe('tenant and token manipulation', () => {
  test('a member cannot select a tenant they do not belong to', async () => {
    const api = await newApi()
    expect((await login(api, SINGLE, SEED_PASSWORD)).body.status).toBe('authenticated')

    const foreign = await api.post('/api/auth/tenants/select', { data: { tenantId: 2 }, headers: CSRF })
    const unknown = await api.post('/api/auth/tenants/select', { data: { tenantId: 987654 }, headers: CSRF })

    expect(foreign.status()).toBe(403)
    expect(unknown.status()).toBe(403)
    // Same answer for a real tenant and one that does not exist: nothing to enumerate.
    expect((await foreign.json()).type).toBe('tenant_not_permitted')
    expect((await unknown.json()).type).toBe('tenant_not_permitted')
    await api.dispose()
  })

  test('editing the tenant claim of an access token gets no access (the signature no longer matches)', async () => {
    const api = await newApi()
    const { body } = await login(api, SINGLE, SEED_PASSWORD)
    const token = body.accessToken as string

    expect((await createOpportunity(api, token)).status()).toBe(403) // authenticated, but the seeded member holds no CRM grant: 403, not 401

    for (const tampered of [withClaim(token, 'tid', '2'), withClaim(token, 'sub', 'someone-else'), `${token.split('.').slice(0, 2).join('.')}.`]) {
      expect((await createOpportunity(api, tampered)).status()).toBe(401)
    }
    await api.dispose()
  })

  test('401 and 403 stay distinct: no token → 401; a token without the grant → 403', async () => {
    const api = await newApi()
    expect((await api.get('/api/opportunities')).status()).toBe(401)
    expect((await api.get('/api/auth/me')).status()).toBe(401)
    expect((await createOpportunity(api)).status()).toBe(401)

    const { body } = await login(api, SINGLE, SEED_PASSWORD)
    expect((await createOpportunity(api, body.accessToken)).status()).toBe(403)
    await api.dispose()
  })

  test('state-changing auth calls without the CSRF header, or from a foreign origin, are refused', async () => {
    const api = await newApi()
    const noHeader = await api.post('/api/auth/login', { data: { email: SINGLE, password: SEED_PASSWORD } })
    const foreign = await api.post('/api/auth/login', {
      data: { email: SINGLE, password: SEED_PASSWORD },
      headers: { ...CSRF, Origin: 'https://evil.example' },
    })
    expect(noHeader.status()).toBe(403)
    expect(foreign.status()).toBe(403)
    await api.dispose()
  })
})
