import { screen } from '@testing-library/react'
import { Outlet } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { useSessionStore } from '@/lib/auth'
import { authenticated, noMembership, selectionRequired } from '@/test/authHandlers'
import { renderRoutes } from '@/test/render'
import { resetSession } from '@/test/session'
import { paths } from './paths'
import { ProtectedRoute } from './ProtectedRoute'
import { PublicOnlyRoute } from './PublicOnlyRoute'
import { SessionRoute } from './SessionRoute'

const app = () => [
  { path: '/', element: <p>ROOT</p> },
  { element: <PublicOnlyRoute />, children: [{ path: paths.login, element: <p>LOGIN PAGE</p> }] },
  { element: <SessionRoute />, children: [
    { path: paths.selectTenant, element: <p>SELECT TENANT PAGE</p> },
    { path: paths.noAccess, element: <p>NO ACCESS PAGE</p> },
  ] },
  { element: <ProtectedRoute />, children: [{ element: <Outlet />, children: [
    { path: paths.dashboard, element: <p>DASHBOARD SECRET</p> },
    { path: '/crm/pipeline', element: <p>PIPELINE SECRET</p> },
  ] }] },
]

const signInAs = (result: unknown) => useSessionStore.getState().applyAuthResult(result as never)

beforeEach(resetSession)

describe('ProtectedRoute', () => {
  it('while the session is unknown it renders a skeleton and NEVER the protected content or the login page', () => {
    renderRoutes(app(), paths.dashboard)

    expect(screen.queryByText('DASHBOARD SECRET')).not.toBeInTheDocument()
    expect(screen.queryByText('LOGIN PAGE')).not.toBeInTheDocument()
    expect(document.querySelector('[aria-busy="true"]')).toBeInTheDocument()
  })

  it('an authenticated session sees the protected page', () => {
    signInAs(authenticated())
    renderRoutes(app(), paths.dashboard)
    expect(screen.getByText('DASHBOARD SECRET')).toBeInTheDocument()
  })

  it.each(['unauthenticated', 'expired'] as const)('%s → login, carrying the deep link as a sanitized returnUrl', async (state) => {
    useSessionStore.getState().endSession(state)
    const { router } = renderRoutes(app(), '/crm/pipeline?x=1')

    expect(await screen.findByText('LOGIN PAGE')).toBeInTheDocument()
    expect(screen.queryByText('PIPELINE SECRET')).not.toBeInTheDocument()
    expect(router.state.location.pathname).toBe(paths.login)
    expect(new URLSearchParams(router.state.location.search).get('returnUrl')).toBe('/crm/pipeline?x=1')
  })

  it('after an explicit sign-out the login page does not remember the page just left', async () => {
    useSessionStore.getState().endSession('unauthenticated', { explicit: true })
    const { router } = renderRoutes(app(), '/crm/pipeline')

    await screen.findByText('LOGIN PAGE')
    expect(router.state.location.search).toBe('')
  })

  it('a signed-in account still choosing a tenant is sent to /select-tenant (with the deep link)', async () => {
    signInAs(selectionRequired())
    const { router } = renderRoutes(app(), '/crm/pipeline')

    expect(await screen.findByText('SELECT TENANT PAGE')).toBeInTheDocument()
    expect(screen.queryByText('PIPELINE SECRET')).not.toBeInTheDocument()
    expect(new URLSearchParams(router.state.location.search).get('returnUrl')).toBe('/crm/pipeline')
  })

  it('an account without any membership is sent to /no-access', async () => {
    signInAs(noMembership())
    renderRoutes(app(), paths.dashboard)

    expect(await screen.findByText('NO ACCESS PAGE')).toBeInTheDocument()
    expect(screen.queryByText('DASHBOARD SECRET')).not.toBeInTheDocument()
  })
})

describe('PublicOnlyRoute', () => {
  it('shows a skeleton, not the login form, while the session is unknown', () => {
    renderRoutes(app(), paths.login)
    expect(screen.queryByText('LOGIN PAGE')).not.toBeInTheDocument()
    expect(document.querySelector('[aria-busy="true"]')).toBeInTheDocument()
  })

  it.each(['unauthenticated', 'expired'] as const)('shows the login page when %s', (state) => {
    useSessionStore.getState().endSession(state)
    renderRoutes(app(), paths.login)
    expect(screen.getByText('LOGIN PAGE')).toBeInTheDocument()
  })

  it('sends a signed-in user to the sanitized returnUrl', async () => {
    signInAs(authenticated())
    const { router } = renderRoutes(app(), `${paths.login}?returnUrl=${encodeURIComponent('/crm/pipeline?x=1')}`)

    expect(await screen.findByText('PIPELINE SECRET')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/crm/pipeline')
  })

  it('never follows a hostile returnUrl — falls back to the dashboard (no open redirect)', async () => {
    signInAs(authenticated())
    const { router } = renderRoutes(app(), `${paths.login}?returnUrl=${encodeURIComponent('//evil.com')}`)

    expect(await screen.findByText('DASHBOARD SECRET')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe(paths.dashboard)
  })

  it('a fresh multi-tenant sign-in continues to tenant selection, keeping the returnUrl', async () => {
    signInAs(selectionRequired())
    const { router } = renderRoutes(app(), `${paths.login}?returnUrl=${encodeURIComponent('/crm/pipeline')}`)

    expect(await screen.findByText('SELECT TENANT PAGE')).toBeInTheDocument()
    expect(new URLSearchParams(router.state.location.search).get('returnUrl')).toBe('/crm/pipeline')
  })

  it('a fresh sign-in without membership continues to /no-access', async () => {
    signInAs(noMembership())
    renderRoutes(app(), paths.login)
    expect(await screen.findByText('NO ACCESS PAGE')).toBeInTheDocument()
  })
})

describe('SessionRoute', () => {
  it('unauthenticated visitors cannot reach /select-tenant or /no-access', async () => {
    useSessionStore.getState().endSession('unauthenticated')
    renderRoutes(app(), paths.selectTenant)
    expect(await screen.findByText('LOGIN PAGE')).toBeInTheDocument()
  })

  it('shows a skeleton while the session is unknown', () => {
    renderRoutes(app(), paths.noAccess)
    expect(screen.queryByText('NO ACCESS PAGE')).not.toBeInTheDocument()
    expect(document.querySelector('[aria-busy="true"]')).toBeInTheDocument()
  })
})
