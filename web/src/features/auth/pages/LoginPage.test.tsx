import { screen } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import LoginPage from './LoginPage'

beforeEach(resetSession)

describe('LoginPage', () => {
  it('links to the password-reset flow', () => {
    renderRoutes([{ path: '/login', element: <LoginPage /> }], '/login')
    expect(screen.getByRole('link', { name: tr('loginPage.forgotPassword') })).toHaveAttribute('href', '/forgot-password')
  })

  it('shows the "password updated" notice only right after a reset', () => {
    renderRoutes([{ path: '/login', element: <LoginPage /> }], { pathname: '/login', state: { notice: 'passwordUpdated' } })
    expect(screen.getByText(tr('loginPage.passwordUpdated'))).toBeInTheDocument()
  })

  it('shows no notice on a plain visit, or for an unknown notice value', () => {
    const { unmount } = renderRoutes([{ path: '/login', element: <LoginPage /> }], '/login')
    expect(screen.queryByText(tr('loginPage.passwordUpdated'))).not.toBeInTheDocument()
    unmount()

    renderRoutes([{ path: '/login', element: <LoginPage /> }], { pathname: '/login', state: { notice: '<b>injected</b>' } })
    expect(screen.queryByText(tr('loginPage.passwordUpdated'))).not.toBeInTheDocument()
    expect(screen.queryByText('injected')).not.toBeInTheDocument()
  })
})
