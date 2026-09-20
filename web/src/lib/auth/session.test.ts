import { beforeEach, describe, expect, it } from 'vitest'
import { resetSession } from '@/test/session'
import { useSessionStore } from './session'
import type { AuthResult } from './types'

const result = (overrides: Partial<AuthResult> = {}): AuthResult => ({
  status: 'authenticated',
  accessToken: 'tok',
  expiresIn: 600,
  account: { id: 7, email: 'ada@example.com', displayName: 'Ada Lovelace', locale: 'tr' },
  activeTenant: { tenantId: 1 },
  memberships: [{ tenantId: 1 }],
  ...overrides,
})

beforeEach(resetSession)

describe('session store', () => {
  it('starts unknown with nothing in it', () => {
    const s = useSessionStore.getState()
    expect(s.status).toBe('unknown')
    expect(s.accessToken).toBeNull()
    expect(s.user).toBeNull()
  })

  it('authenticated: token, tenant and a derived user', () => {
    useSessionStore.getState().applyAuthResult(result())
    const s = useSessionStore.getState()
    expect(s.status).toBe('authenticated')
    expect(s.accessToken).toBe('tok')
    expect(s.activeTenantId).toBe(1)
    expect(s.user).toEqual({ id: '7', name: 'Ada Lovelace', email: 'ada@example.com', initials: 'AL', locale: 'tr' })
    expect(s.noMembership).toBe(false)
  })

  it('tenant_selection_required: signed in but tokenless, memberships kept', () => {
    useSessionStore.getState().applyAuthResult(
      result({ status: 'tenant_selection_required', accessToken: null, activeTenant: null, memberships: [{ tenantId: 1 }, { tenantId: 2 }] }),
    )
    const s = useSessionStore.getState()
    expect(s.status).toBe('tenant_unresolved')
    expect(s.accessToken).toBeNull()
    expect(s.activeTenantId).toBeNull()
    expect(s.memberships).toHaveLength(2)
    expect(s.noMembership).toBe(false)
  })

  it('no_membership: tenant_unresolved with the noMembership flag', () => {
    useSessionStore.getState().applyAuthResult(result({ status: 'no_membership', accessToken: null, activeTenant: null, memberships: [] }))
    const s = useSessionStore.getState()
    expect(s.status).toBe('tenant_unresolved')
    expect(s.noMembership).toBe(true)
  })

  it('an "authenticated" payload without a token can never yield an authenticated session', () => {
    useSessionStore.getState().applyAuthResult(result({ accessToken: null }))
    expect(useSessionStore.getState().status).toBe('tenant_unresolved')
    expect(useSessionStore.getState().accessToken).toBeNull()
  })

  it('tenant selection stores the new token and tenant', () => {
    useSessionStore.getState().applyAuthResult(
      result({ status: 'tenant_selection_required', accessToken: null, activeTenant: null, memberships: [{ tenantId: 1 }, { tenantId: 2 }] }),
    )
    useSessionStore.getState().applyTenantSelection({ accessToken: 'tok2', expiresIn: 600, activeTenant: { tenantId: 2 } })
    const s = useSessionStore.getState()
    expect(s.status).toBe('authenticated')
    expect(s.accessToken).toBe('tok2')
    expect(s.activeTenantId).toBe(2)
    expect(s.user?.name).toBe('Ada Lovelace') // identity survives a tenant switch
  })

  it.each(['unauthenticated', 'expired'] as const)('endSession(%s) wipes identity, tenant and token', (status) => {
    useSessionStore.getState().applyAuthResult(result())
    useSessionStore.getState().endSession(status)
    const s = useSessionStore.getState()
    expect(s.status).toBe(status)
    expect(s.accessToken).toBeNull()
    expect(s.user).toBeNull()
    expect(s.memberships).toEqual([])
    expect(s.activeTenantId).toBeNull()
  })

  it('remembers an explicit sign-out, and forgets it at the next sign-in', () => {
    useSessionStore.getState().endSession('unauthenticated', { explicit: true })
    expect(useSessionStore.getState().explicitSignOut).toBe(true)
    useSessionStore.getState().applyAuthResult(result())
    expect(useSessionStore.getState().explicitSignOut).toBe(false)
  })

  it('derives initials from a one-word name and from the email as a last resort', () => {
    useSessionStore.getState().applyAuthResult(result({ account: { id: 1, email: 'x@example.com', displayName: 'Plato', locale: null } }))
    expect(useSessionStore.getState().user?.initials).toBe('PL')
  })
})
