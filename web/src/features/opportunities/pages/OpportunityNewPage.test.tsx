import { QueryClient } from '@tanstack/react-query'
import { fireEvent, screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, url } from '@/test/authHandlers'
import { chooseFromCombobox, mockParties, problemResponse, recordRequests, typeInCombobox, wirePath } from '@/test/opportunities'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import OpportunityNewPage from './OpportunityNewPage'

const t = (key: string, options?: Record<string, unknown>) => tr(key, options, 'opportunities')

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated())
  mockParties()
  server.use(http.get(url(endpoints.crmSettings.root), () => HttpResponse.json({
    defaultPipelineDefinitionId: null, opportunityCreationMode: 'Form', defaultOpportunityTypeId: null,
    requireLostReason: false, requireWonLine: true, defaultAssignmentMode: 'Manual', assignmentPolicy: 'AnyAssignablePrincipal',
    defaultPrincipal: null, defaultTeamId: null, defaultTerritoryId: null, rowVersion: 0,
    pipelines: [], opportunityTypes: [], lostReasons: [], customerNeeds: [],
  })))
})

const partyLabel = () => t('form.partyId.label')
const chooseParty = () => chooseFromCombobox(partyLabel(), 'Acme', /Acme Ltd/)

const render = () =>
  renderRoutes(
    [
      { path: '/crm/opportunities', element: <p>LIST</p> },
      { path: '/crm/opportunities/new', element: <OpportunityNewPage /> },
      { path: '/crm/opportunities/:id', element: <p data-testid="detail-page">{window.location.pathname}</p> },
    ],
    '/crm/opportunities/new',
    new QueryClient({ defaultOptions: { queries: { retryDelay: 0, staleTime: 0 } } }),
  )

describe('opportunity create — validation', () => {
  it('follows the tenant-configured wizard flow without changing the create command contract', async () => {
    const recorder = recordRequests()
    server.use(
      http.get(url(endpoints.crmSettings.root), () => HttpResponse.json({
        defaultPipelineDefinitionId: null, opportunityCreationMode: 'Wizard', defaultOpportunityTypeId: null,
        requireLostReason: false, requireWonLine: true, defaultAssignmentMode: 'Manual', assignmentPolicy: 'AnyAssignablePrincipal',
        defaultPrincipal: null, defaultTeamId: null, defaultTerritoryId: null, rowVersion: 0,
        pipelines: [], opportunityTypes: [], lostReasons: [], customerNeeds: [],
      })),
      http.post(url(endpoints.opportunities.create), async ({ request }) => {
        await recorder.record(request)
        return HttpResponse.json({ opportunityId: 77, replayed: false })
      }),
    )
    render()
    await chooseParty()
    fireEvent.click(await screen.findByRole('button', { name: t('form.next') }))
    await screen.findByRole('combobox', { name: t('form.currency.label') })
    expect(screen.queryByRole('combobox', { name: partyLabel() })).not.toBeInTheDocument()
    fireEvent.change(await screen.findByRole('combobox', { name: t('form.currency.label') }), { target: { value: 'EUR' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '125' } })
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))
    await screen.findByTestId('detail-page')
    expect(recorder.commands()[0].body).toEqual({ partyId: 1001, currency: 'EUR', estimatedAmount: 125 })
  })

  it('shows field errors on empty submit', async () => {
    server.use(http.post(url(endpoints.opportunities.create), () => HttpResponse.json({ opportunityId: 1, replayed: false })))
    render()

    const submitButton = screen.getByRole('button', { name: t('form.submit') })
    fireEvent.click(submitButton)

    // Wait for at least one validation error to appear
    const partyError = await screen.findByText(t('form.partyId.invalid'))
    expect(partyError).toBeInTheDocument()
  })

  it('sends no request on empty submit', async () => {
    const recorder = recordRequests()
    server.use(
      http.post(url(endpoints.opportunities.create), async ({ request }) => {
        await recorder.record(request)
        return HttpResponse.json({ opportunityId: 1, replayed: false })
      }),
    )
    render()

    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))
    await screen.findByText(t('form.partyId.invalid'))

    expect(recorder.commands()).toHaveLength(0)
  })

  it('offers only supported currency values', async () => {
    render()
    const currency = await screen.findByRole('combobox', { name: t('form.currency.label') })
    expect(currency.querySelectorAll('option')).toHaveLength(3)
    expect(currency).toHaveValue('TRY')
  })

  it('shows decimals error for amounts with 3+ decimals', async () => {
    server.use(http.post(url(endpoints.opportunities.create), () => HttpResponse.json({ opportunityId: 1, replayed: false })))
    render()

    await chooseParty()
    fireEvent.change(screen.getByRole('combobox', { name: t('form.currency.label') }), { target: { value: 'TRY' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '100.123' } })
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))

    expect(await screen.findByText(t('form.estimatedAmount.decimals'))).toBeInTheDocument()
  })

  it('keeps typed values when validation fails', async () => {
    server.use(http.post(url(endpoints.opportunities.create), () => HttpResponse.json({ opportunityId: 1, replayed: false })))
    render()

    await chooseParty()
    const partyInput = screen.getByRole('combobox', { name: partyLabel() })
    const currencyInput = screen.getByRole('combobox', { name: t('form.currency.label') })
    const amountInput = screen.getByRole('textbox', { name: t('form.estimatedAmount.label') })

    fireEvent.change(currencyInput, { target: { value: 'EUR' } })
    fireEvent.change(amountInput, { target: { value: '250.123' } })

    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))
    await screen.findByText(t('form.estimatedAmount.decimals'))

    expect(partyInput).toHaveValue('Acme Ltd')
    expect(amountInput).toHaveValue('250.123')
    expect(currencyInput).toHaveValue('EUR')
  })
})

