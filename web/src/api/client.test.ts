import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { apiClient, toApiError } from '@/api/client'
import { endpoints } from '@/api/endpoints'
import { useSessionStore } from '@/lib/auth'
import '@/lib/auth/sessionClient' // registers the refresh handler the interceptor calls
import { server } from '@/mocks/server'
import { resetSession } from '@/test/session'
import { ACCESS_TOKEN, authenticated, problem, refresh, url } from '@/test/authHandlers'
import type { ApiError } from '@/types'

function signIn(token = ACCESS_TOKEN) {
  useSessionStore.getState().applyAuthResult(authenticated({ accessToken: token }))
}

beforeEach(resetSession)

/** A protected resource that answers 401 until it sees the expected bearer token. */
function protectedResource(path: string, acceptedToken: string) {
  const calls: (string | null)[] = []
  server.use(
    http.get(url(path), ({ request }) => {
      const auth = request.headers.get('authorization')
      calls.push(auth)
      return auth === `Bearer ${acceptedToken}` ? HttpResponse.json({ ok: true }) : problem(401, 'unauthorized')
    }),
  )
  return calls
}

describe('request headers and credentials', () => {
  it('sends the cookie (withCredentials) on every request', async () => {
    let credentials: string | undefined
    server.use(http.get(url('/ping'), ({ request }) => { credentials = request.credentials; return HttpResponse.json({}) }))

    await apiClient.get('/ping')

    expect(apiClient.defaults.withCredentials).toBe(true)
    expect(credentials).toBe('include')
  })

  it('adds the bearer token from the session, and none when signed out', async () => {
    const seen: (string | null)[] = []
    server.use(http.get(url('/ping'), ({ request }) => { seen.push(request.headers.get('authorization')); return HttpResponse.json({}) }))

    await apiClient.get('/ping')
    signIn('tok-1')
    await apiClient.get('/ping')

    expect(seen).toEqual([null, 'Bearer tok-1'])
  })

  it('marks state-changing calls with X-Requested-With, not reads, and gives every call a fresh correlation id', async () => {
    const seen: { method: string; requestedWith: string | null; correlation: string | null }[] = []
    const record = ({ request }: { request: Request }) => {
      seen.push({ method: request.method, requestedWith: request.headers.get('x-requested-with'), correlation: request.headers.get('x-correlation-id') })
      return HttpResponse.json({})
    }
    server.use(http.get(url('/ping'), record), http.post(url('/ping'), record))

    await apiClient.get('/ping')
    await apiClient.post('/ping', {})
    await apiClient.post('/ping', {})

    expect(seen.map((s) => s.requestedWith)).toEqual([null, 'fynovio', 'fynovio'])
    expect(new Set(seen.map((s) => s.correlation)).size).toBe(3)
    expect(seen.every((s) => s.correlation)).toBe(true)
  })
})

describe('401 handling', () => {
  it('refreshes once and replays the original request with the new token', async () => {
    signIn('old-token')
    let refreshCalls = 0
    server.use(refresh(() => { refreshCalls++; return HttpResponse.json(authenticated({ accessToken: 'new-token' })) }))
    const calls = protectedResource('/deals', 'new-token')

    const response = await apiClient.get('/deals')

    expect(response.data).toEqual({ ok: true })
    expect(refreshCalls).toBe(1)
    expect(calls).toEqual(['Bearer old-token', 'Bearer new-token'])
    expect(useSessionStore.getState().status).toBe('authenticated')
  })

  it('shares ONE refresh between concurrent 401s', async () => {
    signIn('old-token')
    let refreshCalls = 0
    server.use(refresh(async () => { refreshCalls++; await new Promise((r) => setTimeout(r, 30)); return HttpResponse.json(authenticated({ accessToken: 'new-token' })) }))
    protectedResource('/a', 'new-token')
    protectedResource('/b', 'new-token')
    protectedResource('/c', 'new-token')

    const results = await Promise.all([apiClient.get('/a'), apiClient.get('/b'), apiClient.get('/c')])

    expect(results.every((r) => r.status === 200)).toBe(true)
    expect(refreshCalls).toBe(1)
  })

  it('a failed refresh ends the session as expired and rejects the original request', async () => {
    signIn('old-token')
    server.use(refresh(() => problem(401, 'session_invalid')))
    protectedResource('/deals', 'never')

    await expect(apiClient.get('/deals')).rejects.toMatchObject({ status: 401 })

    expect(useSessionStore.getState().status).toBe('expired')
    expect(useSessionStore.getState().accessToken).toBeNull()
  })

  it('does not replay twice: a second 401 after the retry is final', async () => {
    signIn('old-token')
    let refreshCalls = 0
    let resourceCalls = 0
    server.use(
      refresh(() => { refreshCalls++; return HttpResponse.json(authenticated({ accessToken: 'new-token' })) }),
      // Bounded on purpose: if the retry guard broke, a runaway loop must end in a clean assertion failure, not a hung run.
      http.get(url('/deals'), () => { resourceCalls++; return resourceCalls > 4 ? problem(500, 'runaway_retry_loop') : problem(401, 'unauthorized') }),
    )

    await expect(apiClient.get('/deals')).rejects.toMatchObject({ status: 401 })

    expect(resourceCalls).toBe(2) // the original + exactly one replay
    expect(refreshCalls).toBe(1)
  })

  it('never tries to refresh for an /auth/* call (a 401 from login is an answer, not an expiry)', async () => {
    let refreshCalls = 0
    server.use(
      refresh(() => { refreshCalls++; return HttpResponse.json(authenticated()) }),
      http.post(url(endpoints.auth.login), () => problem(401, 'invalid_credentials')),
    )

    const error = (await apiClient.post(endpoints.auth.login, {}).catch((e) => e)) as ApiError

    expect(error).toMatchObject({ status: 401, code: 'invalid_credentials' })
    expect(refreshCalls).toBe(0)
  })

  it('does not refresh when the caller opts out with skipAuthRefresh', async () => {
    let refreshCalls = 0
    server.use(refresh(() => { refreshCalls++; return HttpResponse.json(authenticated()) }), http.get(url('/x'), () => problem(401, 'unauthorized')))

    await expect(apiClient.get('/x', { skipAuthRefresh: true })).rejects.toMatchObject({ status: 401 })

    expect(refreshCalls).toBe(0)
  })
})

