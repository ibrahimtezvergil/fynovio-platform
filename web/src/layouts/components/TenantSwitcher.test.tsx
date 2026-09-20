import { fireEvent, screen, waitFor } from '@testing-library/react'
import { HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { queryClient } from '@/api/queryClient'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, problem, selectTenantHandler } from '@/test/authHandlers'
import { renderPage, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import { TenantSwitcher } from './TenantSwitcher'

beforeEach(resetSession)

const tenantButton = (id: number) => screen.getByRole('button', { name: new RegExp(tr('tenantSelector.tenantLabel', { id })) })

describe('TenantSwitcher', () => {
  it('renders nothing for a single-tenant account', () => {
    useSessionStore.getState().applyAuthResult(authenticated())
    const { container } = renderPage(<TenantSwitcher />)
    expect(container).toBeEmptyDOMElement()
  })

  it('lists the tenants of a multi-tenant account and marks the active one', () => {
    useSessionStore.getState().applyAuthResult(authenticated({ memberships: [{ tenantId: 1 }, { tenantId: 2 }] }))
    renderPage(<TenantSwitcher />)

    expect(tenantButton(1)).toHaveAttribute('aria-current', 'true')
    expect(tenantButton(2)).not.toHaveAttribute('aria-current')
  })

  it('switching asks the server for the new tenant, swaps the token and clears the query cache', async () => {
    useSessionStore.getState().applyAuthResult(authenticated({ memberships: [{ tenantId: 1 }, { tenantId: 2 }] }))
    const clear = vi.spyOn(queryClient, 'clear')
    const onSwitched = vi.fn()
    server.use(selectTenantHandler(() => HttpResponse.json({ accessToken: 'tenant-2-token', expiresIn: 600, activeTenant: { tenantId: 2 } })))
    renderPage(<TenantSwitcher onSwitched={onSwitched} />)

    fireEvent.click(tenantButton(2))

    await waitFor(() => expect(onSwitched).toHaveBeenCalled())
    expect(useSessionStore.getState().activeTenantId).toBe(2)
    expect(useSessionStore.getState().accessToken).toBe('tenant-2-token')
    expect(clear).toHaveBeenCalled()
  })

  it('clicking the already-active tenant does nothing', async () => {
    useSessionStore.getState().applyAuthResult(authenticated({ memberships: [{ tenantId: 1 }, { tenantId: 2 }] }))
    let calls = 0
    server.use(selectTenantHandler(() => { calls++; return HttpResponse.json({ accessToken: 't', expiresIn: 600, activeTenant: { tenantId: 1 } }) }))
    renderPage(<TenantSwitcher />)

    fireEvent.click(tenantButton(1))
    await new Promise((r) => setTimeout(r, 20))

    expect(calls).toBe(0)
  })

  it('a refused switch keeps the current tenant and token', async () => {
    useSessionStore.getState().applyAuthResult(authenticated({ memberships: [{ tenantId: 1 }, { tenantId: 2 }] }))
    const clear = vi.spyOn(queryClient, 'clear')
    const onSwitched = vi.fn()
    server.use(selectTenantHandler(() => problem(403, 'tenant_not_permitted')))
    renderPage(<TenantSwitcher onSwitched={onSwitched} />)

    fireEvent.click(tenantButton(2))

    await waitFor(() => expect(tenantButton(2)).toBeEnabled())
    expect(useSessionStore.getState().activeTenantId).toBe(1)
    expect(useSessionStore.getState().accessToken).toBe('access-token-1')
    expect(onSwitched).not.toHaveBeenCalled()
    expect(clear).not.toHaveBeenCalled()
  })
})
