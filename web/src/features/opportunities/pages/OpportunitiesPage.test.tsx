import { QueryClient } from '@tanstack/react-query'
import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, url } from '@/test/authHandlers'
import { PAGE_SIZE, problemResponse, stages, wireOpportunity } from '@/test/opportunities'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import OpportunitiesPage from './OpportunitiesPage'

const t = (key: string, options?: Record<string, unknown>) => tr(key, options, 'opportunities')

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated())
  // The list resolves customer and stage names on the side; tests that do not care get empty lookups.
  server.use(
    http.get(url(endpoints.references.parties), () => HttpResponse.json([])),
    http.get(url(endpoints.pipelines.stages(3)), () => HttpResponse.json([])),
  )
})

const render = (initialEntry = '/crm/opportunities') => {
  const result = renderRoutes([{ path: '/crm/opportunities', element: <OpportunitiesPage /> }], initialEntry, new QueryClient({ defaultOptions: { queries: { retryDelay: 0, staleTime: 0 } } }))
  return result
}

describe('opportunities list — loading and empty states', () => {
  it('shows skeleton rows while loading', async () => {
    let responseReady = false
    server.use(
      http.get(url(endpoints.opportunities.list), async () => {
        await new Promise((resolve) => {
          const checkInterval = setInterval(() => {
            if (responseReady) {
              clearInterval(checkInterval)
              resolve(undefined)
            }
          }, 5)
        })
        return HttpResponse.json([])
      }),
    )
    render()

    const table = screen.getByRole('table')
    expect(table).toHaveAttribute('aria-busy', 'true')
    responseReady = true
    expect(await screen.findByText(t('list.empty.title'))).toBeInTheDocument()
  })

  it('shows empty state when no opportunities exist', async () => {
    server.use(http.get(url(endpoints.opportunities.list), () => HttpResponse.json([])))
    const { container } = render()

    expect(await screen.findByText(t('list.empty.title'))).toBeInTheDocument()
    expect(screen.getByText(t('list.empty.description'))).toBeInTheDocument()

    // Verify the new action link to /crm/opportunities/new exists
    const newLink = container.querySelector(`a[href="/crm/opportunities/new"]`)
    expect(newLink).toBeTruthy()
  })

  it('shows filtered empty state with a clear filter button', async () => {
    let lastStatusParam: string | null = null
    server.use(
      http.get(url(endpoints.opportunities.list), ({ request }) => {
        const searchParams = new URL(request.url).searchParams
        lastStatusParam = searchParams.get('status')
        return HttpResponse.json([])
      }),
    )
    const { router } = render('/crm/opportunities?status=Won')

    expect(await screen.findByText(t('list.empty.filteredTitle'))).toBeInTheDocument()
    const clearButton = screen.getByRole('button', { name: t('list.empty.clearFilter') })
    fireEvent.click(clearButton)

    await waitFor(() => {
      expect(screen.queryByText(t('list.empty.filteredTitle'))).not.toBeInTheDocument()
    })

    expect(router.state.location.search).not.toContain('status')
    expect(lastStatusParam).toBeNull()
  })
})

describe('opportunities list — error states', () => {
  it('shows 403 forbidden state', async () => {
    server.use(http.get(url(endpoints.opportunities.list), () => problemResponse(403, 'forbidden')))
    render()

    expect(await screen.findByText(t('state.forbidden.title'))).toBeInTheDocument()
  })

  it('shows 500 error state with a retry button', async () => {
    let fail = true
    server.use(
      http.get(url(endpoints.opportunities.list), () =>
        fail ? problemResponse(500, 'about:blank', 'System.NullReferenceException') : HttpResponse.json([]),
      ),
    )
    render()

    expect(await screen.findByText(t('state.error.title'))).toBeInTheDocument()
    expect(screen.queryByText(/NullReferenceException/)).not.toBeInTheDocument()

    fail = false
    fireEvent.click(screen.getByRole('button', { name: t('state.error.retry') }))
    expect(await screen.findByText(t('list.empty.title'))).toBeInTheDocument()
  })
})

