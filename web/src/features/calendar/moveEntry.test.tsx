import { QueryClientProvider } from '@tanstack/react-query'
import { act, renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { queryClient } from '@/api/queryClient'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, problem, url } from '@/test/authHandlers'
import { accessibleLink, wireAllDayEntry, wireEntry } from '@/test/calendar'
import { resetSession } from '@/test/session'
import { useCalendarEntries, useMoveCalendarEntry } from './api'

const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
const RANGE = { from: '2026-08-31T21:00:00.000Z', to: '2026-10-12T21:00:00.000Z' }

interface Put {
  id: number
  body: Record<string, unknown>
  key: string | null
}

/** The entries list plus the drag hook, sharing one cache like the page does. */
const setup = () =>
  renderHook(() => ({ list: useCalendarEntries(RANGE), move: useMoveCalendarEntry() }), { wrapper })

type Store = { entries: Record<string, unknown>[] }

/** The contract: the server returns instants normalised to UTC (`+00:00`). */
const utc = (instant: unknown) => (typeof instant === 'string' ? new Date(instant).toISOString().replace('.000Z', '+00:00') : instant)

/** What a real server does with an accepted replace: store the body (instants in UTC), bump the version, answer with it. */
function accept(store: Store, put: Put) {
  const { expectedVersion: _expected, ...body } = put.body
  const fields = { ...body, startAt: utc(body.startAt), endAt: utc(body.endAt) }
  const current = store.entries.find((entry) => entry.id === put.id) as { rowVersion: number }
  const rowVersion = current.rowVersion + 1
  store.entries = store.entries.map((entry) => (entry.id === put.id ? { ...entry, ...fields, rowVersion } : entry))
  return HttpResponse.json({ id: put.id, rowVersion, replayed: false })
}

/**
 * A stateful fake of the API: GET serves `store`, every PUT is recorded, and `respond` (default: accept it) decides the
 * answer. `getDelay` slows the list down so a rollback can be told apart from the refetch that follows it.
 */
function mockServer(store: Store, respond: (put: Put) => Response | Promise<Response> = (put) => accept(store, put), getDelay = 0) {
  const puts: Put[] = []
  let lists = 0
  server.use(
    http.get(url(endpoints.calendar.entries), async () => {
      lists++
      if (getDelay > 0 && lists > 1) await new Promise((resolve) => setTimeout(resolve, getDelay))
      return HttpResponse.json({ items: store.entries })
    }),
    http.put(url(`${endpoints.calendar.entries}/:id`), async ({ request, params }) => {
      const put: Put = { id: Number(params.id), body: (await request.json()) as Record<string, unknown>, key: request.headers.get('Idempotency-Key') }
      puts.push(put)
      return respond(put)
    }),
  )
  return { puts, lists: () => lists }
}

/** The cache itself, synchronously — observers are notified asynchronously, so the hook’s `data` lags a tick. */
const cached = () => queryClient.getQueriesData<Array<{ id: number; startAt: string | null; rowVersion: number }>>({ queryKey: ['calendar'] }).flatMap(([, entries]) => entries ?? [])[0]

const at = (isoUtc: string) => new Date(isoUtc)

beforeEach(() => {
  vi.stubEnv('TZ', 'Europe/Istanbul')
  resetSession()
  queryClient.clear()
  useSessionStore.getState().applyAuthResult(authenticated({ activeTenant: { tenantId: 1 } }))
})
afterEach(() => vi.unstubAllEnvs())

