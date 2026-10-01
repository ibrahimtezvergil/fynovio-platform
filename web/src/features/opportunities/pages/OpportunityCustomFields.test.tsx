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
  { id: 1, fieldName: 'budget_code', label: 'Budget code', fieldType: 'text', isRequired: true, config: {}, status: 'Active', sortOrder: 1, rowVersion: 1 },
  { id: 2, fieldName: 'priority', label: 'Priority', fieldType: 'select', isRequired: false, sortOrder: 2, rowVersion: 1, status: 'Active',
    config: { options: [{ key: 'low', label: 'Low', isDeprecated: false }, { key: 'high', label: 'High', isDeprecated: false }] } },
  { id: 3, fieldName: 'legacy_ref', label: 'Legacy ref', fieldType: 'text', isRequired: false, config: {}, status: 'Deprecated', sortOrder: 3, rowVersion: 2 },
]

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated())
  server.use(http.get(url(endpoints.crmSettings.customFields), () => HttpResponse.json(definitions)))
})

const queryClient = () => new QueryClient({ defaultOptions: { queries: { retryDelay: 0, staleTime: 0 } } })

describe('custom fields on the create form', () => {
  beforeEach(() => {
    mockParties()
    server.use(http.get(url(endpoints.crmSettings.root), () => HttpResponse.json({
      defaultPipelineDefinitionId: null, opportunityCreationMode: 'Form', defaultOpportunityTypeId: null,
      requireLostReason: false, requireWonLine: true, defaultAssignmentMode: 'Manual', assignmentPolicy: 'AnyAssignablePrincipal',
      defaultPrincipal: null, defaultTeamId: null, defaultTerritoryId: null, rowVersion: 0,
      pipelines: [], opportunityTypes: [], lostReasons: [], customerNeeds: [],
    })))
  })

  const render = () => renderRoutes(
    [
      { path: '/crm/opportunities/new', element: <OpportunityNewPage /> },
      { path: '/crm/opportunities/:id', element: <p data-testid="detail-page" /> },
    ],
    '/crm/opportunities/new',
    queryClient(),
  )

  it('renders only active fields, blocks a missing required one, then sends the values with the command', async () => {
    const recorder = recordRequests()
    server.use(http.post(url(endpoints.opportunities.create), async ({ request }) => {
      await recorder.record(request)
      return HttpResponse.json({ opportunityId: 42, replayed: false })
    }))
    render()

    await chooseFromCombobox(t('form.partyId.label'), 'Acme', /Acme Ltd/)
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '100' } })
    expect(await screen.findByRole('textbox', { name: 'Budget code *' })).toBeInTheDocument()
    expect(screen.queryByText('Legacy ref')).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))
    expect(await screen.findByText(t('customFields.errors.required'))).toBeInTheDocument()
    expect(recorder.commands()).toHaveLength(0)

    fireEvent.change(screen.getByRole('textbox', { name: 'Budget code *' }), { target: { value: 'B-7' } })
    fireEvent.change(screen.getByRole('combobox', { name: 'Priority' }), { target: { value: 'high' } })
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))

    await screen.findByTestId('detail-page')
    expect(recorder.commands()[0].body).toEqual({ partyId: 1001, currency: 'TRY', estimatedAmount: 100, customFields: { budget_code: 'B-7', priority: 'high' } })
  })
})

describe('custom fields on the detail page', () => {
  const render = () => renderRoutes([{ path: '/crm/opportunities/:id', element: <OpportunityDetailPage /> }], '/crm/opportunities/12', queryClient())
  const card = async () => (await screen.findByText(t('customFields.title'), { selector: '[data-slot="card-title"]' })).closest('[data-slot="card"]') as HTMLElement
  beforeEach(() => mockParties())

  it('shows stored values by label and keeps a deprecated field read-only', async () => {
    mockDetailApi(12, recordRequests(), {
      detail: () => HttpResponse.json(wireOpportunity({ customFields: { budget_code: 'B-1', priority: 'low', legacy_ref: 'OLD-9' } })),
    })
    render()

    const section = within(await card())
    expect(await section.findByText('B-1')).toBeInTheDocument()
    expect(section.getByText('Low')).toBeInTheDocument()
    expect(section.getByText('OLD-9')).toBeInTheDocument()
    expect(section.getByText(t('customFields.deprecated'))).toBeInTheDocument()
  })

  it('saves a full replacement of the active fields and words a 422 by its code', async () => {
    const recorder = recordRequests()
    let reject = true
    mockDetailApi(12, recordRequests(), {
      detail: () => HttpResponse.json(wireOpportunity({ rowVersion: 5, customFields: { budget_code: 'B-1', legacy_ref: 'OLD-9' } })),
    })
    server.use(http.put(url(endpoints.opportunities.customFields(12)), async ({ request }) => {
      await recorder.record(request)
      if (reject) {
        reject = false
        return HttpResponse.json({ status: 422, type: 'custom_field_invalid', title: 'invalid', errors: { priority: ['bad'] }, codes: { priority: ['invalid_option'] } }, { status: 422 })
      }
      return HttpResponse.json({ opportunityId: 12, replayed: false })
    }))
    render()

    const section = within(await card())
    fireEvent.click(await section.findByRole('button', { name: t('customFields.edit') }))
    fireEvent.change(section.getByRole('combobox', { name: 'Priority' }), { target: { value: 'high' } })
    fireEvent.click(section.getByRole('button', { name: t('customFields.save') }))

    expect(await section.findByText(t('customFields.errors.invalid_option'))).toBeInTheDocument()
    const sent = recorder.seen.find((entry) => entry.method === 'PUT')
    expect(sent?.body).toEqual({ expectedVersion: 5, customFields: { budget_code: 'B-1', priority: 'high' } })

    fireEvent.click(section.getByRole('button', { name: t('customFields.save') }))
    await waitFor(() => expect(section.queryByRole('button', { name: t('customFields.save') })).not.toBeInTheDocument())
  })

  it('offers no edit on an archived opportunity', async () => {
    mockDetailApi(12, recordRequests(), {
      detail: () => HttpResponse.json(wireOpportunity({ isArchived: true, customFields: { budget_code: 'B-1' } })),
    })
    render()

    const section = within(await card())
    expect(await section.findByText('B-1')).toBeInTheDocument()
    expect(section.queryByRole('button', { name: t('customFields.edit') })).not.toBeInTheDocument()
  })
})
