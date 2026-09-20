import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { queryClient } from '@/api/queryClient'
import { endpoints } from '@/api/endpoints'
import { server } from '@/mocks/server'
import {
  ACCESS_TOKEN, authenticated, login as loginHandler, logout as logoutHandler, noMembership, problem, refresh,
  selectionRequired, selectTenantHandler, url,
} from '@/test/authHandlers'
import { resetSession } from '@/test/session'
import { useSessionStore } from './session'
import { acceptInvitation, bootstrapSession, LEGACY_STORAGE_KEY, login, logout, refreshSession, resetPassword, selectTenant } from './sessionClient'

beforeEach(resetSession)
afterEach(() => {
  vi.restoreAllMocks()
  Reflect.deleteProperty(navigator, 'locks')
})

const status = () => useSessionStore.getState().status

describe('bootstrapSession', () => {
  it('restores an authenticated session from the refresh cookie', async () => {
    server.use(refresh(() => HttpResponse.json(authenticated())))
    await bootstrapSession()
    expect(status()).toBe('authenticated')
    expect(useSessionStore.getState().accessToken).toBe(ACCESS_TOKEN)
  })

  it('a multi-tenant session lands in tenant_unresolved without a token', async () => {
    server.use(refresh(() => HttpResponse.json(selectionRequired())))
    await bootstrapSession()
    expect(status()).toBe('tenant_unresolved')
    expect(useSessionStore.getState().accessToken).toBeNull()
    expect(useSessionStore.getState().memberships).toHaveLength(2)
  })

  it('an account without a membership lands in tenant_unresolved + noMembership', async () => {
    server.use(refresh(() => HttpResponse.json(noMembership())))
    await bootstrapSession()
    expect(status()).toBe('tenant_unresolved')
    expect(useSessionStore.getState().noMembership).toBe(true)
  })

  it('no cookie / invalid session (401) means unauthenticated — not expired, nothing ever existed', async () => {
    server.use(refresh(() => problem(401, 'session_invalid')))
    await bootstrapSession()
    expect(status()).toBe('unauthenticated')
  })

  it('an unreachable API at start-up is treated as signed out rather than hanging on "unknown"', async () => {
    server.use(refresh(() => HttpResponse.error()))
    await bootstrapSession()
    expect(status()).toBe('unauthenticated')
  })

  it('a malformed payload never produces a session', async () => {
    server.use(refresh(() => HttpResponse.json({ status: 'authenticated', nonsense: true })))
    await bootstrapSession()
    expect(status()).toBe('unauthenticated')
    expect(useSessionStore.getState().accessToken).toBeNull()
  })

  it('runs once per page load however often it is called', async () => {
    let calls = 0
    server.use(refresh(() => { calls++; return HttpResponse.json(authenticated()) }))
    await Promise.all([bootstrapSession(), bootstrapSession(), bootstrapSession()])
    await bootstrapSession()
    expect(calls).toBe(1)
  })

  it('purges the legacy persisted store (it held a mock token) before anything else', async () => {
    localStorage.setItem(LEGACY_STORAGE_KEY, JSON.stringify({ state: { token: 'mock-token' } }))
    server.use(refresh(() => problem(401, 'session_invalid')))
    await bootstrapSession()
    expect(localStorage.getItem(LEGACY_STORAGE_KEY)).toBeNull()
  })
})

