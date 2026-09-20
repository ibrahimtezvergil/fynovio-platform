/** Outward-facing surface of the shared session module (features may import it; it imports no feature). */
export { useSessionStore, getAccessToken } from './session'
export { bootstrapSession, login, logout, refreshSession, selectTenant } from './sessionClient'
export { returnUrlQuery, sanitizeReturnUrl } from './returnUrl'
export { SessionGate } from './SessionGate'
export type { Membership, SessionStatus, SessionUser } from './types'