describe('opportunities list — filtering and pagination', () => {
  it('sends status filter in the request and resets to page 0', async () => {
    const rows = Array.from({ length: 26 }, (_, i) => wireOpportunity({ id: i + 1 }))
    const requestParams: Array<{ status: string | null; skip: string | null }> = []
    const { router } = render('/crm/opportunities?page=1')

    server.use(
      http.get(url(endpoints.opportunities.list), ({ request }) => {
        const searchParams = new URL(request.url).searchParams
        requestParams.push({
          status: searchParams.get('status'),
          skip: searchParams.get('skip'),
        })
        const skip = Number(searchParams.get('skip') ?? 0)
        return HttpResponse.json(skip === 25 ? rows : [])
      }),
    )

    // Start at page 1 (skip=25)
    await screen.findAllByTestId('opportunity-row')
    expect(requestParams[requestParams.length - 1].skip).toBe('25')

    // Click Open filter
    const openSegment = screen.getByRole('tab', { name: t('status.Open') })
    fireEvent.click(openSegment)

    await waitFor(() => {
      const lastReq = requestParams[requestParams.length - 1]
      expect(lastReq.status).toBe('Open')
      expect(lastReq.skip).toBe('0')
    })

    expect(router.state.location.search).not.toContain('page')
  })

  it('renders up to 25 rows and disables/enables next button based on 26th row', async () => {
    const rows = Array.from({ length: 26 }, (_, i) => wireOpportunity({ id: i + 1 }))
    server.use(http.get(url(endpoints.opportunities.list), () => HttpResponse.json(rows)))
    render()

    const opportunityRows = await screen.findAllByTestId('opportunity-row')
    expect(opportunityRows).toHaveLength(25)
    const nextButton = screen.getByRole('button', { name: t('list.next') })
    expect(nextButton).not.toBeDisabled()
  })

  it('disables next when 25 or fewer rows are returned', async () => {
    const rows = Array.from({ length: 25 }, (_, i) => wireOpportunity({ id: i + 1 }))
    server.use(http.get(url(endpoints.opportunities.list), () => HttpResponse.json(rows)))
    render()

    await screen.findAllByTestId('opportunity-row')
    const nextButton = screen.getByRole('button', { name: t('list.next') })
    expect(nextButton).toBeDisabled()
  })

  it('disables previous on page 0', async () => {
    const rows = Array.from({ length: 5 }, (_, i) => wireOpportunity({ id: i + 1 }))
    server.use(http.get(url(endpoints.opportunities.list), () => HttpResponse.json(rows)))
    render()

    await screen.findAllByTestId('opportunity-row')
    const prevButton = screen.getByRole('button', { name: t('list.previous') })
    expect(prevButton).toBeDisabled()
  })

  it('sends skip parameter on next page click', async () => {
    const rows = Array.from({ length: 26 }, (_, i) => wireOpportunity({ id: i + 1 }))
    const skipValues: string[] = []
    server.use(
      http.get(url(endpoints.opportunities.list), ({ request }) => {
        const searchParams = new URL(request.url).searchParams
        const skip = searchParams.get('skip') ?? '0'
        skipValues.push(skip)
        const skipNum = Number(skip)
        return HttpResponse.json(skipNum === 0 ? rows : [])
      }),
    )
    render()

    // Wait for the first 25 rows to render
    await screen.findAllByTestId('opportunity-row')

    const nextButton = screen.getByRole('button', { name: t('list.next') })
    expect(nextButton).not.toBeDisabled()

    fireEvent.click(nextButton)

    await waitFor(
      () => {
        expect(skipValues.length).toBeGreaterThan(1)
        expect(skipValues[skipValues.length - 1]).toBe(String(PAGE_SIZE))
      },
      { timeout: 3000 },
    )
  })

  it('takes PAGE_SIZE + 1 to check for next page', async () => {
    let lastTakeValue: string | null = null
    server.use(
      http.get(url(endpoints.opportunities.list), ({ request }) => {
        const searchParams = new URL(request.url).searchParams
        lastTakeValue = searchParams.get('take')
        return HttpResponse.json([])
      }),
    )
    render()

    await screen.findByText(t('list.empty.title'))
    expect(lastTakeValue).toBe(String(PAGE_SIZE + 1))
  })
})