describe('refreshSession', () => {
  it('is single-flight: N concurrent callers share exactly ONE network call', async () => {
    let calls = 0
    server.use(refresh(async () => { calls++; await new Promise((r) => setTimeout(r, 30)); return HttpResponse.json(authenticated()) }))

    const results = await Promise.all(Array.from({ length: 6 }, () => refreshSession()))

    expect(calls).toBe(1)
    expect(results.every(Boolean)).toBe(true)
  })

  it('starts a new request once the previous one has settled', async () => {
    let calls = 0
    server.use(refresh(() => { calls++; return HttpResponse.json(authenticated()) }))
    await refreshSession()
    await refreshSession()
    expect(calls).toBe(2)
  })

  it('coordinates across tabs with the Web Locks API when it exists', async () => {
    const request = vi.fn(async (_name: string, callback: () => Promise<boolean>) => callback())
    Object.defineProperty(navigator, 'locks', { value: { request }, configurable: true })
    server.use(refresh(() => HttpResponse.json(authenticated())))

    await refreshSession()

    expect(request).toHaveBeenCalledTimes(1)
    expect(request.mock.calls[0][0]).toBe('fynovio-auth-refresh')
  })

  it('retries ONCE after a 409 refresh_conflict, then succeeds', async () => {
    let calls = 0
    server.use(refresh(() => (++calls === 1 ? problem(409, 'refresh_conflict') : HttpResponse.json(authenticated()))))

    expect(await refreshSession()).toBe(true)
    expect(calls).toBe(2)
  })

  it('gives up after a second 409 (no retry storm) and keeps a signed-in state', async () => {
    useSessionStore.getState().applyAuthResult(authenticated())
    let calls = 0
    server.use(refresh(() => { calls++; return problem(409, 'refresh_conflict') }))

    expect(await refreshSession()).toBe(false)
    expect(calls).toBe(2)
    expect(status()).toBe('authenticated') // a conflict is not an expiry
  })

  it('a 401 mid-session means expired', async () => {
    useSessionStore.getState().applyAuthResult(authenticated())
    server.use(refresh(() => problem(401, 'session_invalid')))

    expect(await refreshSession()).toBe(false)
    expect(status()).toBe('expired')
  })

  it('a transient failure mid-session (5xx) leaves the session as it is', async () => {
    useSessionStore.getState().applyAuthResult(authenticated())
    server.use(refresh(() => new HttpResponse(null, { status: 503 })))

    expect(await refreshSession()).toBe(false)
    expect(status()).toBe('authenticated')
  })

  it('a refresh that ends in tenant selection is not a usable token', async () => {
    server.use(refresh(() => HttpResponse.json(selectionRequired())))
    expect(await refreshSession()).toBe(false)
    expect(status()).toBe('tenant_unresolved')
  })
})

describe('login / logout / selectTenant', () => {
  it('login updates the session and clears the previous user’s cache', async () => {
    const clear = vi.spyOn(queryClient, 'clear')
    server.use(loginHandler(authenticated()))

    expect(await login({ email: 'ada@example.com', password: 'pw' })).toBe('authenticated')

    expect(useSessionStore.getState().user?.name).toBe('Ada Lovelace')
    expect(clear).toHaveBeenCalled()
  })

  it('a rejected login leaves the session untouched', async () => {
    server.use(http.post(url(endpoints.auth.login), () => problem(401, 'invalid_credentials')))

    await expect(login({ email: 'a@b.co', password: 'x' })).rejects.toMatchObject({ status: 401, code: 'invalid_credentials' })
    expect(status()).toBe('unknown')
    expect(useSessionStore.getState().accessToken).toBeNull()
  })

  it('logout calls the endpoint, clears state and caches', async () => {
    useSessionStore.getState().applyAuthResult(authenticated())
    const clear = vi.spyOn(queryClient, 'clear')
    let calls = 0
    server.use(logoutHandler(() => { calls++; return new HttpResponse(null, { status: 204 }) }))

    await logout()

    expect(calls).toBe(1)
    expect(status()).toBe('unauthenticated')
    expect(useSessionStore.getState().accessToken).toBeNull()
    expect(useSessionStore.getState().explicitSignOut).toBe(true)
    expect(clear).toHaveBeenCalled()
  })

  it('logout still ends the local session when the request fails', async () => {
    useSessionStore.getState().applyAuthResult(authenticated())
    server.use(logoutHandler(() => HttpResponse.error() as unknown as Response))

    await expect(logout()).resolves.toBeUndefined()

    expect(status()).toBe('unauthenticated')
    expect(useSessionStore.getState().accessToken).toBeNull()
  })

  it('selectTenant stores the tenant-scoped token and clears the previous tenant’s cache', async () => {
    useSessionStore.getState().applyAuthResult(selectionRequired())
    const clear = vi.spyOn(queryClient, 'clear')
    server.use(selectTenantHandler(() => HttpResponse.json({ accessToken: 'tenant-2-token', expiresIn: 600, activeTenant: { tenantId: 2 } })))

    await selectTenant(2)

    expect(status()).toBe('authenticated')
    expect(useSessionStore.getState().accessToken).toBe('tenant-2-token')
    expect(useSessionStore.getState().activeTenantId).toBe(2)
    expect(clear).toHaveBeenCalled()
  })

  it('selectTenant sends only the requested tenant id, never a tenant claim', async () => {
    useSessionStore.getState().applyAuthResult(selectionRequired())
    let body: unknown
    server.use(http.post(url(endpoints.auth.selectTenant), async ({ request }) => {
      body = await request.json()
      return HttpResponse.json({ accessToken: 't', expiresIn: 600, activeTenant: { tenantId: 2 } })
    }))

    await selectTenant(2)

    expect(body).toEqual({ tenantId: 2 })
  })

  it('a 403 tenant_not_permitted rejects and changes nothing', async () => {
    useSessionStore.getState().applyAuthResult(selectionRequired())
    server.use(selectTenantHandler(() => problem(403, 'tenant_not_permitted')))

    await expect(selectTenant(99)).rejects.toMatchObject({ status: 403, code: 'tenant_not_permitted' })

    expect(status()).toBe('tenant_unresolved')
    expect(useSessionStore.getState().accessToken).toBeNull()
  })

  it('a 401 while selecting a tenant means the session is gone', async () => {
    useSessionStore.getState().applyAuthResult(selectionRequired())
    server.use(selectTenantHandler(() => problem(401, 'session_invalid')))

    await expect(selectTenant(1)).rejects.toMatchObject({ status: 401 })

    expect(status()).toBe('expired')
  })
})