describe('drag / resize → PUT with expectedVersion', () => {
  it('sends the FULL entry with the new offset-bearing times, expectedVersion from the cache, and a fresh idempotency key', async () => {
    const store = { entries: [wireEntry({ link: accessibleLink() })] }
    const { puts } = mockServer(store)
    const { result } = setup()
    await waitFor(() => expect(result.current.list.data).toHaveLength(1))

    await act(async () => {
      await result.current.move(42, { start: at('2026-09-22T07:00:00Z'), end: at('2026-09-22T08:30:00Z'), allDay: false })
    })

    expect(puts).toHaveLength(1)
    expect(puts[0].body).toEqual({
      title: 'Call with vendor',
      notes: 'Bring the price list',
      color: '#3b82f6',
      allDay: false,
      startAt: '2026-09-22T10:00:00+03:00',
      endAt: '2026-09-22T11:30:00+03:00',
      startDate: null,
      endDate: null,
      link: { boundedContext: 'crm', entityType: 'opportunity', id: 17 }, // bare ref, so the link survives the replace
      expectedVersion: 3,
    })
    expect(puts[0].key).toMatch(/\S+/)
  })

  it('an all-day entry keeps its dates, with an exclusive end', async () => {
    const store = { entries: [wireAllDayEntry()] }
    const { puts } = mockServer(store)
    const { result } = setup()
    await waitFor(() => expect(result.current.list.data).toHaveLength(1))

    await act(async () => {
      await result.current.move(42, { start: new Date(2026, 8, 24), end: new Date(2026, 8, 26), allDay: true })
    })
    expect(puts[0].body).toMatchObject({ allDay: true, startAt: null, endAt: null, startDate: '2026-09-24', endDate: '2026-09-26' })
  })

  it('writes the new time optimistically, before the server answers, then adopts the returned rowVersion', async () => {
    const store = { entries: [wireEntry()] }
    let release: () => void = () => {}
    const gate = new Promise<void>((resolve) => (release = resolve))
    mockServer(store, async (put) => {
      await gate
      return accept(store, put)
    })
    const { result } = setup()
    await waitFor(() => expect(result.current.list.data).toHaveLength(1))

    let moved!: Promise<void>
    act(() => {
      moved = result.current.move(42, { start: at('2026-09-22T07:00:00Z'), end: at('2026-09-22T08:00:00Z'), allDay: false })
    })
    expect(cached()).toMatchObject({ startAt: '2026-09-22T10:00:00+03:00', rowVersion: 3 }) // written now, request still pending
    await waitFor(() => expect(result.current.list.data?.[0].startAt).toBe('2026-09-22T10:00:00+03:00'))

    release()
    await act(async () => moved)
    expect(cached().rowVersion).toBe(4)
  })

  it('refetches once the burst has settled', async () => {
    const store = { entries: [wireEntry()] }
    const { lists } = mockServer(store)
    const { result } = setup()
    await waitFor(() => expect(result.current.list.data).toHaveLength(1))
    const before = lists()

    await act(async () => {
      await result.current.move(42, { start: at('2026-09-22T07:00:00Z'), end: at('2026-09-22T08:00:00Z'), allDay: false })
    })
    await waitFor(() => expect(lists()).toBe(before + 1))
  })

  it('ignores an entry that is not in the cache (no request)', async () => {
    const { puts } = mockServer({ entries: [wireEntry()] })
    const { result } = setup()
    await waitFor(() => expect(result.current.list.data).toHaveLength(1))
    await act(async () => {
      await result.current.move(999, { start: new Date(), end: null, allDay: false })
    })
    expect(puts).toHaveLength(0)
  })
})

describe('serialization per entry — one request in flight, the next uses the returned rowVersion', () => {
  it('the second drag waits for the first response and sends expectedVersion = the rowVersion the first returned', async () => {
    const store = { entries: [wireEntry()] }
    let inFlight = 0
    let maxInFlight = 0
    const releases: Array<() => void> = []
    const { puts } = mockServer(store, (put) => {
      inFlight += 1
      maxInFlight = Math.max(maxInFlight, inFlight)
      return new Promise<Response>((resolve) =>
        releases.push(() => {
          inFlight -= 1
          resolve(accept(store, put))
        }),
      )
    })
    const { result } = setup()
    await waitFor(() => expect(result.current.list.data).toHaveLength(1))

    let first!: Promise<void>
    let second!: Promise<void>
    act(() => {
      first = result.current.move(42, { start: at('2026-09-22T07:00:00Z'), end: at('2026-09-22T08:00:00Z'), allDay: false })
      second = result.current.move(42, { start: at('2026-09-23T07:00:00Z'), end: at('2026-09-23T08:00:00Z'), allDay: false })
    })
    expect(cached().startAt).toBe('2026-09-23T10:00:00+03:00') // both drags are already visible…

    await waitFor(() => expect(puts).toHaveLength(1))
    await new Promise((resolve) => setTimeout(resolve, 60))
    expect(puts).toHaveLength(1) // …yet the second is queued behind the first, not racing it

    releases[0]()
    await waitFor(() => expect(puts).toHaveLength(2))
    expect(puts[0].body.expectedVersion).toBe(3)
    expect(puts[1].body.expectedVersion).toBe(4) // learned from the first response, not the stale 3
    expect(puts[1].body.startAt).toBe('2026-09-23T10:00:00+03:00')
    expect(puts[1].key).not.toBe(puts[0].key)

    releases[1]()
    await act(async () => {
      await Promise.all([first, second])
    })
    expect(maxInFlight).toBe(1)
    await waitFor(() => expect(result.current.list.data?.[0]).toMatchObject({ rowVersion: 5, startAt: '2026-09-23T07:00:00+00:00' }))
  })

  it('different entries do not wait for each other', async () => {
    const store = { entries: [wireEntry(), wireEntry({ id: 43, title: 'Other' })] }
    const releases: Array<() => void> = []
    const { puts } = mockServer(store, (put) => new Promise<Response>((resolve) => releases.push(() => resolve(accept(store, put)))))
    const { result } = setup()
    await waitFor(() => expect(result.current.list.data).toHaveLength(2))

    act(() => {
      void result.current.move(42, { start: at('2026-09-22T07:00:00Z'), end: null, allDay: false })
      void result.current.move(43, { start: at('2026-09-22T09:00:00Z'), end: null, allDay: false })
    })
    await waitFor(() => expect(puts.map((put) => put.id).sort()).toEqual([42, 43]))
    releases.forEach((release) => release())
  })
})

