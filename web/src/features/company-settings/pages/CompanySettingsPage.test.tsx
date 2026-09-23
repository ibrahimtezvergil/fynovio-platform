import { fireEvent, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, problem, selectTenantHandler, url } from '@/test/authHandlers'
import { renderPage, renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import CompanySettingsPage from './CompanySettingsPage'

const t = (key: string, options?: Record<string, unknown>) => tr(key, options, 'company-settings')
const settings = {
  displayName: 'Acme Logistics', legalName: null, taxNumber: null, taxOffice: null, email: 'hello@acme.test', phone: null,
  address: null, timezone: 'Europe/Istanbul', currencyCode: 'TRY', rowVersion: 3,
} as const
const accessOverview = {
  revision: 3, canInvite: true, canGrant: true, canRevoke: true, canManageRoles: true,
  availableActions: [
    { key: 'crm.opportunity.read', ownerModule: 'CRM', resourceType: 'CRM.Opportunity' },
    { key: 'crm.opportunity.list', ownerModule: 'CRM', resourceType: 'CRM.Opportunity' },
  ],
  members: [{
    principalIssuer: 'https://id.fynovio.local', principalSubject: 'account-1', displayName: 'Ada Yılmaz', email: 'ada@acme.test', status: 'active',
    assignments: [{ assignmentId: 9, roleKey: 'crm_manager', roleName: 'CRM Manager', canRevoke: true }],
  }],
  roles: [{ key: 'crm_manager', name: 'CRM Manager', origin: 'system_template', canEdit: false, permissions: [{ actionKey: 'crm.opportunity.read', relation: null }] }],
  pendingInvitations: [],
} as const

beforeAll(() => {
  globalThis.IntersectionObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof IntersectionObserver
})

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated({ activeTenant: { tenantId: 1 } }))
})