describe('403 and 429', () => {
  it('a 403 propagates as ApiError and leaves the session untouched (403 is not 401)', async () => {
    signIn()
    let refreshCalls = 0
    server.use(refresh(() => { refreshCalls++; return HttpResponse.json(authenticated()) }), http.get(url('/deals'), () => problem(403, 'forbidden')))

    await expect(apiClient.get('/deals')).rejects.toMatchObject({ status: 403 })

    expect(refreshCalls).toBe(0)
    expect(useSessionStore.getState().status).toBe('authenticated')
    expect(useSessionStore.getState().accessToken).toBe(ACCESS_TOKEN)
  })

  it('surfaces Retry-After on a 429', async () => {
    server.use(http.get(url('/x'), () => problem(429, 'rate_limited', {}, { 'Retry-After': '30' })))

    await expect(apiClient.get('/x')).rejects.toMatchObject({ status: 429, code: 'rate_limited', retryAfterSeconds: 30 })
  })
})

describe('error normalisation', () => {
  it('maps RFC 7807: type → code, detail → message, errors → fields (400)', async () => {
    server.use(http.post(url('/x'), () => problem(400, 'validation_error', { detail: 'Invalid input.', errors: { email: ['Required'] } })))

    const error = (await apiClient.post('/x', {}).catch((e) => e)) as ApiError

    expect(error).toEqual({ message: 'Invalid input.', status: 400, code: 'validation_error', fields: { email: ['Required'] } })
  })

  it('keeps the Laravel-style 422 body working', async () => {
    server.use(http.post(url('/x'), () => HttpResponse.json({ message: 'Nope', code: 'invalid', errors: { name: ['Too short'] } }, { status: 422 })))

    const error = (await apiClient.post('/x', {}).catch((e) => e)) as ApiError

    expect(error).toMatchObject({ status: 422, message: 'Nope', code: 'invalid', fields: { name: ['Too short'] } })
  })

  it('does not expose fields for other statuses', async () => {
    server.use(http.get(url('/x'), () => HttpResponse.json({ errors: { a: ['b'] } }, { status: 500 })))

    expect(((await apiClient.get('/x').catch((e) => e)) as ApiError).fields).toBeUndefined()
  })

  it('reports a network failure as status 0', async () => {
    server.use(http.get(url('/x'), () => HttpResponse.error()))

    expect(((await apiClient.get('/x').catch((e) => e)) as ApiError).status).toBe(0)
  })

  it('toApiError falls back to the axios message when the body has none', () => {
    expect(toApiError({ message: 'boom', response: undefined } as never)).toMatchObject({ message: 'boom', status: 0 })
  })
})

describe('bearer-authenticated /auth endpoints', () => {
  it('an expired access token on change-password is refreshed and the request replayed once', async () => {
    signIn('stale')
    const calls: (string | null)[] = []
    server.use(
      http.post(url(endpoints.auth.changePassword), ({ request }) => {
        calls.push(request.headers.get('authorization'))
        return request.headers.get('authorization') === 'Bearer fresh' ? new HttpResponse(null, { status: 204 }) : problem(401, 'unauthorized')
      }),
      refresh(() => HttpResponse.json(authenticated({ accessToken: 'fresh' }))),
    )

    const response = await apiClient.post(endpoints.auth.changePassword, { currentPassword: 'a', newPassword: 'b' })

    expect(response.status).toBe(204)
    expect(calls).toEqual(['Bearer stale', 'Bearer fresh'])
  })

  it('but a 401 from a cookie/link-token /auth endpoint never triggers a refresh', async () => {
    let refreshes = 0
    server.use(
      refresh(() => { refreshes++; return HttpResponse.json(authenticated()) }),
      http.post(url(endpoints.auth.resetPassword), () => problem(401, 'invalid_credentials')),
    )

    await expect(apiClient.post(endpoints.auth.resetPassword, {})).rejects.toMatchObject({ status: 401 })

    expect(refreshes).toBe(0)
  })
})

describe('toApiError: password policy violations', () => {
  it('exposes the machine codes of a 400 password_policy_violation', async () => {
    server.use(http.post(url(endpoints.auth.resetPassword), () => problem(400, 'password_policy_violation', { violations: ['too_short'] })))

    await expect(apiClient.post(endpoints.auth.resetPassword, {})).rejects.toMatchObject({ status: 400, code: 'password_policy_violation', violations: ['too_short'] })
  })

  it('ignores a malformed violations value', async () => {
    server.use(http.post(url(endpoints.auth.resetPassword), () => problem(400, 'password_policy_violation', { violations: 'nope' })))

    const error = (await apiClient.post(endpoints.auth.resetPassword, {}).catch((e) => e)) as ApiError
    expect(error.violations).toBeUndefined()
  })
})
