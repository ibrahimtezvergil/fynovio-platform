import { QueryClient } from '@tanstack/react-query'
import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, url } from '@/test/authHandlers'
import { chooseFromCombobox, mockDetailApi, mockParties, recordRequests, wireOpportunity } from '@/test/opportunities'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import OpportunityDetailPage from './OpportunityDetailPage'
import OpportunityNewPage from './OpportunityNewPage'

const t = (key: string, options?: Record<string, unknown>) => tr(key, options, 'opportunities')

const definitions = [
  { id: 1, fieldName: 'note', label: 'Note', fieldType: 'text', isRequired: false, config: {}, status: 'Active', sortOrder: 1, rowVersion: 1 },
  { id: 2, fieldName: 'account', label: 'Account owner', fieldType: 'reference', isRequired: false, sortOrder: 2, rowVersion: 1, status: 'Active',
    config: { target: { boundedContext: 'masterdata', entityType: 'party' } } },
]

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated())
  mockParties()
  server.use(http.get(url(endpoints.crmSettings.customFields), () => HttpResponse.json(definitions)))
})

const queryClient = () => new QueryClient({ defaultOptions: { queries: { retryDelay: 0, staleTime: 0 } } })

describe('a reference field on the create form', () => {
  beforeEach(() => {
    server.use(http.get(url(endpoints.crmSettings.root), () => HttpResponse.json({
      defaultPipelineDefinitionId: null, opportunityCreationMode: 'Form', defaultOpportunityTypeId: null,
      requireLostReason: false, requireWonLine: true, defaultAssignmentMode: 'Manual', assignmentPolicy: 'AnyAssignablePrincipal',
      defaultPrincipal: null, defaultTeamId: null, defaultTerritoryId: null, rowVersion: 0,
      pipelines: [], opportunityTypes: [], lostReasons: [], customerNeeds: [],
    })))
  })

  it('is picked from the same customer search and sent as the bare id', async () => {
    const recorder = recordRequests()
    server.use(http.post(url(endpoints.opportunities.create), async ({ request }) => {
      await recorder.record(request)
      return HttpResponse.json({ opportunityId: 42, replayed: false })
    }))
    renderRoutes(
      [{ path: '/crm/opportunities/new', element: <OpportunityNewPage /> }, { path: '/crm/opportunities/:id', element: <p data-testid="detail-page" /> }],
      '/crm/opportunities/new', queryClient(),
    )

    await chooseFromCombobox(t('form.partyId.label'), 'Acme', /Acme Ltd/)
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '100' } })
    await chooseFromCombobox('Account owner', 'Bora', /Bora Tekstil/)
    expect(await screen.findByText('Bora Tekstil', { selector: '[data-testid="reference-account"] span' })).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))

    await screen.findByTestId('detail-page')
    expect(recorder.commands()[0].body).toEqual({ partyId: 1001, currency: 'TRY', estimatedAmount: 100, customFields: { account: 1002 } })
  })

  it('can be cleared after choosing, and is then left out of the command', async () => {
    const recorder = recordRequests()
    server.use(http.post(url(endpoints.opportunities.create), async ({ request }) => {
      await recorder.record(request)
      return HttpResponse.json({ opportunityId: 43, replayed: false })
    }))
    renderRoutes(
      [{ path: '/crm/opportunities/new', element: <OpportunityNewPage /> }, { path: '/crm/opportunities/:id', element: <p data-testid="detail-page" /> }],
      '/crm/opportunities/new', queryClient(),
    )

    await chooseFromCombobox(t('form.partyId.label'), 'Acme', /Acme Ltd/)
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '100' } })
    await chooseFromCombobox('Account owner', 'Bora', /Bora Tekstil/)
    fireEvent.click(await screen.findByRole('button', { name: t('customFields.reference.clear', { name: 'Account owner' }) }))
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))

    await screen.findByTestId('detail-page')
    expect(recorder.commands()[0].body).toEqual({ partyId: 1001, currency: 'TRY', estimatedAmount: 100, customFields: {} })
  })
})

describe('a reference field on the detail page', () => {
  const render = () => renderRoutes([{ path: '/crm/opportunities/:id', element: <OpportunityDetailPage /> }], '/crm/opportunities/12', queryClient())
  const card = async () => (await screen.findByText(t('customFields.title'), { selector: '[data-slot="card-title"]' })).closest('[data-slot="card"]') as HTMLElement

  it('shows the label this reader may see, and only the fact of unavailability when they may not', async () => {
    mockDetailApi(12, recordRequests(), {
      detail: () => HttpResponse.json(wireOpportunity({ customFields: { account: 1002 }, customFieldReferences: { account: { id: 1002, accessible: true, label: 'Bora Tekstil' } } })),
    })
    const first = render()
    expect(await within(await card()).findByText('Bora Tekstil')).toBeInTheDocument()
    first.unmount?.()
  })

  it('shows an unavailable reference without any label', async () => {
    mockDetailApi(12, recordRequests(), {
      detail: () => HttpResponse.json(wireOpportunity({ customFields: { account: 1002 }, customFieldReferences: { account: { id: 1002, accessible: false, label: null } } })),
    })
    render()

    const section = within(await card())
    expect(await section.findByText(t('customFields.reference.unavailableShort'))).toBeInTheDocument()
    expect(section.queryByText('Bora Tekstil')).not.toBeInTheDocument()
  })

  it('keeps an unavailable stored reference when an unrelated field is saved, and replaces it only on request', async () => {
    const recorder = recordRequests()
    mockDetailApi(12, recordRequests(), {
      detail: () => HttpResponse.json(wireOpportunity({ rowVersion: 5, customFields: { account: 1002, note: 'old' }, customFieldReferences: { account: { id: 1002, accessible: false, label: null } } })),
    })
    server.use(http.put(url(endpoints.opportunities.customFields(12)), async ({ request }) => {
      await recorder.record(request)
      return HttpResponse.json({ opportunityId: 12, replayed: false })
    }))
    render()

    fireEvent.click(within(await card()).getByRole('button', { name: t('customFields.edit') }))
    expect(await screen.findByText(t('customFields.reference.unavailable', { id: 1002 }))).toBeInTheDocument()
    fireEvent.change(screen.getByRole('textbox', { name: 'Note' }), { target: { value: 'new' } })
    fireEvent.click(screen.getByRole('button', { name: t('customFields.save') }))
    await waitFor(() => expect(recorder.seen.some((entry) => entry.method === 'PUT')).toBe(true))
    expect(recorder.seen.find((entry) => entry.method === 'PUT')?.body).toEqual({ expectedVersion: 5, customFields: { note: 'new', account: 1002 } })
  })

  it('words a refused reference by its code', async () => {
    mockDetailApi(12, recordRequests(), {
      detail: () => HttpResponse.json(wireOpportunity({ rowVersion: 5, customFields: { note: 'x' } })),
    })
    server.use(http.put(url(endpoints.opportunities.customFields(12)), () => HttpResponse.json(
      { status: 422, type: 'custom_field_invalid', title: 'invalid', errors: { account: ['bad'] }, codes: { account: ['invalid_reference'] } }, { status: 422 })))
    render()

    fireEvent.click(within(await card()).getByRole('button', { name: t('customFields.edit') }))
    await chooseFromCombobox('Account owner', 'Bora', /Bora Tekstil/)
    fireEvent.click(screen.getByRole('button', { name: t('customFields.save') }))

    expect(await screen.findByText(t('customFields.errors.invalid_reference'))).toBeInTheDocument()
  })
})
