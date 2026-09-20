import { apiClient, setRefreshHandler } from '@/api/client'
import { endpoints } from '@/api/endpoints'
import { queryClient } from '@/api/queryClient'
import type { ApiError } from '@/types'
import { useSessionStore } from './session'
import { authResultSchema, tenantSelectionSchema, type AuthResult, type SessionStatus } from './types'

/** Auth calls answer for themselves: a 401 from them must never trigger the refresh interceptor. */
const AUTH_REQUEST = { skipAuthRefresh: true } as const

const LOCK_NAME = 'fynovio-auth-refresh'
const CONFLICT_RETRY_DELAY_MS = 300
/** The mock-era store; it held a token in localStorage. Removed at boot. */
export const LEGACY_STORAGE_KEY = 'fynovio-auth'

const sleep = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms))

function purgeLegacyStorage() {
  try {
    localStorage.removeItem(LEGACY_STORAGE_KEY)
  } catch {
    /* storage unavailable — nothing to purge */
  }
}

/** Server state changed under the user: drop everything cached for the previous session/tenant. */
function resetClientState() {
  queryClient.clear()
}

export async function login(values: { email: string; password: string }): Promise<SessionStatus> {
  const { data } = await apiClient.post<unknown>(endpoints.auth.login, values, AUTH_REQUEST)
  useSessionStore.getState().applyAuthResult(authResultSchema.parse(data))
  resetClientState()
  return useSessionStore.getState().status
}

/** Accepting an invitation ends with a brand-new session, exactly like a sign-in. */
export async function acceptInvitation(values: { token: string; password: string; displayName?: string }): Promise<SessionStatus> {
  const { data } = await apiClient.post<unknown>(endpoints.auth.acceptInvitation, values, AUTH_REQUEST)
  useSessionStore.getState().applyAuthResult(authResultSchema.parse(data))
  resetClientState()
  return useSessionStore.getState().status
}

/**
 * The server revokes EVERY session of the account when a password is reset (and clears the cookie), so the
 * browser must forget its own session too — otherwise it would keep a token the API already rejects.
 */
export async function resetPassword(values: { token: string; newPassword: string }): Promise<void> {
  await apiClient.post(endpoints.auth.resetPassword, values, AUTH_REQUEST)
  useSessionStore.getState().endSession('unauthenticated')
  resetClientState()
}

export async function logout(): Promise<void> {
  try {
    await apiClient.post(endpoints.auth.logout, undefined, AUTH_REQUEST)
  } catch {
    // Server unreachable or the session is already gone — the browser still ends its side.
  }
  useSessionStore.getState().endSession('unauthenticated', { explicit: true })
  resetClientState()
}

export async function selectTenant(tenantId: number): Promise<void> {
  try {
    const { data } = await apiClient.post<unknown>(endpoints.auth.selectTenant, { tenantId }, AUTH_REQUEST)
    useSessionStore.getState().applyTenantSelection(tenantSelectionSchema.parse(data))
    resetClientState() // a different tenant must never see the previous tenant's cached data
  } catch (error) {
    if ((error as ApiError).status === 401) {
      useSessionStore.getState().endSession('expired')
      resetClientState()
    }
    throw error
  }
}

/** true only when a NEW access token was obtained. */
function applyRefreshResult(result: AuthResult): boolean {
  useSessionStore.getState().applyAuthResult(result)
  return useSessionStore.getState().status === 'authenticated'
}

function handleRefreshFailure(status: number | undefined) {
  const session = useSessionStore.getState()
  if (status === 401) {
    const hadSession = session.status === 'authenticated' || session.status === 'tenant_unresolved'
    session.endSession(hadSession ? 'expired' : session.status === 'expired' ? 'expired' : 'unauthenticated')
    resetClientState()
  } else if (session.status === 'unknown') {
    // Boot with the API unreachable: we cannot prove a session, so treat it as signed out.
    session.endSession('unauthenticated')
  }
  // Any other failure mid-session (network, 5xx, 429) leaves the state as it is.
}

async function attemptRefresh(): Promise<boolean> {
  for (let attempt = 0; attempt < 2; attempt++) {
    try {
      const { data } = await apiClient.post<unknown>(endpoints.auth.refresh, undefined, AUTH_REQUEST)
      return applyRefreshResult(authResultSchema.parse(data))
    } catch (error) {
      const status = (error as ApiError).status
      if (status === 409 && attempt === 0) {
        // Another tab/request rotated the cookie a moment ago; the cookie jar already holds the new one.
        await sleep(CONFLICT_RETRY_DELAY_MS)
        continue
      }
      handleRefreshFailure(status)
      return false
    }
  }
  return false
}

async function withCrossTabLock<T>(task: () => Promise<T>): Promise<T> {
  const locks = typeof navigator === 'undefined' ? undefined : navigator.locks
  return locks?.request ? locks.request(LOCK_NAME, task) : task()
}

let refreshInFlight: Promise<boolean> | null = null

/**
 * Exchanges the refresh cookie for a new access token. Single-flight: every
 * concurrent caller (the boot sequence, several 401s at once) shares ONE
 * request, and the Web Locks API keeps other tabs from rotating the cookie at
 * the same time. Resolves true only when a new access token was obtained.
 */
export function refreshSession(): Promise<boolean> {
  refreshInFlight ??= withCrossTabLock(attemptRefresh).finally(() => {
    refreshInFlight = null
  })
  return refreshInFlight
}

let bootstrap: Promise<void> | null = null

/** Runs once per page load: purge the legacy store, then restore the session from the refresh cookie. */
export function bootstrapSession(): Promise<void> {
  bootstrap ??= (async () => {
    purgeLegacyStorage()
    await refreshSession()
    const session = useSessionStore.getState()
    if (session.status === 'unknown') session.endSession('unauthenticated')
  })()
  return bootstrap
}

/** Test seam: forget that the boot sequence already ran. */
export function resetBootstrapForTests() {
  bootstrap = null
  refreshInFlight = null
}

setRefreshHandler(refreshSession)