describe('opportunities list — rows and rendering', () => {
  it('renders rows with status badge showing the localized status name', async () => {
    const rows = [wireOpportunity({ id: 1, status: 1 }), wireOpportunity({ id: 2, status: 2 })]
    server.use(http.get(url(endpoints.opportunities.list), () => HttpResponse.json(rows)))
    render()

    const statusCells = await screen.findAllByTestId('opportunity-row')
    const firstRow = statusCells[0]
    const secondRow = statusCells[1]

    expect(within(firstRow).getByText(t('status.Open'))).toBeInTheDocument()
    expect(within(secondRow).getByText(t('status.Won'))).toBeInTheDocument()
  })

  it('renders row links to detail page', async () => {
    const rows = [wireOpportunity({ id: 42 })]
    server.use(http.get(url(endpoints.opportunities.list), () => HttpResponse.json(rows)))
    render()

    const detailLink = await screen.findByRole('link', { name: /#42/ })
    expect(detailLink).toHaveAttribute('href', '/crm/opportunities/42')
  })

  it('has no mutation controls on rows', async () => {
    const rows = [wireOpportunity({ id: 1 })]
    server.use(http.get(url(endpoints.opportunities.list), () => HttpResponse.json(rows)))
    render()

    await screen.findByTestId('opportunity-row')
    expect(screen.queryByRole('button', { name: /edit|delete|change|update/i })).not.toBeInTheDocument()
  })
})

const wireParty = (id: number, displayName: string) => ({ id, partyType: 'organization', displayName, email: null })

describe('opportunities list — resolved columns', () => {
  it('shows the customer name and the stage name of the row’s own pipeline version', async () => {
    const rows = [
      wireOpportunity({ id: 1, partyId: 1001, pipelineDefinitionVersionId: 3, pipelineStageId: 30 }),
      wireOpportunity({ id: 2, partyId: 1002, pipelineDefinitionVersionId: 4, pipelineStageId: 30 }),
    ]
    server.use(
      http.get(url(endpoints.opportunities.list), () => HttpResponse.json(rows)),
      http.get(url(endpoints.references.parties), () => HttpResponse.json([wireParty(1001, 'Acme'), wireParty(1002, 'Globex')])),
      http.get(url(endpoints.pipelines.stages(3)), () => HttpResponse.json(stages)),
      // The same stage id in another version is a different stage.
      http.get(url(endpoints.pipelines.stages(4)), () => HttpResponse.json([{ id: 30, name: 'Discovery', sortOrder: 10, isActive: true, isEntry: true }])),
    )
    render()

    const [first, second] = await screen.findAllByTestId('opportunity-row')
    expect(await within(first).findByText('Acme')).toBeInTheDocument()
    expect(await within(first).findByText('Qualification')).toBeInTheDocument()
    expect(await within(second).findByText('Globex')).toBeInTheDocument()
    expect(await within(second).findByText('Discovery')).toBeInTheDocument()
  })

  it('falls back to the bare ids when the names cannot be read', async () => {
    server.use(
      http.get(url(endpoints.opportunities.list), () => HttpResponse.json([wireOpportunity({ id: 1, partyId: 1001, pipelineStageId: 30 })])),
      http.get(url(endpoints.references.parties), () => problemResponse(403, 'forbidden')),
      http.get(url(endpoints.pipelines.stages(3)), () => problemResponse(403, 'forbidden')),
    )
    render()

    const row = await screen.findByTestId('opportunity-row')
    expect(await within(row).findByText(t('list.partyId', { id: 1001 }))).toBeInTheDocument()
    expect(within(row).getByText(t('list.stageId', { id: 30 }))).toBeInTheDocument()
  })
})

describe('opportunities list — filters over the loaded page', () => {
  it('searches the loaded rows by customer name without asking the server again', async () => {
    const rows = [wireOpportunity({ id: 1, partyId: 1001 }), wireOpportunity({ id: 2, partyId: 1002 })]
    let listCalls = 0
    server.use(
      http.get(url(endpoints.opportunities.list), () => {
        listCalls++
        return HttpResponse.json(rows)
      }),
      http.get(url(endpoints.references.parties), () => HttpResponse.json([wireParty(1001, 'Acme'), wireParty(1002, 'Globex')])),
      http.get(url(endpoints.pipelines.stages(3)), () => HttpResponse.json(stages)),
    )
    render()

    expect(await screen.findAllByTestId('opportunity-row')).toHaveLength(2)
    await screen.findByText('Globex')
    fireEvent.change(screen.getByPlaceholderText(t('list.filters.searchPlaceholder')), { target: { value: 'glob' } })

    await waitFor(() => expect(screen.getAllByTestId('opportunity-row')).toHaveLength(1))
    expect(screen.getByText('Globex')).toBeInTheDocument()
    expect(listCalls).toBe(1)
  })

  it('totals the amounts of the visible rows in their shared currency', async () => {
    const rows = [wireOpportunity({ id: 1, estimatedAmount: 100 }), wireOpportunity({ id: 2, estimatedAmount: 50 })]
    server.use(http.get(url(endpoints.opportunities.list), () => HttpResponse.json(rows)))
    render()

    await screen.findAllByTestId('opportunity-row')
    expect(screen.getByText(/2 fırsat · .*150/)).toBeInTheDocument()
  })

  it('says so when the filters leave nothing on the page, and clears them', async () => {
    server.use(http.get(url(endpoints.opportunities.list), () => HttpResponse.json([wireOpportunity({ id: 1 })])))
    render()

    await screen.findByTestId('opportunity-row')
    fireEvent.change(screen.getByPlaceholderText(t('list.filters.searchPlaceholder')), { target: { value: 'zzz' } })

    expect(await screen.findByText(t('list.grid.noMatchTitle'))).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: t('list.filters.clear') }))
    expect(await screen.findByTestId('opportunity-row')).toBeInTheDocument()
  })
})