describe('CompanySettingsPage', () => {
  it('renders values loaded from the real company settings contract', async () => {
    server.use(http.get(url(endpoints.companySettings.profile), () => HttpResponse.json(settings)))
    renderPage(<CompanySettingsPage />)
    expect(await screen.findByDisplayValue('Acme Logistics')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: t('page.title') })).toBeInTheDocument()
    expect(screen.queryByText(/profile settings|profil ayarları/i)).not.toBeInTheDocument()
    const sectionNav = screen.getByRole('navigation', { name: t('page.sectionNavLabel') })
    const tenantMenu = screen.getByText('Acme Türkiye')
    expect(sectionNav).toBeInTheDocument()
    expect(tenantMenu.compareDocumentPosition(sectionNav) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(screen.getByRole('link', { name: t('page.sectionIdentity') })).toHaveAttribute('href', '/company/settings')
  })

  it('submits the full replacement with the server version and resets its dirty state', async () => {
    let body: Record<string, unknown> | undefined
    server.use(
      http.get(url(endpoints.companySettings.profile), () => HttpResponse.json(settings)),
      http.put(url(endpoints.companySettings.profile), async ({ request }) => {
        body = await request.json() as Record<string, unknown>
        return HttpResponse.json({ settings: { ...settings, displayName: 'Acme Europe', rowVersion: 4 }, replayed: false })
      }),
    )
    renderPage(<CompanySettingsPage />)
    fireEvent.change(await screen.findByDisplayValue('Acme Logistics'), { target: { value: 'Acme Europe' } })
    fireEvent.click(screen.getByRole('button', { name: t('saveBar.save') }))
    await waitFor(() => expect(body).toMatchObject({ displayName: 'Acme Europe', expectedVersion: 3 }))
    await waitFor(() => expect(screen.getByText(t('saveBar.allSaved'))).toBeInTheDocument())
  })

  it('asks before switching tenant when the form has unsaved changes', async () => {
    let switchCalls = 0
    useSessionStore.getState().applyAuthResult(authenticated({ activeTenant: { tenantId: 1 }, memberships: [{ tenantId: 1, displayName: 'Acme Logistics' }, { tenantId: 2, displayName: 'Northwind' }] }))
    server.use(
      http.get(url(endpoints.companySettings.profile), () => HttpResponse.json(settings)),
      selectTenantHandler(() => {
        switchCalls++
        return HttpResponse.json({ accessToken: 'tenant-2-token', expiresIn: 600, activeTenant: { tenantId: 2 } })
      }),
    )
    renderPage(<CompanySettingsPage />)

    fireEvent.change(await screen.findByDisplayValue('Acme Logistics'), { target: { value: 'Unsaved Acme' } })
    fireEvent.click(screen.getByRole('button', { name: 'Acme Logistics' }))
    fireEvent.click(screen.getByRole('button', { name: /northwind/i }))

    expect(await screen.findByRole('heading', { name: t('context.unsavedTitle') })).toBeInTheDocument()
    expect(switchCalls).toBe(0)
    fireEvent.click(screen.getByRole('button', { name: t('context.cancel') }))
    await waitFor(() => expect(screen.queryByRole('heading', { name: t('context.unsavedTitle') })).not.toBeInTheDocument())
    expect(useSessionStore.getState().activeTenantId).toBe(1)
  })

  it('warns the browser before closing or reloading an unsaved form', async () => {
    server.use(http.get(url(endpoints.companySettings.profile), () => HttpResponse.json(settings)))
    renderPage(<CompanySettingsPage />)
    fireEvent.change(await screen.findByDisplayValue('Acme Logistics'), { target: { value: 'Unsaved Acme' } })

    const event = new Event('beforeunload', { cancelable: true })
    window.dispatchEvent(event)

    expect(event.defaultPrevented).toBe(true)
  })

  it('switches only after the user discards the unsaved changes', async () => {
    useSessionStore.getState().applyAuthResult(authenticated({ activeTenant: { tenantId: 1 }, memberships: [{ tenantId: 1, displayName: 'Acme Logistics' }, { tenantId: 2, displayName: 'Northwind' }] }))
    server.use(
      http.get(url(endpoints.companySettings.profile), () => HttpResponse.json(settings)),
      selectTenantHandler(() => HttpResponse.json({ accessToken: 'tenant-2-token', expiresIn: 600, activeTenant: { tenantId: 2 } })),
    )
    renderPage(<CompanySettingsPage />)

    fireEvent.change(await screen.findByDisplayValue('Acme Logistics'), { target: { value: 'Unsaved Acme' } })
    fireEvent.click(screen.getByRole('button', { name: 'Acme Logistics' }))
    fireEvent.click(screen.getByRole('button', { name: /northwind/i }))
    fireEvent.click(await screen.findByRole('button', { name: t('context.discardAndSwitch') }))

    await waitFor(() => expect(useSessionStore.getState().activeTenantId).toBe(2))
  })

  it('shows an access explanation instead of a blank page on a denied read', async () => {
    server.use(http.get(url(endpoints.companySettings.profile), () => problem(403, 'authorization_denied')))
    renderPage(<CompanySettingsPage />)
    expect(await screen.findByText(t('problem.forbiddenTitle'))).toBeInTheDocument()
  })

  it('manages members from its own page-sized section', async () => {
    let invitation: Record<string, unknown> | undefined
    let invitationKey: string | null = null
    server.use(
      http.get(url(endpoints.companySettings.profile), () => HttpResponse.json(settings)),
      http.get(url(endpoints.companySettings.access), () => HttpResponse.json(accessOverview)),
      http.post(url(endpoints.companySettings.invitations), async ({ request }) => {
        invitation = await request.json() as Record<string, unknown>
        invitationKey = request.headers.get('Idempotency-Key')
        return HttpResponse.json({ status: 'accepted' }, { status: 202 })
      }),
    )
    renderRoutes([{ path: '/company/settings/:section', element: <CompanySettingsPage /> }], '/company/settings/members')
    expect(await screen.findByText('Ada Yılmaz')).toBeInTheDocument()
    expect(screen.getAllByText('CRM Manager')).toHaveLength(2)
    fireEvent.change(screen.getByLabelText(t('members.email')), { target: { value: 'new@acme.test' } })
    fireEvent.click(screen.getByRole('button', { name: t('members.invite') }))
    await waitFor(() => expect(invitation).toMatchObject({ email: 'new@acme.test' }))
    expect(invitationKey).toBeTruthy()
    expect(screen.queryByText(t('roles.ownerScope'))).not.toBeInTheDocument()
  })

  it('shows pending invitations and lets an administrator cancel one', async () => {
    const invitationId = 'a89d570f-1cf8-45dc-a16e-65b49baa3412'
    let cancelled = false
    server.use(
      http.get(url(endpoints.companySettings.profile), () => HttpResponse.json(settings)),
      http.get(url(endpoints.companySettings.access), () => HttpResponse.json({
        ...accessOverview,
        pendingInvitations: [{ invitationId, email: 'pending@acme.test', displayName: null,
          roleKey: 'crm_manager', expiresAt: '2026-10-01T12:00:00Z' }],
      })),
      http.delete(url(endpoints.companySettings.invitation(invitationId)), () => {
        cancelled = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderRoutes([{ path: '/company/settings/:section', element: <CompanySettingsPage /> }], '/company/settings/members')
    expect(await screen.findByText('pending@acme.test')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: t('members.cancelInvitation') }))
    await waitFor(() => expect(cancelled).toBe(true))
  })

  it('creates a custom role from a searchable permission picker', async () => {
    let body: Record<string, unknown> | undefined
    server.use(
      http.get(url(endpoints.companySettings.profile), () => HttpResponse.json(settings)),
      http.get(url(endpoints.companySettings.access), () => HttpResponse.json(accessOverview)),
      http.post(url(endpoints.companySettings.roles), async ({ request }) => {
        body = await request.json() as Record<string, unknown>
        return HttpResponse.json({ roleKey: 'custom-1', revision: 4, replayed: false })
      }),
    )
    renderRoutes([{ path: '/company/settings/:section', element: <CompanySettingsPage /> }], '/company/settings/roles')
    fireEvent.click(await screen.findByRole('button', { name: t('roles.create') }))
    fireEvent.change(screen.getByLabelText(t('roles.name')), { target: { value: 'Satış analisti' } })
    fireEvent.click(screen.getByLabelText('Fırsat görüntüle'))
    fireEvent.click(screen.getByRole('button', { name: t('roles.save') }))
    await waitFor(() => expect(body).toMatchObject({ name: 'Satış analisti', actionKeys: ['crm.opportunity.read'], expectedRevision: 3 }))
  })

  it('still shows access data while an API process is being restarted to the current contract', async () => {
    const { revision: _revision, canManageRoles: _canManageRoles, availableActions: _availableActions, ...legacy } = accessOverview
    const legacyRoles = legacy.roles.map(({ canEdit: _canEdit, ...role }) => role)
    server.use(
      http.get(url(endpoints.companySettings.profile), () => HttpResponse.json(settings)),
      http.get(url(endpoints.companySettings.access), () => HttpResponse.json({ ...legacy, roles: legacyRoles })),
    )
    renderRoutes([{ path: '/company/settings/:section', element: <CompanySettingsPage /> }], '/company/settings/roles')
    expect(await screen.findByText('CRM Manager')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: t('roles.create') })).not.toBeInTheDocument()
  })
})
