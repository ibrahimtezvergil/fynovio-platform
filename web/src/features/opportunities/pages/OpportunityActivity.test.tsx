import { QueryClient } from '@tanstack/react-query'
import { fireEvent, screen, within } from '@testing-library/react'
import { HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useSessionStore } from '@/lib/auth'
import { authenticated } from '@/test/authHandlers'
import { mockDetailApi, mockParties, problemResponse, recordRequests, wireOpportunity } from '@/test/opportunities'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import { formatMoney } from '../lib/format'
import OpportunityDetailPage from './OpportunityDetailPage'

const t = (key: string, options?: Record<string, unknown>) => tr(key, options, 'opportunities')

const entry = (kind: string, overrides: Record<string, unknown> = {}) => ({
  eventId: `${kind}-${Math.random()}`, kind, occurredAt: '2026-10-01T10:00:00Z', version: 1, ...overrides,
})

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated())
  mockParties()
})

const render = () =>
  renderRoutes(
    [{ path: '/crm/opportunities/:id', element: <OpportunityDetailPage /> }],
    '/crm/opportunities/12',
    new QueryClient({ defaultOptions: { queries: { retry: false, retryDelay: 0, staleTime: 0 } } }),
  )

describe('opportunity detail — activity timeline', () => {
  it('describes each fact with the labels the server resolved, in the order it sent them', async () => {
    mockDetailApi(12, recordRequests(), {
      activity: () => HttpResponse.json([
        entry('lost', { lostReason: 'Price' }),
        entry('custom_fields_changed', { changedFields: ['Region', 'Tier'] }),
        entry('reassigned', { principalName: 'Ada Yılmaz' }),
        entry('stage_changed', { fromStageName: 'Qualified', stageName: 'Proposal' }),
        entry('won', { amount: 1200, currency: 'EUR' }),
        entry('created'),
        entry('something_new'),
      ]),
    })
    render()

    const list = await screen.findByRole('list', { name: t('activity.title') })
    expect(within(list).getAllByRole('listitem').map((item) => item.querySelector('span')?.textContent)).toEqual([
      t('activity.kinds.lostWithReason', { reason: 'Price' }),
      t('activity.kinds.customFieldsChangedNamed', { fields: 'Region, Tier' }),
      t('activity.kinds.reassignedTo', { name: 'Ada Yılmaz' }),
      t('activity.kinds.stageChangedFromTo', { from: 'Qualified', to: 'Proposal' }),
      t('activity.kinds.wonWithAmount', { amount: formatMoney(1200, 'EUR') }),
      t('activity.kinds.created'),
      t('activity.kinds.unknown'),
    ])
  })

  it('says so when nothing has been recorded yet', async () => {
    mockDetailApi(12, recordRequests())
    render()

    expect(await screen.findByText(t('activity.empty'))).toBeInTheDocument()
  })

  it('keeps the page usable when the timeline cannot be loaded, and retries on request', async () => {
    let fail = true
    mockDetailApi(12, recordRequests(), {
      activity: () => (fail ? problemResponse(500, 'server_error') : HttpResponse.json([entry('archived')])),
    })
    render()

    expect(await screen.findByText(t('activity.error'))).toBeInTheDocument()
    expect(screen.getByText(t('detail.eyebrow'))).toBeInTheDocument()

    fail = false
    fireEvent.click(screen.getByRole('button', { name: t('activity.retry') }))
    expect(await screen.findByText(t('activity.kinds.archived'))).toBeInTheDocument()
  })
})

describe('opportunity detail — activity catch-up', () => {
  it('looks again shortly after the record changes, without waiting for the next poll', async () => {
    let version = 1
    let activityCalls = 0
    mockDetailApi(12, recordRequests(), {
      detail: () => HttpResponse.json(wireOpportunity({ id: 12, rowVersion: version })),
      activity: () => {
        activityCalls += 1
        return HttpResponse.json(version === 1 ? [] : [entry('archived')])
      },
    })
    const { queryClient } = render()
    expect(await screen.findByText(t('activity.empty'))).toBeInTheDocument()
    const before = activityCalls

    version = 2
    await queryClient.invalidateQueries({ queryKey: ['opportunities'], predicate: (query) => query.queryKey.includes('detail') })

    expect(await screen.findByText(t('activity.kinds.archived'), undefined, { timeout: 5_000 })).toBeInTheDocument()
    expect(activityCalls).toBeGreaterThan(before)
  }, 10_000)
})