describe('opportunity create — success', () => {
  it('POSTs with exact body and navigates to detail', async () => {
    const recorder = recordRequests()
    server.use(
      http.post(url(endpoints.opportunities.create), async ({ request }) => {
        await recorder.record(request)
        return HttpResponse.json({ opportunityId: 42, replayed: false })
      }),
    )
    render()

    await chooseParty()
    fireEvent.change(screen.getByRole('combobox', { name: t('form.currency.label') }), { target: { value: 'TRY' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '250.5' } })
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))

    await screen.findByTestId('detail-page')
    expect(recorder.commands()).toHaveLength(1)
    expect(recorder.commands()[0].body).toEqual({ partyId: 1001, currency: 'TRY', estimatedAmount: 250.5 })
  })

  it('sends Idempotency-Key header', async () => {
    const recorder = recordRequests()
    server.use(
      http.post(url(endpoints.opportunities.create), async ({ request }) => {
        await recorder.record(request)
        return HttpResponse.json({ opportunityId: 42, replayed: false })
      }),
    )
    render()

    await chooseParty()
    fireEvent.change(screen.getByRole('combobox', { name: t('form.currency.label') }), { target: { value: 'EUR' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '500' } })
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))

    await screen.findByTestId('detail-page')
    expect(recorder.commands()[0].idempotencyKey).toMatch(/\S{8,}/)
  })

  it('navigates to detail route with created opportunity ID', async () => {
    server.use(http.post(url(endpoints.opportunities.create), () => HttpResponse.json({ opportunityId: 42, replayed: false })))
    render()

    await chooseParty()
    fireEvent.change(screen.getByRole('combobox', { name: t('form.currency.label') }), { target: { value: 'TRY' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '250' } })
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))

    // After navigation, the detail page element should be visible (form disappears)
    expect(await screen.findByTestId('detail-page')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: t('form.submit') })).not.toBeInTheDocument()
  })
})

describe('opportunity create — concurrency', () => {
  it('fires one request on double click', async () => {
    const recorder = recordRequests()
    server.use(
      http.post(url(endpoints.opportunities.create), async ({ request }) => {
        await recorder.record(request)
        await new Promise((resolve) => setTimeout(resolve, 30))
        return HttpResponse.json({ opportunityId: 42, replayed: false })
      }),
    )
    render()

    await chooseParty()
    fireEvent.change(screen.getByRole('combobox', { name: t('form.currency.label') }), { target: { value: 'TRY' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '100' } })
    const submitButton = screen.getByRole('button', { name: t('form.submit') })
    fireEvent.click(submitButton)
    fireEvent.click(submitButton)

    await screen.findByTestId('detail-page')
    expect(recorder.commands()).toHaveLength(1)
  })
})

describe('opportunity create — failure and retry', () => {
  it('shows generic failure notice and keeps form on network error', async () => {
    const recorder = recordRequests()
    server.use(
      http.post(url(endpoints.opportunities.create), async ({ request }) => {
        await recorder.record(request)
        return HttpResponse.error()
      }),
    )
    render()

    await chooseParty()
    fireEvent.change(screen.getByRole('combobox', { name: t('form.currency.label') }), { target: { value: 'TRY' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '100' } })
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))

    const notice = await screen.findByRole('alert')
    expect(notice).toHaveAttribute('data-problem', 'unavailable')
    expect(screen.getByRole('combobox', { name: partyLabel() })).toHaveValue('Acme Ltd')
    expect(recorder.commands()).toHaveLength(1)
  })

  it('reuses the same Idempotency-Key on retry after network failure', async () => {
    const recorder = recordRequests()
    let attempts = 0
    server.use(
      http.post(url(endpoints.opportunities.create), async ({ request }) => {
        await recorder.record(request)
        attempts += 1
        return attempts === 1 ? HttpResponse.error() : HttpResponse.json({ opportunityId: 42, replayed: true })
      }),
    )
    render()

    await chooseParty()
    fireEvent.change(screen.getByRole('combobox', { name: t('form.currency.label') }), { target: { value: 'TRY' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '100' } })
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))

    await screen.findByRole('alert')
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))
    await screen.findByTestId('detail-page')

    const [first, second] = recorder.commands()
    expect(second.idempotencyKey).toBe(first.idempotencyKey)
    expect(second.body).toEqual(first.body)
  })

  it('generates a new Idempotency-Key when a value changes after a definitive failure', async () => {
    const recorder = recordRequests()
    let firstKey: string | null = null
    const domainMessage = 'Currency must be a 3-letter ISO code.'
    server.use(
      http.post(url(endpoints.opportunities.create), async ({ request }) => {
        const key = request.headers.get('Idempotency-Key')
        if (firstKey === null) {
          firstKey = key
          await recorder.record(request)
          return problemResponse(400, 'validation_error', domainMessage)
        }
        await recorder.record(request)
        return HttpResponse.json({ opportunityId: 42, replayed: false })
      }),
    )
    render()

    await chooseParty()
    fireEvent.change(screen.getByRole('combobox', { name: t('form.currency.label') }), { target: { value: 'TRY' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '100' } })
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))

    // Wait for the error notice to appear and verify the domain message is visible inside it
    const alert = await screen.findByRole('alert')
    expect(alert).toHaveAttribute('data-problem', 'validation')
    within(alert).getByText(domainMessage)

    // Change a value to trigger a new key
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '200' } })
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))

    await screen.findByTestId('detail-page')
    const [first, second] = recorder.commands()
    expect(second.idempotencyKey).not.toBe(first.idempotencyKey)
  })

  it('shows 403 forbidden notice and keeps form', async () => {
    server.use(http.post(url(endpoints.opportunities.create), () => problemResponse(403, 'forbidden')))
    render()

    await chooseParty()
    fireEvent.change(screen.getByRole('combobox', { name: t('form.currency.label') }), { target: { value: 'TRY' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '100' } })
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))

    const notice = await screen.findByRole('alert')
    expect(notice).toHaveAttribute('data-problem', 'forbidden')
    expect(screen.getByRole('combobox', { name: partyLabel() })).toHaveValue('Acme Ltd')
  })
})

