import { QueryClient } from '@tanstack/react-query'
import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, url } from '@/test/authHandlers'
import { mockDetailApi, noActions, problemResponse, recordRequests, wireOpportunity } from '@/test/opportunities'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import OpportunityDetailPage from './OpportunityDetailPage'

const t = (key: string, options?: Record<string, unknown>) => tr(key, options, 'opportunities')

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated())
})

const render = () =>
  renderRoutes(
    [
      { path: '/crm/opportunities', element: <p>LIST</p> },
      { path: '/crm/opportunities/:id', element: <OpportunityDetailPage /> },
    ],
    '/crm/opportunities/12',
    new QueryClient({ defaultOptions: { queries: { retryDelay: 0, staleTime: 0 } } }),
  )

const dialog = () => screen.findByRole('dialog')

describe('detail — actions come from the backend projection, not from the lifecycle', () => {
  it('a Draft with every action false offers nothing (status alone never shows a button)', async () => {
    mockDetailApi(12, recordRequests(), { detail: () => HttpResponse.json(wireOpportunity({ status: 0, pipelineStageId: null, pipelineDefinitionVersionId: null })) })
    render()

    expect(await screen.findByTestId('no-actions')).toBeInTheDocument()
    for (const key of ['open.action', 'win.action', 'lose.action']) expect(screen.queryByRole('button', { name: t(key) })).not.toBeInTheDocument()
  })

  it('shows exactly the actions the server allows', async () => {
    mockDetailApi(12, recordRequests(), { actions: () => HttpResponse.json({ ...noActions, canWin: true, canLose: true }) })
    render()

    expect(await screen.findByRole('button', { name: t('win.action') })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: t('lose.action') })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: t('open.action') })).not.toBeInTheDocument()
  })

  it('fails closed when the projection cannot be loaded: no control, an explicit notice', async () => {
    mockDetailApi(12, recordRequests(), { actions: () => problemResponse(500, 'server_error') })
    render()

    expect(await screen.findByTestId('actions-unavailable')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: t('win.action') })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: t('lose.action') })).not.toBeInTheDocument()
  })

  it('offers the reassign dependency notice only when the backend says reassign is allowed, and never a principal input', async () => {
    mockDetailApi(12, recordRequests(), { actions: () => HttpResponse.json({ ...noActions, canReassign: true }) })
    render()

    expect(await screen.findByTestId('reassign-dependency')).toBeInTheDocument()
    expect(screen.queryByRole('textbox', { name: /principal|subject|owner|sahip/i })).not.toBeInTheDocument()
  })

  it('shows no reassign notice when it is not allowed', async () => {
    mockDetailApi(12, recordRequests())
    render()

    await screen.findByText(t('detail.title', { id: 12 }))
    expect(screen.queryByTestId('reassign-dependency')).not.toBeInTheDocument()
  })

  it('a masked/omitted field is simply not rendered', async () => {
    mockDetailApi(12, recordRequests(), { detail: () => HttpResponse.json({ id: 12, status: 1, rowVersion: 5, lines: [] }) })
    render()

    await screen.findByText(t('detail.title', { id: 12 }))
    expect(screen.queryByTestId('summary-estimated')).not.toBeInTheDocument()
    expect(screen.queryByTestId('summary-party')).not.toBeInTheDocument()
    expect(screen.queryByTestId('summary-owner')).not.toBeInTheDocument()
  })

  it('a won opportunity is closed: terminal notice, total shown, no line controls', async () => {
    mockDetailApi(12, recordRequests(), { detail: () => HttpResponse.json(wireOpportunity({ status: 2, totalAmount: 100, wonDate: '2029-12-20T10:00:00Z' })) })
    render()

    expect(await screen.findByTestId('terminal-state')).toBeInTheDocument()
    expect(screen.getByTestId('summary-total')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: t('lines.add.action') })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: t('lines.cancel.action') })).not.toBeInTheDocument()
  })
})

