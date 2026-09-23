import { fireEvent, screen, waitFor } from '@testing-library/react'
import { HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { queryClient } from '@/api/queryClient'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { problem, selectionRequired, selectTenantHandler } from '@/test/authHandlers'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import TenantSelectorPage from './TenantSelectorPage'

beforeEach(resetSession)

const routes = () => [
  { path: '/next', element: <p>LANDED</p> },
  { path: '/crm/pipeline', element: <p>PIPELINE</p> },
  { path: '/dashboard', element: <p>DASHBOARD</p> },
  { path: '/no-access', element: <p>NO ACCESS</p> },
  { path: '/select-tenant', element: <TenantSelectorPage /> },
]

const tenantName = (id: number) => id === 1 ? 'Acme Türkiye' : id === 2 ? 'Northwind' : `Tenant ${id}`
const tenantButton = (id: number) => screen.getByRole('button', { name: new RegExp(tenantName(id), 'i') })

describe('TenantSelectorPage', () => {
  it('lists one button per membership', () => {
    useSessionStore.getState().applyAuthResult(selectionRequired([{ tenantId: 4, displayName: tenantName(4) }, { tenantId: 9, displayName: tenantName(9) }]))
    renderRoutes(routes(), '/select-tenant')

    expect(tenantButton(4)).toBeInTheDocument()
    expect(tenantButton(9)).toBeInTheDocument()
  })

  it('selecting a tenant asks the server, clears the previous tenant’s cache and lands on the returnUrl', async () => {
    useSessionStore.getState().applyAuthResult(selectionRequired())
    const clear = vi.spyOn(queryClient, 'clear')
    server.use(selectTenantHandler(() => HttpResponse.json({ accessToken: 'tenant-2-token', expiresIn: 600, activeTenant: { tenantId: 2 } })))
    const { router } = renderRoutes(routes(), `/select-tenant?returnUrl=${encodeURIComponent('/crm/pipeline')}`)

    fireEvent.click(tenantButton(2))

    expect(await screen.findByText('PIPELINE')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/crm/pipeline')
    expect(useSessionStore.getState().activeTenantId).toBe(2)
    expect(useSessionStore.getState().accessToken).toBe('tenant-2-token')
    expect(clear).toHaveBeenCalled()
  })

  it('a hostile returnUrl is ignored: the destination is the dashboard', async () => {
    useSessionStore.getState().applyAuthResult(selectionRequired())
    server.use(selectTenantHandler(() => HttpResponse.json({ accessToken: 't', expiresIn: 600, activeTenant: { tenantId: 1 } })))
    renderRoutes(routes(), `/select-tenant?returnUrl=${encodeURIComponent('https://evil.example')}`)

    fireEvent.click(tenantButton(1))

    expect(await screen.findByText('DASHBOARD')).toBeInTheDocument()
  })

  it('a 403 shows a message, keeps the page and does not sign the user in', async () => {
    useSessionStore.getState().applyAuthResult(selectionRequired())
    server.use(selectTenantHandler(() => problem(403, 'tenant_not_permitted')))
    renderRoutes(routes(), '/select-tenant')

    fireEvent.click(tenantButton(1))

    expect(await screen.findByRole('alert')).toHaveTextContent(tr('tenantSelector.notPermitted'))
    expect(useSessionStore.getState().status).toBe('tenant_unresolved')
    expect(tenantButton(1)).toBeEnabled()
  })

  it('disables every choice while a selection is in flight (no double selection)', async () => {
    useSessionStore.getState().applyAuthResult(selectionRequired())
    let calls = 0
    let release!: () => void
    const gate = new Promise<void>((r) => { release = r })
    server.use(selectTenantHandler(async () => { calls++; await gate; return HttpResponse.json({ accessToken: 't', expiresIn: 600, activeTenant: { tenantId: 1 } }) }))
    renderRoutes(routes(), '/select-tenant')

    fireEvent.click(tenantButton(1))
    await waitFor(() => expect(tenantButton(2)).toBeDisabled())
    fireEvent.click(tenantButton(2))

    release()
    await screen.findByText('DASHBOARD')
    expect(calls).toBe(1)
  })

  it('an account without memberships is sent to /no-access', async () => {
    useSessionStore.getState().applyAuthResult({ status: 'no_membership', account: { id: 1, email: 'a@b.co', displayName: 'A', locale: null }, activeTenant: null, memberships: [] })
    renderRoutes(routes(), '/select-tenant')

    expect(await screen.findByText('NO ACCESS')).toBeInTheDocument()
  })
})