describe('opportunity create — form contract', () => {
  it('has no tenant field', async () => {
    render()

    expect(screen.queryByRole('textbox', { name: /tenant|kiracı/i })).not.toBeInTheDocument()
  })

  it('has no owner field', async () => {
    render()

    expect(screen.queryByRole('textbox', { name: /owner|sahip/i })).not.toBeInTheDocument()
  })

  it('has no principal field', async () => {
    render()

    expect(screen.queryByRole('textbox', { name: /principal|subject/i })).not.toBeInTheDocument()
  })
})

describe('opportunity create — customer picker', () => {
  it('is a server-backed picker: there is no free-text party id field', () => {
    render()
    expect(screen.getByRole('combobox', { name: partyLabel() })).toBeInTheDocument()
    expect(screen.queryByRole('textbox', { name: partyLabel() })).not.toBeInTheDocument()
  })

  it('asks the server for the typed text and lists exactly what comes back (no local filtering)', async () => {
    const recorder = recordRequests()
    // The server matches "zz" to Bora although the name does not contain it: a client-side filter would hide her.
    server.use(
      http.get(url(endpoints.references.parties), async ({ request }) => {
        await recorder.record(request)
        const search = new URL(request.url).searchParams.get('search')
        return HttpResponse.json(search === 'zz' ? [{ id: 1002, partyType: 'Organization', displayName: 'Bora Tekstil', email: null }] : [])
      }),
    )
    render()

    await typeInCombobox(partyLabel(), 'zz')
    expect(await screen.findByRole('option', { name: /Bora Tekstil/ })).toBeInTheDocument()
    expect(recorder.seen.some((entry) => entry.path === wirePath(endpoints.references.parties) && entry.search.includes('search=zz'))).toBe(true)
  })

  it('says so when nothing matches', async () => {
    render()
    await typeInCombobox(partyLabel(), 'nobody-by-this-name')
    expect(await screen.findByText(t('picker.empty'))).toBeInTheDocument()
  })

  it('fails closed on 403: a forbidden message, no manual fallback, and no create request', async () => {
    const recorder = recordRequests()
    server.use(
      http.get(url(endpoints.references.parties), () => problemResponse(403, 'forbidden')),
      http.post(url(endpoints.opportunities.create), async ({ request }) => {
        await recorder.record(request)
        return HttpResponse.json({ opportunityId: 1, replayed: false })
      }),
    )
    render()

    await typeInCombobox(partyLabel(), 'a')
    expect(await screen.findByText(t('picker.forbidden'))).toBeInTheDocument()
    expect(screen.queryByRole('textbox', { name: partyLabel() })).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: t('form.submit'), hidden: true }))
    expect(await screen.findByText(t('form.partyId.invalid'))).toBeInTheDocument()
    expect(recorder.commands()).toHaveLength(0)
  })

  it('clearing the choice makes the form invalid again — a stale id is never submitted', async () => {
    const recorder = recordRequests()
    server.use(
      http.post(url(endpoints.opportunities.create), async ({ request }) => {
        await recorder.record(request)
        return HttpResponse.json({ opportunityId: 1, replayed: false })
      }),
    )
    render()
    await chooseParty()
    fireEvent.click(screen.getByRole('button', { name: tr('asyncCombobox.clearSelection', undefined, 'common') }))

    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))
    expect(await screen.findByText(t('form.partyId.invalid'))).toBeInTheDocument()
    expect(recorder.commands()).toHaveLength(0)
  })
})