describe('rollback on failure', () => {
  it('a 409 puts the entry back, refetches the server’s truth, and drops the moves queued behind it', async () => {
    const store = { entries: [wireEntry()] }
    let releaseFirst: () => void = () => {}
    const { puts, lists } = mockServer(store, () => {
      // Someone else edited the entry meanwhile: the server now holds version 9.
      store.entries = [wireEntry({ rowVersion: 9, title: 'Changed elsewhere' })]
      return new Promise<Response>((resolve) => (releaseFirst = () => resolve(problem(409, 'concurrency_conflict'))))
    })
    const { result } = setup()
    await waitFor(() => expect(result.current.list.data).toHaveLength(1))
    const before = lists()

    let first!: Promise<void>
    let second!: Promise<void>
    act(() => {
      first = result.current.move(42, { start: at('2026-09-22T07:00:00Z'), end: at('2026-09-22T08:00:00Z'), allDay: false })
      second = result.current.move(42, { start: at('2026-09-23T07:00:00Z'), end: at('2026-09-23T08:00:00Z'), allDay: false })
    })
    expect(cached().startAt).toBe('2026-09-23T10:00:00+03:00')

    await waitFor(() => expect(puts).toHaveLength(1))
    releaseFirst()
    await act(async () => {
      await Promise.all([first, second])
    })

    expect(puts).toHaveLength(1) // the queued second move was built on a state that no longer exists: never sent
    await waitFor(() => expect(lists()).toBeGreaterThan(before)) // refetch
    await waitFor(() => expect(result.current.list.data?.[0]).toMatchObject({ title: 'Changed elsewhere', rowVersion: 9 }))
  })

  it('restores the entry immediately on failure, before the (slow) refetch lands', async () => {
    const store = { entries: [wireEntry()] }
    mockServer(store, () => problem(500, 'internal'), 300)
    const { result } = setup()
    await waitFor(() => expect(result.current.list.data).toHaveLength(1))

    await act(async () => {
      await result.current.move(42, { start: at('2026-09-22T07:00:00Z'), end: at('2026-09-22T08:00:00Z'), allDay: false })
    })
    expect(cached()).toMatchObject({ startAt: '2026-09-21T06:00:00+00:00', rowVersion: 3 })
  })

  it('after a first move succeeded, a failing second move rolls back to the first’s confirmed state, not the original', async () => {
    const store = { entries: [wireEntry()] }
    mockServer(store, (put) => (put.body.expectedVersion === 3 ? accept(store, put) : problem(409, 'concurrency_conflict')), 300)
    const { result } = setup()
    await waitFor(() => expect(result.current.list.data).toHaveLength(1))

    let first!: Promise<void>
    let second!: Promise<void>
    act(() => {
      first = result.current.move(42, { start: at('2026-09-22T07:00:00Z'), end: at('2026-09-22T08:00:00Z'), allDay: false })
      second = result.current.move(42, { start: at('2026-09-23T07:00:00Z'), end: at('2026-09-23T08:00:00Z'), allDay: false })
    })
    await act(async () => {
      await Promise.all([first, second])
    })
    expect(cached()).toMatchObject({ startAt: '2026-09-22T10:00:00+03:00', rowVersion: 4 })
  })

  it('a network failure is also a rollback, with the entry unchanged', async () => {
    const store = { entries: [wireEntry()] }
    mockServer(store, () => HttpResponse.error(), 300)
    const { result } = setup()
    await waitFor(() => expect(result.current.list.data).toHaveLength(1))
    await act(async () => {
      await result.current.move(42, { start: at('2026-09-22T07:00:00Z'), end: null, allDay: false })
    })
    expect(cached()).toMatchObject({ startAt: '2026-09-21T06:00:00+00:00', rowVersion: 3 })
  })
})