describe('opportunities list — table / board switcher', () => {
  const twoStages = [
    wireOpportunity({ id: 1, pipelineStageId: 30 }),
    wireOpportunity({ id: 2, pipelineStageId: 30 }),
    wireOpportunity({ id: 3, pipelineStageId: 31 }),
    wireOpportunity({ id: 4, status: 0, pipelineDefinitionVersionId: null, pipelineStageId: null }),
  ]
  const useRows = (rows: unknown[]) =>
    server.use(
      http.get(url(endpoints.opportunities.list), () => HttpResponse.json(rows)),
      http.get(url(endpoints.pipelines.stages(3)), () => HttpResponse.json(stages)),
    )

  it('opens on the table, with both views offered', async () => {
    useRows(twoStages)
    render()

    expect(await screen.findAllByTestId('opportunity-row')).toHaveLength(4)
    expect(screen.queryByTestId('opportunity-card')).not.toBeInTheDocument()
    expect(screen.getByRole('tab', { name: t('list.view.grid') })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('tab', { name: t('list.view.board') })).toHaveAttribute('aria-selected', 'false')
  })

  it('shows the same rows as cards grouped by stage, drafts without a stage first, and keeps the choice in the URL', async () => {
    useRows(twoStages)
    const { router } = render()
    await screen.findAllByTestId('opportunity-row')

    fireEvent.click(screen.getByRole('tab', { name: t('list.view.board') }))

    expect(await screen.findAllByTestId('opportunity-card')).toHaveLength(4)
    expect(screen.queryByTestId('opportunity-row')).not.toBeInTheDocument()
    expect(router.state.location.search).toBe('?view=board')
    const headings = screen.getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent)
    expect(headings).toEqual([t('list.board.noStage'), 'Qualification', 'Proposal'])
    expect(screen.getByRole('link', { name: /#3/ })).toHaveAttribute('href', '/crm/opportunities/3')
  })

  it('opens straight on the board from the URL and returns to the table', async () => {
    useRows(twoStages)
    const { router } = render('/crm/opportunities?view=board')

    expect(await screen.findAllByTestId('opportunity-card')).toHaveLength(4)
    fireEvent.click(screen.getByRole('tab', { name: t('list.view.grid') }))

    expect(await screen.findAllByTestId('opportunity-row')).toHaveLength(4)
    expect(router.state.location.search).toBe('')
  })

  it('keeps the view when the status filter or the page changes', async () => {
    useRows(twoStages)
    const { router } = render('/crm/opportunities?view=board')
    await screen.findAllByTestId('opportunity-card')

    fireEvent.click(screen.getByRole('tab', { name: t('status.Won') }))

    await waitFor(() => expect(router.state.location.search).toBe('?status=Won&view=board'))
  })

  it('leaves the density toggle to the table, since it only resizes the grid', async () => {
    useRows(twoStages)
    render()
    await screen.findAllByTestId('opportunity-row')
    const densityLabel = tr('densityToggle.ariaLabel', {}, 'common')

    expect(screen.getByRole('tablist', { name: densityLabel })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('tab', { name: t('list.view.board') }))
    await screen.findAllByTestId('opportunity-card')
    expect(screen.queryByRole('tablist', { name: densityLabel })).not.toBeInTheDocument()
  })
})
