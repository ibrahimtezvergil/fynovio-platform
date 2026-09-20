import { create } from 'zustand'
import type { AuthResult, Membership, SessionStatus, SessionUser, TenantSelection } from './types'

function deriveInitials(name: string, email: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  const letters = parts.length > 1 ? `${parts[0][0]}${parts[parts.length - 1][0]}` : (parts[0] ?? email).slice(0, 2)
  return letters.toUpperCase()
}

interface SessionState {
  status: SessionStatus
  user: SessionUser | null
  memberships: Membership[]
  activeTenantId: number | null
  /** Held in memory ONLY. Never write it to localStorage/sessionStorage — a reload restores it through the refresh cookie. */
  accessToken: string | null
  /** Signed in, but the account belongs to no tenant (→ /no-access). */
  noMembership: boolean
  /** The user pressed "sign out" — the login screen must not carry a returnUrl then. */
  explicitSignOut: boolean
  applyAuthResult: (result: AuthResult) => void
  applyTenantSelection: (selection: TenantSelection) => void
  endSession: (status: 'unauthenticated' | 'expired', options?: { explicit?: boolean }) => void
}

const signedOut = () => ({
  user: null,
  memberships: [] as Membership[],
  activeTenantId: null,
  accessToken: null,
  noMembership: false,
})

export const useSessionStore = create<SessionState>()((set) => ({
  status: 'unknown',
  ...signedOut(),
  explicitSignOut: false,

  applyAuthResult: (result) => {
    const user: SessionUser = {
      id: String(result.account.id),
      name: result.account.displayName,
      email: result.account.email,
      initials: deriveInitials(result.account.displayName, result.account.email),
      locale: result.account.locale ?? null,
    }
    const base = { user, memberships: result.memberships, explicitSignOut: false }

    if (result.status === 'authenticated' && result.accessToken && result.activeTenant) {
      set({ ...base, status: 'authenticated', accessToken: result.accessToken, activeTenantId: result.activeTenant.tenantId, noMembership: false })
      return
    }
    set({
      ...base,
      status: 'tenant_unresolved',
      accessToken: null,
      activeTenantId: null,
      noMembership: result.status === 'no_membership' || result.memberships.length === 0,
    })
  },

  applyTenantSelection: (selection) =>
    set({ status: 'authenticated', accessToken: selection.accessToken, activeTenantId: selection.activeTenant.tenantId, noMembership: false }),

  endSession: (status, options) => set({ ...signedOut(), status, explicitSignOut: options?.explicit ?? false }),
}))

export function getAccessToken(): string | null {
  return useSessionStore.getState().accessToken
}
