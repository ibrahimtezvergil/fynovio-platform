import { QueryClient } from '@tanstack/react-query'
import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, url } from '@/test/authHandlers'
import { closingStages, noActions, problemResponse, recordRequests, stages, wireOpportunity } from '@/test/opportunities'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import OpportunitiesPage from './OpportunitiesPage'

const t = (key: string, options?: Record<string, unknown>) => tr(key, options, 'opportunities')

const openStages = stages.filter((stage) => stage.isActive)
const pipeline = [...openStages, ...closingStages]

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated())
  server.use(http.get(url(endpoints.references.parties), () => HttpResponse.json([])))
})

const render = (search = '?view=board') =>
  renderRoutes([{ path: '/crm/opportunities', element: <OpportunitiesPage /> }], `/crm/opportunities${search}`, new QueryClient({ defaultOptions: { queries: { retryDelay: 0, staleTime: 0 } } }))

/** One open card in `Qualification`; the server lets it move to `Proposal`, win, but not lose. */
function mockBoard(rows: unknown[] = [wireOpportunity({ id: 12, pipelineStageId: 30 })], actions: Record<string, unknown> = {}) {
  const recorder = recordRequests()
  server.use(
    http.get(url(endpoints.opportunities.list), () => HttpResponse.json(rows)),
    http.get(url(endpoints.pipelines.stages(3)), () => HttpResponse.json(pipeline)),
    http.get(url(endpoints.pipelines.defaultStages), () => HttpResponse.json(pipeline)),
    http.get(url(endpoints.opportunities.actions(12)), async ({ request }) => {
      await recorder.record(request)
      return HttpResponse.json({ ...noActions, canChangeStage: true, allowedTargetStageIds: [31], canWin: true, canLose: false, ...actions })
    }),
  )
  return recorder
}

const grip = () => screen.findByRole('button', { name: t('list.board.grabLabel', { id: 12 }) })

