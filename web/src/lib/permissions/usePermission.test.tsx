import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { Can } from '@/components/common/Can'
import { useAuthStore } from '@/features/auth/store/useAuthStore'
import { hasPermission } from '@/lib/permissions'

afterEach(() => {
  useAuthStore.setState({ isAuthenticated: false, user: null, token: null })
})

describe('permission seam', () => {
  it('keeps the mock policy resolution independent of the auth store', () => {
    expect(hasPermission({ role: 'manager' }, 'deal.approve')).toBe(true)
    expect(hasPermission({ role: 'viewer' }, 'deal.approve')).toBe(false)
  })

  it('renders Can children only for a permitted signed-in user', () => {
    useAuthStore.setState({
      isAuthenticated: true,
      token: 'test-token',
      user: { id: 'viewer', name: 'Viewer', email: 'viewer@example.com', role: 'viewer', initials: 'V' },
    })
    const { rerender } = render(<Can action="deal.approve">Approve</Can>)
    expect(screen.queryByText('Approve')).not.toBeInTheDocument()

    useAuthStore.setState({
      user: { id: 'manager', name: 'Manager', email: 'manager@example.com', role: 'manager', initials: 'M' },
    })
    rerender(<Can action="deal.approve">Approve</Can>)
    expect(screen.getByText('Approve')).toBeInTheDocument()
  })
})
