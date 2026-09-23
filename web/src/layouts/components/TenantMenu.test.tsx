import { fireEvent, screen, waitFor } from '@testing-library/react'
import { HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, selectTenantHandler } from '@/test/authHandlers'
import { renderPage, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import { TenantMenu } from './TenantMenu'

beforeEach(resetSession)

const name = (id: number) => id === 1 ? 'Acme Türkiye' : 'Northwind'

describe('TenantMenu', () => {
  it('renders nothing without an active tenant', () => {
    const { container } = renderPage(<TenantMenu collapsed={false} />)
    expect(container).toBeEmptyDOMElement()
  })

  it('shows the active tenant without a switcher for a single-tenant account', () => {
    useSessionStore.getState().applyAuthResult(authenticated())
    renderPage(<TenantMenu collapsed={false} />)
    expect(screen.getByText(name(1))).toBeInTheDocument()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('lets a multi-tenant account switch from the sidebar and closes afterwards', async () => {
    useSessionStore.getState().applyAuthResult(authenticated({ memberships: [{ tenantId: 1, displayName: name(1) }, { tenantId: 2, displayName: name(2) }] }))
    server.use(selectTenantHandler(() => HttpResponse.json({ accessToken: 'tenant-2-token', expiresIn: 600, activeTenant: { tenantId: 2 } })))
    renderPage(<TenantMenu collapsed={false} />)

    fireEvent.click(screen.getByRole('button', { name: name(1) }))
    fireEvent.click(await screen.findByRole('button', { name: new RegExp(name(2), 'i') }))

    await waitFor(() => expect(useSessionStore.getState().activeTenantId).toBe(2))
    await waitFor(() => expect(screen.queryByRole('group', { name: tr('tenantSwitcher.label') })).not.toBeInTheDocument())
    expect(screen.getByRole('button', { name: name(2) })).toBeInTheDocument()
  })
})
