import { QueryClient } from '@tanstack/react-query'
import { fireEvent, screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, url } from '@/test/authHandlers'
import { problemResponse, recordRequests } from '@/test/opportunities'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import OpportunityNewPage from './OpportunityNewPage'

const t = (key: string, options?: Record<string, unknown>) => tr(key, options, 'opportunities')

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated())
})

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

  it('shows currency error for invalid code', async () => {
    server.use(http.post(url(endpoints.opportunities.create), () => HttpResponse.json({ opportunityId: 1, replayed: false })))
    render()

    fireEvent.change(screen.getByRole('textbox', { name: t('form.partyId.label') }), { target: { value: '1001' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.currency.label') }), { target: { value: 'TR' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '100' } })
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))

    expect(await screen.findByText(t('form.currency.invalid'))).toBeInTheDocument()
  })

  it('shows decimals error for amounts with 3+ decimals', async () => {
    server.use(http.post(url(endpoints.opportunities.create), () => HttpResponse.json({ opportunityId: 1, replayed: false })))
    render()

    fireEvent.change(screen.getByRole('textbox', { name: t('form.partyId.label') }), { target: { value: '1001' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.currency.label') }), { target: { value: 'TRY' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '100.123' } })
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))

    expect(await screen.findByText(t('form.estimatedAmount.decimals'))).toBeInTheDocument()
  })

  it('keeps typed values when validation fails', async () => {
    server.use(http.post(url(endpoints.opportunities.create), () => HttpResponse.json({ opportunityId: 1, replayed: false })))
    render()

    const partyInput = screen.getByRole('textbox', { name: t('form.partyId.label') })
    const currencyInput = screen.getByRole('textbox', { name: t('form.currency.label') })
    const amountInput = screen.getByRole('textbox', { name: t('form.estimatedAmount.label') })

    fireEvent.change(partyInput, { target: { value: '999' } })
    fireEvent.change(currencyInput, { target: { value: 'INVALID' } })
    fireEvent.change(amountInput, { target: { value: '250.50' } })

    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))
    await screen.findByText(t('form.currency.invalid'))

    expect(partyInput).toHaveValue('999')
    expect(amountInput).toHaveValue('250.50')
    expect(currencyInput).toHaveValue('INVALID')
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

    fireEvent.change(screen.getByRole('textbox', { name: t('form.partyId.label') }), { target: { value: '1001' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.currency.label') }), { target: { value: 'try' } })
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

    fireEvent.change(screen.getByRole('textbox', { name: t('form.partyId.label') }), { target: { value: '1001' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.currency.label') }), { target: { value: 'EUR' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '500' } })
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))

    await screen.findByTestId('detail-page')
    expect(recorder.commands()[0].idempotencyKey).toMatch(/\S{8,}/)
  })

  it('navigates to detail route with created opportunity ID', async () => {
    server.use(http.post(url(endpoints.opportunities.create), () => HttpResponse.json({ opportunityId: 42, replayed: false })))
    render()

    fireEvent.change(screen.getByRole('textbox', { name: t('form.partyId.label') }), { target: { value: '1001' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.currency.label') }), { target: { value: 'TRY' } })
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

    fireEvent.change(screen.getByRole('textbox', { name: t('form.partyId.label') }), { target: { value: '1001' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.currency.label') }), { target: { value: 'TRY' } })
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

    fireEvent.change(screen.getByRole('textbox', { name: t('form.partyId.label') }), { target: { value: '1001' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.currency.label') }), { target: { value: 'TRY' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '100' } })
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))

    const notice = await screen.findByRole('alert')
    expect(notice).toHaveAttribute('data-problem', 'unavailable')
    expect(screen.getByRole('textbox', { name: t('form.partyId.label') })).toHaveValue('1001')
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

    fireEvent.change(screen.getByRole('textbox', { name: t('form.partyId.label') }), { target: { value: '1001' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.currency.label') }), { target: { value: 'TRY' } })
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
    server.use(
      http.post(url(endpoints.opportunities.create), async ({ request }) => {
        const key = request.headers.get('Idempotency-Key')
        if (firstKey === null) {
          firstKey = key
          await recorder.record(request)
          return problemResponse(400, 'validation_error', 'Invalid request')
        }
        await recorder.record(request)
        return HttpResponse.json({ opportunityId: 42, replayed: false })
      }),
    )
    render()

    fireEvent.change(screen.getByRole('textbox', { name: t('form.partyId.label') }), { target: { value: '1001' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.currency.label') }), { target: { value: 'TRY' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '100' } })
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))

    // Wait for the error notice to appear
    await screen.findByRole('alert')
    expect(screen.getByRole('alert')).toHaveAttribute('data-problem', 'validation')

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

    fireEvent.change(screen.getByRole('textbox', { name: t('form.partyId.label') }), { target: { value: '1001' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.currency.label') }), { target: { value: 'TRY' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('form.estimatedAmount.label') }), { target: { value: '100' } })
    fireEvent.click(screen.getByRole('button', { name: t('form.submit') }))

    const notice = await screen.findByRole('alert')
    expect(notice).toHaveAttribute('data-problem', 'forbidden')
    expect(screen.getByRole('textbox', { name: t('form.partyId.label') })).toHaveValue('1001')
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