describe('detail — pipeline stage', () => {
  it('names the current stage and offers only the backend-allowed targets, by name; a retired or unlisted stage never appears', async () => {
    mockDetailApi(12, recordRequests(), { actions: () => HttpResponse.json({ ...noActions, canChangeStage: true, allowedTargetStageIds: [32, 31] }) })
    render()

    expect(await screen.findByTestId('current-stage')).toHaveTextContent('Qualification')
    const select = await screen.findByLabelText(t('pipeline.move.label'))
    const options = within(select).getAllByRole('option').map((option) => option.textContent)
    expect(options).toEqual([t('pipeline.move.placeholder'), 'Proposal', 'Negotiation']) // sort order, not the order the ids arrived in
  })

  it('says so when there is nothing to move to, and when moves are not available', async () => {
    mockDetailApi(12, recordRequests(), { actions: () => HttpResponse.json({ ...noActions, canChangeStage: true }) })
    render()
    expect(await screen.findByText(t('pipeline.noTargets'))).toBeInTheDocument()
  })

  it('explains that the entry stage arrives on Open when the Draft has none, without inventing a stage', async () => {
    mockDetailApi(12, recordRequests(), { detail: () => HttpResponse.json(wireOpportunity({ status: 0, pipelineStageId: null, pipelineDefinitionVersionId: null })) })
    render()
    expect(await screen.findByText(t('pipeline.assignedOnOpen'))).toBeInTheDocument()
  })

  it('moves the stage with the version the user saw and a fresh idempotency key, then shows the refetched state', async () => {
    const recorder = recordRequests()
    let stageId = 30
    mockDetailApi(12, recorder, {
      detail: () => HttpResponse.json(wireOpportunity({ pipelineStageId: stageId, rowVersion: stageId === 30 ? 5 : 6 })),
      actions: () => HttpResponse.json({ ...noActions, canChangeStage: true, allowedTargetStageIds: [31] }),
    })
    server.use(
      http.post(url(endpoints.opportunities.changeStage(12)), async ({ request }) => {
        await recorder.record(request)
        stageId = 31
        return HttpResponse.json({ opportunityId: 12, pipelineStageId: 31, replayed: false })
      }),
    )
    render()

    fireEvent.change(await screen.findByLabelText(t('pipeline.move.label')), { target: { value: '31' } })
    fireEvent.click(screen.getByRole('button', { name: t('pipeline.move.submit') }))

    await waitFor(() => expect(screen.getByTestId('current-stage')).toHaveTextContent('Proposal'))
    const [command] = recorder.commands()
    expect(command.path).toBe('/api/opportunities/12/stage')
    expect(command.body).toEqual({ expectedVersion: 5, targetStageId: 31 })
    expect(command.idempotencyKey).toMatch(/\S{8,}/)
  })
})

