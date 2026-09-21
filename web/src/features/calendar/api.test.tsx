import { QueryClientProvider } from '@tanstack/react-query'
import { act, renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { queryClient } from '@/api/queryClient'
import { useSessionStore } from '@/lib/auth'
import { selectTenant } from '@/lib/auth/sessionClient'
import { server } from '@/mocks/server'
import { authenticated, problem, url } from '@/test/authHandlers'
import { wireEntry } from '@/test/calendar'
import { resetSession } from '@/test/session'
import type { ApiError } from '@/types'
import { calendarKeys, findCachedEntry, toVisibleRange, useCalendarEntries, useCreateEntry, useDeleteEntry, useReplaceEntry } from './api'
import { calendarEntrySchema, type CalendarEntryBody } from './schema'

const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>

const RANGE = { from: '2026-08-31T21:00:00.000Z', to: '2026-10-12T21:00:00.000Z' }
const OTHER_RANGE = { from: '2026-09-28T21:00:00.000Z', to: '2026-11-09T21:00:00.000Z' }

beforeEach(() => {
  resetSession()
  queryClient.clear()
  useSessionStore.getState().applyAuthResult(authenticated({ activeTenant: { tenantId: 1 } })) // principal '7'
})

describe('calendar query keys — tenant AND principal isolation', () => {
  it('every key is rooted in the tenant and the principal', () => {
    for (const key of [calendarKeys.all(1, '7'), calendarKeys.entries(1, '7'), calendarKeys.range(1, '7', RANGE)]) {
      expect(key.slice(0, 3)).toEqual(['calendar', 1, '7'])
    }
  })

  it('changes with the tenant, and with the principal, independently', () => {
    expect(calendarKeys.range(1, '7', RANGE)).not.toEqual(calendarKeys.range(2, '7', RANGE))
    expect(calendarKeys.range(1, '7', RANGE)).not.toEqual(calendarKeys.range(1, '8', RANGE))
    expect(calendarKeys.range(1, '7', RANGE)).not.toEqual(calendarKeys.range(1, '7', OTHER_RANGE))
    expect(calendarKeys.entries(1, null)).not.toEqual(calendarKeys.entries(1, '7'))
  })

  it('a prefix invalidation for one identity cannot match another identity’s windows', () => {
    const mine = calendarKeys.range(1, '7', RANGE)
    const theirs = calendarKeys.range(1, '8', RANGE)
    const prefix = calendarKeys.entries(1, '7')
    expect(mine.slice(0, prefix.length)).toEqual(prefix)
    expect(theirs.slice(0, prefix.length)).not.toEqual(prefix)
  })
})

describe('findCachedEntry', () => {
  const cached = (rowVersion: number, title: string) => calendarEntrySchema.parse(wireEntry({ rowVersion, title }))

  it.each([
    ['the newer copy sits in the window that was cached last', [2, 3]],
    ['the newer copy sits in the window that was cached first', [3, 2]],
  ])('returns the copy with the highest rowVersion when %s', (_name, versions) => {
    const entriesKey = calendarKeys.entries(1, '7')
    queryClient.setQueryData(calendarKeys.range(1, '7', RANGE), [cached(versions[0], `v${versions[0]}`)])
    queryClient.setQueryData(calendarKeys.range(1, '7', OTHER_RANGE), [cached(versions[1], `v${versions[1]}`)])

    expect(findCachedEntry(queryClient, entriesKey, 42)?.rowVersion).toBe(3)
  })

  it('returns undefined when no window holds the entry, and ignores another identity’s windows', () => {
    queryClient.setQueryData(calendarKeys.range(1, '8', RANGE), [cached(9, 'theirs')])

    expect(findCachedEntry(queryClient, calendarKeys.entries(1, '7'), 42)).toBeUndefined()
  })
})

describe('toVisibleRange', () => {
  it('sends UTC instants with a Z suffix, whatever the browser offset', () => {
    const range = toVisibleRange(new Date('2026-09-21T00:00:00+03:00'), new Date('2026-10-01T00:00:00-05:00'))
    expect(range).toEqual({ from: '2026-09-20T21:00:00.000Z', to: '2026-10-01T05:00:00.000Z' })
    expect(range.from).toMatch(/Z$/)
    expect(range.to).not.toContain('+')
  })
})

describe('useCalendarEntries', () => {
  it('waits for the grid’s first visible range: no request with undefined from/to', async () => {
    let requests = 0
    server.use(http.get(url(endpoints.calendar.entries), () => (requests++, HttpResponse.json({ items: [] }))))
    const { result, rerender } = renderHook(({ range }) => useCalendarEntries(range), { wrapper, initialProps: { range: null as typeof RANGE | null } })

    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(requests).toBe(0)
    expect(result.current.fetchStatus).toBe('idle')

    rerender({ range: RANGE })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(requests).toBe(1)
  })

  it('asks for the visible window as UTC from/to, authenticated, and parses `{ items }`', async () => {
    let seen: { from: string | null; to: string | null; auth: string | null; keys: string[] } | undefined
    server.use(
      http.get(url(endpoints.calendar.entries), ({ request }) => {
        const search = new URL(request.url).searchParams
        seen = { from: search.get('from'), to: search.get('to'), auth: request.headers.get('Authorization'), keys: [...search.keys()].sort() }
        return HttpResponse.json({ items: [wireEntry()] })
      }),
    )
    const { result } = renderHook(() => useCalendarEntries(RANGE), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(seen).toEqual({ from: RANGE.from, to: RANGE.to, auth: 'Bearer access-token-1', keys: ['from', 'to'] }) // no tenant / principal parameter
    expect(result.current.data).toHaveLength(1)
    expect(result.current.data?.[0]).toMatchObject({ id: 42, rowVersion: 3, color: '#3b82f6' })
  })

  it('a malformed response is an error state, not a thrown render (throwOnError is off) and not a partial list', async () => {
    server.use(http.get(url(endpoints.calendar.entries), () => HttpResponse.json({ items: [wireEntry({ color: 'red' })] })))
    const { result } = renderHook(() => useCalendarEntries(RANGE), { wrapper })
    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(result.current.data).toBeUndefined()
  })

  it('422 range_too_large is an inline error carrying its code, and is not retried', async () => {
    let requests = 0
    server.use(http.get(url(endpoints.calendar.entries), () => (requests++, problem(422, 'range_too_large'))))
    const { result } = renderHook(() => useCalendarEntries(RANGE), { wrapper })
    await waitFor(() => expect(result.current.isError).toBe(true))
    expect((result.current.error as unknown as ApiError).code).toBe('range_too_large')
    expect(requests).toBe(1)
  })

  it('keeps the previous window on screen while the next one loads (same identity)', async () => {
    server.use(
      http.get(url(endpoints.calendar.entries), async ({ request }) => {
        const from = new URL(request.url).searchParams.get('from')
        if (from === OTHER_RANGE.from) await new Promise((resolve) => setTimeout(resolve, 60))
        return HttpResponse.json({ items: [wireEntry({ id: from === RANGE.from ? 1 : 2, title: from === RANGE.from ? 'August' : 'October' })] })
      }),
    )
    const { result, rerender } = renderHook(({ range }) => useCalendarEntries(range), { wrapper, initialProps: { range: RANGE } })
    await waitFor(() => expect(result.current.data?.[0].title).toBe('August'))

    rerender({ range: OTHER_RANGE })
    expect(result.current.isPlaceholderData).toBe(true)
    expect(result.current.data?.[0].title).toBe('August')
    await waitFor(() => expect(result.current.data?.[0].title).toBe('October'))
    expect(result.current.isPlaceholderData).toBe(false)
  })

  it('never shows another tenant’s entries as the placeholder while the new tenant loads', async () => {
    server.use(
      http.get(url(endpoints.calendar.entries), async ({ request }) => {
        if (request.headers.get('Authorization') === 'Bearer access-token-1') return HttpResponse.json({ items: [wireEntry({ title: 'Tenant one secret' })] })
        await new Promise((resolve) => setTimeout(resolve, 60))
        return HttpResponse.json({ items: [wireEntry({ id: 2, title: 'Tenant two' })] })
      }),
    )
    const { result } = renderHook(() => useCalendarEntries(RANGE), { wrapper })
    await waitFor(() => expect(result.current.data?.[0].title).toBe('Tenant one secret'))

    act(() => useSessionStore.setState({ activeTenantId: 2, accessToken: 'tenant-2-token' }))
    await waitFor(() => expect(result.current.isFetching).toBe(true))
    expect(result.current.data).toBeUndefined()
    await waitFor(() => expect(result.current.data?.[0].title).toBe('Tenant two'))
  })

  it('never shows another principal’s entries as the placeholder after a user change on the same tenant', async () => {
    server.use(
      http.get(url(endpoints.calendar.entries), async ({ request }) => {
        if (request.headers.get('Authorization') === 'Bearer access-token-1') return HttpResponse.json({ items: [wireEntry({ title: 'Ada private' })] })
        await new Promise((resolve) => setTimeout(resolve, 60))
        return HttpResponse.json({ items: [wireEntry({ id: 2, title: 'Berk private' })] })
      }),
    )
    const { result } = renderHook(() => useCalendarEntries(RANGE), { wrapper })
    await waitFor(() => expect(result.current.data?.[0].title).toBe('Ada private'))

    act(() => {
      useSessionStore.getState().applyAuthResult(
        authenticated({ accessToken: 'berk-token', account: { id: 8, email: 'berk@example.com', displayName: 'Berk Demir', locale: 'tr' } }),
      )
    })
    await waitFor(() => expect(result.current.isFetching).toBe(true))
    expect(result.current.data).toBeUndefined()
    await waitFor(() => expect(result.current.data?.[0].title).toBe('Berk private'))
  })

  it('a tenant switch through the session client clears the previous tenant’s cached entries', async () => {
    server.use(
      http.get(url(endpoints.calendar.entries), () => HttpResponse.json({ items: [wireEntry()] })),
      http.post(url(endpoints.auth.selectTenant), () => HttpResponse.json({ accessToken: 'tenant-2-token', expiresIn: 600, activeTenant: { tenantId: 2 } })),
    )
    const { result } = renderHook(() => useCalendarEntries(RANGE), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(queryClient.getQueryCache().findAll({ queryKey: ['calendar', 1] })).toHaveLength(1)

    await act(async () => {
      await selectTenant(2)
    })
    expect(queryClient.getQueryCache().findAll({ queryKey: ['calendar', 1] })).toHaveLength(0)
  })
})

const BODY: CalendarEntryBody = {
  title: 'Call with vendor',
  notes: null,
  color: '#3b82f6',
  allDay: false,
  startAt: '2026-09-21T09:00:00+03:00',
  endAt: null,
  startDate: null,
  endDate: null,
  link: { boundedContext: 'crm', entityType: 'opportunity', id: 17 },
}

describe('commands — contract shape', () => {
  it('POST /calendar/entries: the body as given, an Idempotency-Key header, no tenant/principal, no expectedVersion', async () => {
    let seen: { body: Record<string, unknown>; key: string | null } | undefined
    server.use(
      http.post(url(endpoints.calendar.entries), async ({ request }) => {
        seen = { body: (await request.json()) as Record<string, unknown>, key: request.headers.get('Idempotency-Key') }
        return HttpResponse.json({ id: 42, rowVersion: 1, replayed: false }, { status: 201 })
      }),
    )
    const { result } = renderHook(() => useCreateEntry(), { wrapper })

    let created: unknown
    await act(async () => {
      created = await result.current.mutateAsync({ body: BODY, idempotencyKey: 'key-1' })
    })
    expect(created).toEqual({ id: 42, rowVersion: 1, replayed: false })
    expect(seen?.key).toBe('key-1')
    expect(seen?.body).toEqual(BODY)
    expect(Object.keys(seen?.body ?? {})).not.toEqual(expect.arrayContaining(['tenantId']))
    expect(seen?.body).not.toHaveProperty('expectedVersion')
    expect(seen?.body).not.toHaveProperty('principalId')
    // The link is the bare ref, not the `{ ref, state, label }` response wrapper.
    expect(seen?.body.link).toEqual({ boundedContext: 'crm', entityType: 'opportunity', id: 17 })
  })

  it('PUT /calendar/entries/{id}: full body plus expectedVersion IN THE BODY, and the key header', async () => {
    let seen: { body: Record<string, unknown>; key: string | null; search: string } | undefined
    server.use(
      http.put(url(endpoints.calendar.entry(42)), async ({ request }) => {
        seen = { body: (await request.json()) as Record<string, unknown>, key: request.headers.get('Idempotency-Key'), search: new URL(request.url).search }
        return HttpResponse.json({ id: 42, rowVersion: 4, replayed: false })
      }),
    )
    const { result } = renderHook(() => useReplaceEntry(), { wrapper })

    await act(async () => {
      await result.current.mutateAsync({ id: 42, expectedVersion: 3, body: { ...BODY, link: null }, idempotencyKey: 'key-2' })
    })
    expect(seen).toEqual({ body: { ...BODY, link: null, expectedVersion: 3 }, key: 'key-2', search: '' })
  })

  it('DELETE /calendar/entries/{id}: expectedVersion is a QUERY parameter, there is no body, and the key header is sent', async () => {
    let seen: { expected: string | null; key: string | null; body: string } | undefined
    server.use(
      http.delete(url(endpoints.calendar.entry(42)), async ({ request }) => {
        seen = { expected: new URL(request.url).searchParams.get('expectedVersion'), key: request.headers.get('Idempotency-Key'), body: await request.text() }
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { result } = renderHook(() => useDeleteEntry(), { wrapper })

    let deleted: unknown
    await act(async () => {
      deleted = await result.current.mutateAsync({ id: 42, expectedVersion: 3, idempotencyKey: 'key-3' })
    })
    expect(deleted).toEqual({ id: 42 })
    expect(seen).toEqual({ expected: '3', key: 'key-3', body: '' })
  })

  it('refetches the identity’s windows after a successful create, and only theirs', async () => {
    let lists = 0
    server.use(
      http.get(url(endpoints.calendar.entries), () => (lists++, HttpResponse.json({ items: [] }))),
      http.post(url(endpoints.calendar.entries), () => HttpResponse.json({ id: 1, rowVersion: 1, replayed: false }, { status: 201 })),
    )
    const list = renderHook(() => useCalendarEntries(RANGE), { wrapper })
    await waitFor(() => expect(list.result.current.isSuccess).toBe(true))
    queryClient.setQueryData(calendarKeys.range(1, 'someone-else', RANGE), [])
    const before = lists

    const create = renderHook(() => useCreateEntry(), { wrapper })
    await act(async () => {
      await create.result.current.mutateAsync({ body: BODY, idempotencyKey: 'k' })
    })
    await waitFor(() => expect(lists).toBe(before + 1))
    expect(queryClient.getQueryState(calendarKeys.range(1, 'someone-else', RANGE))?.isInvalidated).toBe(false)
  })

  it.each([
    [409, 'concurrency_conflict'],
    [409, 'idempotency_key_reused'],
    [404, 'not_found'],
    [422, 'link_target_unavailable'],
    [400, 'validation_error'],
    [403, 'forbidden'],
  ])('a %i %s rejects with the ProblemDetails type as `code`', async (status, type) => {
    server.use(http.put(url(endpoints.calendar.entry(42)), () => problem(status, type)))
    const { result } = renderHook(() => useReplaceEntry(), { wrapper })
    const error = await act(async () => result.current.mutateAsync({ id: 42, expectedVersion: 3, body: BODY, idempotencyKey: 'k' }).catch((e: ApiError) => e))
    expect(error).toMatchObject({ status, code: type })
  })
})