describe('opportunities board — moving a card', () => {
  it('offers a move only to the columns the server allows, then changes the stage with the row version the card carried', async () => {
    const recorder = mockBoard()
    server.use(
      http.post(url(endpoints.opportunities.changeStage(12)), async ({ request }) => {
        await recorder.record(request)
        return HttpResponse.json({ opportunityId: 12, pipelineStageId: 31, replayed: false })
      }),
    )
    render()

    fireEvent.click(await grip())

    // Proposal is a listed target and Won is winnable; Negotiation is neither, Lost is not loseable.
    const proposal = await screen.findByRole('region', { name: t('list.board.columnLabel', { stage: 'Proposal', count: 0 }) })
    expect(within(proposal).getByRole('button', { name: t('list.board.moveHere') })).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: t('list.board.columnLabel', { stage: 'Won', count: 0 }) })).getByRole('button', { name: t('list.board.moveHere') })).toBeInTheDocument()
    for (const stage of ['Negotiation', 'Lost']) {
      const column = screen.getByRole('region', { name: t('list.board.columnLabel', { stage, count: 0 }) })
      expect(within(column).queryByRole('button', { name: t('list.board.moveHere') })).not.toBeInTheDocument()
      expect(within(column).getByText(t('list.board.columnDenied'))).toBeInTheDocument()
    }

    fireEvent.click(within(proposal).getByRole('button', { name: t('list.board.moveHere') }))

    await waitFor(() => expect(recorder.commands()).toHaveLength(1))
    const [command] = recorder.commands()
    expect(command.path).toBe('/api/opportunities/12/stage')
    expect(command.body).toEqual({ expectedVersion: 5, targetStageId: 31 })
    expect(command.idempotencyKey).toMatch(/\S{8,}/)
  })

  it('lands on a Won column through the win dialog and never through a stage change', async () => {
    const recorder = mockBoard()
    server.use(
      http.post(url(endpoints.opportunities.win(12)), async ({ request }) => {
        await recorder.record(request)
        return HttpResponse.json({ opportunityId: 12, totalAmount: 100, replayed: false })
      }),
    )
    render()

    fireEvent.click(await grip())
    const won = await screen.findByRole('region', { name: t('list.board.columnLabel', { stage: 'Won', count: 0 }) })
    fireEvent.click(await within(won).findByRole('button', { name: t('list.board.moveHere') }))

    const dialog = await screen.findByRole('dialog')
    expect(recorder.commands()).toHaveLength(0)
    fireEvent.click(within(dialog).getByRole('button', { name: t('win.submit') }))

    await waitFor(() => expect(recorder.commands()).toHaveLength(1))
    expect(recorder.commands()[0].path).toBe('/api/opportunities/12/win')
    expect(recorder.commands()[0].body).toEqual({ expectedVersion: 5 })
  })

  it('leaves the card where it is when the win dialog is dismissed', async () => {
    const recorder = mockBoard()
    render()

    fireEvent.click(await grip())
    const won = await screen.findByRole('region', { name: t('list.board.columnLabel', { stage: 'Won', count: 0 }) })
    fireEvent.click(await within(won).findByRole('button', { name: t('list.board.moveHere') }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.click(within(dialog).getByRole('button', { name: t('common.cancel') }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(recorder.commands()).toHaveLength(0)
    expect(within(screen.getByRole('region', { name: t('list.board.columnLabel', { stage: 'Qualification', count: 1 }) })).getByTestId('opportunity-card')).toBeInTheDocument()
  })

  it('sends nothing when the move is cancelled with Escape', async () => {
    const recorder = mockBoard()
    render()

    fireEvent.click(await grip())
    expect(await screen.findByTestId('board-carry-banner')).toBeInTheDocument()
    fireEvent.keyDown(window, { key: 'Escape' })

    await waitFor(() => expect(screen.queryByTestId('board-carry-banner')).not.toBeInTheDocument())
    expect(recorder.commands()).toHaveLength(0)
  })

  it('previews targets with the arrow keys, skipping columns the card cannot enter, and drops on Enter', async () => {
    const recorder = mockBoard()
    server.use(
      http.post(url(endpoints.opportunities.changeStage(12)), async ({ request }) => {
        await recorder.record(request)
        return HttpResponse.json({ opportunityId: 12, pipelineStageId: 31, replayed: false })
      }),
    )
    render()

    const handle = await grip()
    fireEvent.click(handle)
    await screen.findByRole('region', { name: t('list.board.columnLabel', { stage: 'Proposal', count: 0 }) })
    fireEvent.keyDown(handle, { key: 'ArrowRight' })
    expect(screen.getByText(t('list.board.a11y.target', { stage: 'Proposal' }))).toBeInTheDocument()
    expect(recorder.commands()).toHaveLength(0) // an arrow only previews

    fireEvent.click(handle) // Enter/Space on the grip is a click
    await waitFor(() => expect(recorder.commands()).toHaveLength(1))
    expect(recorder.commands()[0].body).toEqual({ expectedVersion: 5, targetStageId: 31 })
  })

  it('shows the problem and keeps the card in place when the row version is stale', async () => {
    mockBoard()
    server.use(http.post(url(endpoints.opportunities.changeStage(12)), () => problemResponse(409, 'concurrency_conflict')))
    render()

    fireEvent.click(await grip())
    const proposal = await screen.findByRole('region', { name: t('list.board.columnLabel', { stage: 'Proposal', count: 0 }) })
    fireEvent.click(within(proposal).getByRole('button', { name: t('list.board.moveHere') }))

    expect(await screen.findByRole('alert')).toHaveAttribute('data-problem', 'concurrency')
    expect(within(screen.getByRole('region', { name: t('list.board.columnLabel', { stage: 'Qualification', count: 1 }) })).getByTestId('opportunity-card')).toBeInTheDocument()
  })

  it('offers no grip on drafts, closed cards or the archive', async () => {
    mockBoard([
      wireOpportunity({ id: 12, pipelineStageId: 30 }),
      wireOpportunity({ id: 13, status: 0, pipelineDefinitionVersionId: null, pipelineStageId: null }),
      wireOpportunity({ id: 14, status: 2, pipelineStageId: 34 }),
    ])
    render()

    await screen.findAllByTestId('opportunity-card')
    expect(screen.getAllByRole('button', { name: /^#\d+/ })).toHaveLength(1)
    expect(screen.queryByRole('button', { name: t('list.board.grabLabel', { id: 13 }) })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: t('list.board.grabLabel', { id: 14 }) })).not.toBeInTheDocument()
  })

  it('offers no grip in the archive view', async () => {
    mockBoard()
    render('?view=board&archive=1')

    await screen.findAllByTestId('opportunity-card')
    expect(screen.queryByRole('button', { name: t('list.board.grabLabel', { id: 12 }) })).not.toBeInTheDocument()
  })
})
