import { http, HttpResponse } from 'msw'
import { endpoints } from '@/api/endpoints'
import type { AuthResult } from '@/lib/auth/types'
import { API_BASE } from '@/mocks/apiBase'

/** Per-test stand-ins for the real `/auth/*` endpoints (the browser worker deliberately mocks none of them). */
export const url = (path: string) => `${API_BASE}${path}`

export const ACCESS_TOKEN = 'access-token-1'

export const account = { id: 7, email: 'ada@example.com', displayName: 'Ada Lovelace', locale: 'tr' }

export const authenticated = (overrides: Partial<AuthResult> = {}): AuthResult => ({
  status: 'authenticated',
  accessToken: ACCESS_TOKEN,
  expiresIn: 600,
  account,
  activeTenant: { tenantId: 1 },
  memberships: [{ tenantId: 1, displayName: 'Acme Türkiye' }],
  ...overrides,
})

export const selectionRequired = (memberships = [{ tenantId: 1, displayName: 'Acme Türkiye' }, { tenantId: 2, displayName: 'Northwind' }]): AuthResult => ({
  status: 'tenant_selection_required',
  account,
  activeTenant: null,
  memberships,
})

export const noMembership = (): AuthResult => ({ status: 'no_membership', account, activeTenant: null, memberships: [] })

export const problem = (status: number, type: string, extra: Record<string, unknown> = {}, headers: Record<string, string> = {}) =>
  HttpResponse.json({ type, title: type, status, ...extra }, { status, headers })

export const login = (body: unknown) => http.post(url(endpoints.auth.login), () => HttpResponse.json(body as object))
export const refresh = (respond: () => Response | Promise<Response>) => http.post(url(endpoints.auth.refresh), respond)
export const logout = (respond: () => Response = () => new HttpResponse(null, { status: 204 })) =>
  http.post(url(endpoints.auth.logout), respond)
export const selectTenantHandler = (respond: () => Response | Promise<Response>) =>
  http.post(url(endpoints.auth.selectTenant), respond)
