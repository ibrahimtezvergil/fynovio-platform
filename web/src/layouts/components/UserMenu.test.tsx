import { fireEvent, screen } from '@testing-library/react'
import { HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { queryClient } from '@/api/queryClient'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, logout as logoutHandler } from '@/test/authHandlers'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import { UserMenu } from './UserMenu'

beforeEach(resetSession)

const routes = () => [
  { path: '/login', element: <p>LOGIN PAGE</p> },
  { path: '/account/security', element: <p>SECURITY PAGE</p> },
  { path: '*', element: <UserMenu placement="topbar" /> },
]

const openMenu = () => fireEvent.click(screen.getByRole('button', { expanded: false }))
const signOutButton = () => screen.getByRole('button', { name: tr('userMenu.logout', {}, 'nav') })

describe('UserMenu sign-out', () => {
  it('renders nothing without a signed-in user', () => {
    useSessionStore.getState().endSession('unauthenticated')
    const { container } = renderRoutes(routes(), '/')
    expect(container).toBeEmptyDOMElement()
  })

  it('shows the identity from the session', () => {
    useSessionStore.getState().applyAuthResult(authenticated())
    renderRoutes(routes(), '/')
    expect(screen.getByText('Ada Lovelace')).toBeInTheDocument()
    expect(screen.getByText('ada@example.com')).toBeInTheDocument()
  })

  it('calls POST /auth/logout, clears the session and caches, then goes to /login', async () => {
    useSessionStore.getState().applyAuthResult(authenticated())
    const clear = vi.spyOn(queryClient, 'clear')
    let calls = 0
    server.use(logoutHandler(() => { calls++; return new HttpResponse(null, { status: 204 }) }))
    const { router } = renderRoutes(routes(), '/')

    openMenu()
    fireEvent.click(signOutButton())

    expect(await screen.findByText('LOGIN PAGE')).toBeInTheDocument()
    expect(calls).toBe(1)
    expect(router.state.location.pathname).toBe('/login')
    expect(useSessionStore.getState().status).toBe('unauthenticated')
    expect(useSessionStore.getState().accessToken).toBeNull()
    expect(clear).toHaveBeenCalled()
  })

  it('still signs out locally when the logout request fails', async () => {
    useSessionStore.getState().applyAuthResult(authenticated())
    server.use(logoutHandler(() => HttpResponse.error() as unknown as Response))
    renderRoutes(routes(), '/')

    openMenu()
    fireEvent.click(signOutButton())

    expect(await screen.findByText('LOGIN PAGE')).toBeInTheDocument()
    expect(useSessionStore.getState().status).toBe('unauthenticated')
    expect(useSessionStore.getState().accessToken).toBeNull()
  })

  it('offers the tenant switcher only to multi-tenant accounts', () => {
    useSessionStore.getState().applyAuthResult(authenticated())
    const single = renderRoutes(routes(), '/')
    openMenu()
    expect(screen.queryByRole('group', { name: tr('tenantSwitcher.label') })).not.toBeInTheDocument()
    single.unmount()

    useSessionStore.getState().applyAuthResult(authenticated({ memberships: [{ tenantId: 1 }, { tenantId: 2 }] }))
    renderRoutes(routes(), '/')
    openMenu()
    expect(screen.getByRole('group', { name: tr('tenantSwitcher.label') })).toBeInTheDocument()
  })
})

describe('UserMenu security entry', () => {
  it('opens the account-security page and closes the menu', async () => {
    useSessionStore.getState().applyAuthResult(authenticated())
    const { router } = renderRoutes(routes(), '/')

    openMenu()
    fireEvent.click(screen.getByRole('button', { name: tr('userMenu.security', {}, 'nav') }))

    expect(await screen.findByText('SECURITY PAGE')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/account/security')
  })
})