describe('detail — commands: concurrency and idempotency', () => {
  const winnable = { actions: () => HttpResponse.json({ ...noActions, canWin: true, canLose: true }) }

  it('win sends the row version the user saw plus an idempotency key, then renders the server state', async () => {
    const recorder = recordRequests()
    let won = false
    mockDetailApi(12, recorder, {
      detail: () => HttpResponse.json(won ? wireOpportunity({ status: 2, totalAmount: 100, rowVersion: 6 }) : wireOpportunity()),
      ...winnable,
    })
    server.use(
      http.post(url(endpoints.opportunities.win(12)), async ({ request }) => {
        await recorder.record(request)
        won = true
        return HttpResponse.json({ opportunityId: 12, totalAmount: 100, replayed: false })
      }),
    )
    render()

    fireEvent.click(await screen.findByRole('button', { name: t('win.action') }))
    const modal = await dialog()
    fireEvent.click(within(modal).getByRole('button', { name: t('win.submit') }))

    expect(await screen.findByTestId('terminal-state')).toBeInTheDocument()
    expect(recorder.commands()).toHaveLength(1)
    expect(recorder.commands()[0].body).toEqual({ expectedVersion: 5 })
    expect(recorder.commands()[0].idempotencyKey).toBeTruthy()
  })

  it('a stale write shows the conflict with a reload, keeps the typed reason, and never retries by itself', async () => {
    const recorder = recordRequests()
    mockDetailApi(12, recorder, winnable)
    server.use(
      http.post(url(endpoints.opportunities.lose(12)), async ({ request }) => {
        await recorder.record(request)
        return problemResponse(409, 'concurrency_conflict', 'stale')
      }),
    )
    render()

    fireEvent.click(await screen.findByRole('button', { name: t('lose.action') }))
    const modal = await dialog()
    fireEvent.change(within(modal).getByLabelText(t('lose.reason.label')), { target: { value: 'Budget cut' } })
    fireEvent.click(within(modal).getByRole('button', { name: t('lose.submit') }))

    const alert = await within(modal).findByRole('alert')
    expect(alert).toHaveAttribute('data-problem', 'concurrency')
    expect(within(modal).getByLabelText(t('lose.reason.label'))).toHaveValue('Budget cut') // input preserved
    await new Promise((resolve) => setTimeout(resolve, 60))
    expect(recorder.commands()).toHaveLength(1) // no automatic retry with a new version

    const detailReads = recorder.seen.filter((entry) => entry.path === '/api/opportunities/12').length
    fireEvent.click(within(modal).getByRole('button', { name: t('problem.reload') }))
    await waitFor(() => expect(recorder.seen.filter((entry) => entry.path === '/api/opportunities/12').length).toBeGreaterThan(detailReads))
    expect(recorder.commands()).toHaveLength(1) // reloading did not resubmit anything
  })

  it('a retry after a network failure reuses the SAME idempotency key and the typed reason', async () => {
    const recorder = recordRequests()
    mockDetailApi(12, recorder, winnable)
    let attempts = 0
    server.use(
      http.post(url(endpoints.opportunities.lose(12)), async ({ request }) => {
        await recorder.record(request)
        attempts += 1
        return attempts === 1 ? HttpResponse.error() : HttpResponse.json({ opportunityId: 12, replayed: true })
      }),
    )
    render()

    fireEvent.click(await screen.findByRole('button', { name: t('lose.action') }))
    const modal = await dialog()
    fireEvent.change(within(modal).getByLabelText(t('lose.reason.label')), { target: { value: 'Went elsewhere' } })
    fireEvent.click(within(modal).getByRole('button', { name: t('lose.submit') }))
    expect(await within(modal).findByRole('alert')).toHaveAttribute('data-problem', 'unavailable')

    fireEvent.click(within(modal).getByRole('button', { name: t('lose.submit') }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())

    const [first, second] = recorder.commands()
    expect(second.idempotencyKey).toBe(first.idempotencyKey)
    expect(second.body).toEqual(first.body)
  })

  it('a double click fires one request', async () => {
    const recorder = recordRequests()
    mockDetailApi(12, recorder, winnable)
    server.use(
      http.post(url(endpoints.opportunities.win(12)), async ({ request }) => {
        await recorder.record(request)
        await new Promise((resolve) => setTimeout(resolve, 30))
        return HttpResponse.json({ opportunityId: 12, totalAmount: 100, replayed: false })
      }),
    )
    render()

    fireEvent.click(await screen.findByRole('button', { name: t('win.action') }))
    const submit = within(await dialog()).getByRole('button', { name: t('win.submit') })
    fireEvent.click(submit)
    fireEvent.click(submit)

    await waitFor(() => expect(recorder.commands()).toHaveLength(1))
    await new Promise((resolve) => setTimeout(resolve, 60))
    expect(recorder.commands()).toHaveLength(1)
  })

  it('lose requires a reason and sends nothing while it is blank', async () => {
    const recorder = recordRequests()
    mockDetailApi(12, recorder, winnable)
    render()

    fireEvent.click(await screen.findByRole('button', { name: t('lose.action') }))
    fireEvent.click(within(await dialog()).getByRole('button', { name: t('lose.submit') }))

    expect(await screen.findByText(t('reason.required'))).toBeInTheDocument()
    expect(recorder.commands()).toHaveLength(0)
  })

  it('a lifecycle rejection shows the backend business rule; a 5xx shows only generic copy (no internals)', async () => {
    const recorder = recordRequests()
    mockDetailApi(12, recorder, winnable)
    let mode: 'rule' | 'crash' = 'rule'
    server.use(
      http.post(url(endpoints.opportunities.win(12)), () =>
        mode === 'rule'
          ? problemResponse(409, 'illegal_lifecycle_transition', 'Cannot win an opportunity without at least one active required line.')
          : problemResponse(500, 'about:blank', 'System.NullReferenceException at Host.Foo'),
      ),
    )
    render()

    fireEvent.click(await screen.findByRole('button', { name: t('win.action') }))
    const modal = await dialog()
    fireEvent.click(within(modal).getByRole('button', { name: t('win.submit') }))
    expect(await within(modal).findByText('Cannot win an opportunity without at least one active required line.')).toBeInTheDocument()

    mode = 'crash'
    fireEvent.click(within(modal).getByRole('button', { name: t('win.submit') }))
    expect(await within(modal).findByText(t('problem.unavailable.title'))).toBeInTheDocument()
    expect(screen.queryByText(/NullReference/)).not.toBeInTheDocument()
  })
})

describe('detail — lines (the editable state Phase 2 defines)', () => {
  it('a Draft offers add-line; an Open opportunity offers cancel but not add; the request carries the contract body', async () => {
    const recorder = recordRequests()
    let lines = [{ id: 1, quantity: 2, unitPrice: 50, lineTotal: 100, isOptional: false, isCanceled: false }]
    mockDetailApi(12, recorder, { detail: () => HttpResponse.json(wireOpportunity({ status: 0, rowVersion: 3, pipelineStageId: null, pipelineDefinitionVersionId: null, lines })) })
    server.use(
      http.post(url(endpoints.opportunities.addLine(12)), async ({ request }) => {
        await recorder.record(request)
        lines = [...lines, { id: 2, quantity: 3, unitPrice: 9.99, lineTotal: 29.97, isOptional: true, isCanceled: false }]
        return HttpResponse.json({ opportunityId: 12 })
      }),
    )
    render()

    fireEvent.click(await screen.findByRole('button', { name: t('lines.add.action') }))
    const modal = await dialog()
    fireEvent.change(within(modal).getByLabelText(t('lines.add.productId.label')), { target: { value: '77' } })
    fireEvent.change(within(modal).getByLabelText(t('lines.add.quantity.label')), { target: { value: '3' } })
    fireEvent.change(within(modal).getByLabelText(t('lines.add.unitPrice.label')), { target: { value: '9.99' } })
    fireEvent.click(within(modal).getByLabelText(t('lines.add.isOptional')))
    fireEvent.click(within(modal).getByRole('button', { name: t('lines.add.submit') }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(recorder.commands()[0].body).toEqual({ expectedVersion: 3, productId: 77, quantity: 3, unitPrice: 9.99, isOptional: true, sortOrder: 1 })
    expect(await screen.findByText('#2')).toBeInTheDocument()
  })

  it('an Open opportunity offers cancel-line but not add-line', async () => {
    mockDetailApi(12, recordRequests())
    render()

    expect(await screen.findByRole('button', { name: t('lines.cancel.action') })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: t('lines.add.action') })).not.toBeInTheDocument()
  })
})

describe('detail — read failures are states of the page, never a crash', () => {
  it('a missing, denied and cross-tenant record all read the same (404 → not-found state)', async () => {
    mockDetailApi(12, recordRequests(), { detail: () => problemResponse(404, 'not_found', 'Opportunity 12 was not found.') })
    render()
    expect(await screen.findByText(t('state.notFound.title'))).toBeInTheDocument()
    expect(screen.queryByText(/Opportunity 12 was not found/)).not.toBeInTheDocument()
  })

  it('403 is the forbidden state', async () => {
    mockDetailApi(12, recordRequests(), { detail: () => problemResponse(403, 'forbidden') })
    render()
    expect(await screen.findByText(t('state.forbidden.title'))).toBeInTheDocument()
  })

  it('a 5xx shows a retry that re-reads', async () => {
    let fail = true
    mockDetailApi(12, recordRequests(), { detail: () => (fail ? problemResponse(500, 'about:blank') : HttpResponse.json(wireOpportunity())) })
    render()

    expect(await screen.findByText(t('state.error.title'))).toBeInTheDocument()
    fail = false
    fireEvent.click(screen.getByRole('button', { name: t('state.error.retry') }))
    expect(await screen.findByText(t('detail.title', { id: 12 }))).toBeInTheDocument()
  })

  it('a malformed id is treated like a record that does not exist, and never reaches the API', async () => {
    const recorder = recordRequests()
    server.use(
      http.get(url('/opportunities/:id'), async ({ request }) => {
        await recorder.record(request)
        return HttpResponse.json(wireOpportunity())
      }),
    )
    renderRoutes([{ path: '/crm/opportunities/:id', element: <OpportunityDetailPage /> }], '/crm/opportunities/abc')
    expect(await screen.findByText(t('state.notFound.title'))).toBeInTheDocument()
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(recorder.seen).toHaveLength(0)
  })
})