describe('nothing sensitive reaches browser storage', () => {
  it('a full login → refresh → select-tenant → logout flow writes no token to localStorage or sessionStorage', async () => {
    const writes: [string, string][] = []
    const original = Storage.prototype.setItem
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(function (this: Storage, key: string, value: string) {
      writes.push([key, value])
      return original.call(this, key, value)
    })
    server.use(
      loginHandler(selectionRequired()),
      refresh(() => HttpResponse.json(authenticated({ accessToken: 'refreshed-token' }))),
      selectTenantHandler(() => HttpResponse.json({ accessToken: 'tenant-token', expiresIn: 600, activeTenant: { tenantId: 2 } })),
      logoutHandler(),
    )

    await login({ email: 'ada@example.com', password: 'pw-that-must-not-leak' })
    await selectTenant(2)
    await refreshSession()
    await logout()

    const leaked = writes.filter(([key, value]) => /token|auth|session/i.test(key) || /token|pw-that-must-not-leak|ada@example/i.test(value))
    expect(leaked).toEqual([])
  })
})

describe('acceptInvitation', () => {
  it('applies the new session like a sign-in and drops every cached response of the previous one', async () => {
    const clear = vi.spyOn(queryClient, 'clear')
    let body: unknown
    server.use(http.post(url(endpoints.auth.acceptInvitation), async ({ request }) => { body = await request.json(); return HttpResponse.json(authenticated()) }))

    await expect(acceptInvitation({ token: 'id.secret', password: 'a-long-new-passphrase', displayName: 'Ada' })).resolves.toBe('authenticated')

    expect(body).toEqual({ token: 'id.secret', password: 'a-long-new-passphrase', displayName: 'Ada' })
    expect(useSessionStore.getState().accessToken).toBe(ACCESS_TOKEN)
    expect(clear).toHaveBeenCalled()
  })

  it('lands in tenant selection when the account belongs to several tenants', async () => {
    server.use(http.post(url(endpoints.auth.acceptInvitation), () => HttpResponse.json(selectionRequired())))
    await expect(acceptInvitation({ token: 't', password: 'p' })).resolves.toBe('tenant_unresolved')
  })

  it('a refused invitation leaves the existing session alone', async () => {
    useSessionStore.getState().applyAuthResult(authenticated())
    server.use(http.post(url(endpoints.auth.acceptInvitation), () => problem(400, 'invalid_or_expired_token')))

    await expect(acceptInvitation({ token: 't', password: 'p' })).rejects.toMatchObject({ status: 400, code: 'invalid_or_expired_token' })

    expect(status()).toBe('authenticated')
  })

  it('a 401 from the accept call is an answer, not a reason to refresh (it is an /auth endpoint)', async () => {
    let refreshes = 0
    server.use(
      refresh(() => { refreshes++; return HttpResponse.json(authenticated()) }),
      http.post(url(endpoints.auth.acceptInvitation), () => problem(401, 'invalid_credentials')),
    )

    await expect(acceptInvitation({ token: 't', password: 'wrong' })).rejects.toMatchObject({ status: 401 })

    expect(refreshes).toBe(0)
  })
})

describe('resetPassword', () => {
  it('ends the local session and clears the caches — the server revoked every session of the account', async () => {
    useSessionStore.getState().applyAuthResult(authenticated())
    const clear = vi.spyOn(queryClient, 'clear')
    server.use(http.post(url(endpoints.auth.resetPassword), () => new HttpResponse(null, { status: 204 })))

    await resetPassword({ token: 'id.secret', newPassword: 'a-long-new-passphrase' })

    expect(status()).toBe('unauthenticated')
    expect(useSessionStore.getState().accessToken).toBeNull()
    expect(clear).toHaveBeenCalled()
  })

  it('a refused reset keeps the session as it was', async () => {
    useSessionStore.getState().applyAuthResult(authenticated())
    server.use(http.post(url(endpoints.auth.resetPassword), () => problem(400, 'invalid_or_expired_token')))

    await expect(resetPassword({ token: 't', newPassword: 'p' })).rejects.toMatchObject({ code: 'invalid_or_expired_token' })

    expect(status()).toBe('authenticated')
  })
})
