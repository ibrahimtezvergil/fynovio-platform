import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { Can } from '@/components/common/Can'
import { useSessionStore } from '@/lib/auth'
import { hasPermission } from '@/lib/permissions'

afterEach(() => {
  useSessionStore.getState().endSession('unauthenticated')
})

function signIn() {
  useSessionStore.getState().applyAuthResult({
    status: 'authenticated',
    accessToken: 'test-token',
    expiresIn: 600,
    account: { id: 1, email: 'viewer@example.com', displayName: 'Viewer', locale: null },
    activeTenant: { tenantId: 1 },
    memberships: [{ tenantId: 1, displayName: 'Acme Türkiye' }],
  })
}

describe('permission seam', () => {
  it('keeps the mock policy resolution independent of the session store', () => {
    expect(hasPermission({ role: 'manager' }, 'deal.approve')).toBe(true)
    expect(hasPermission({ role: 'viewer' }, 'deal.approve')).toBe(false)
  })

  it('fails closed without a role — nothing is granted to a user that carries none', () => {
    expect(hasPermission({}, 'deal.approve')).toBe(false)
    expect(hasPermission(null, 'deal.approve')).toBe(false)
    expect(hasPermission(undefined, 'deal.approve')).toBe(false)
  })

  it('renders no Can children for a real session, which has no role', () => {
    signIn()
    render(<Can action="deal.approve">Approve</Can>)
    expect(screen.queryByText('Approve')).not.toBeInTheDocument()
  })
})
