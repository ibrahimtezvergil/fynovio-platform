import { useQuery, useQueryClient, type QueryClient, type QueryKey } from '@tanstack/react-query'
import { useCallback, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import type { z } from 'zod'
import { apiClient } from '@/api/client'
import { endpoints } from '@/api/endpoints'
import { parseApiResponse } from '@/api/response'
import { useSessionStore } from '@/lib/auth'
import { useAppMutation } from '@/lib/mutations/useAppMutation'
import type { ApiError } from '@/types'
import { entryToBody } from './lib/entry'
import { describeProblem, toProblem } from './lib/problem'
import { timingFromRange, type DraggedRange } from './lib/time'
import {
  calendarListSchema,
  commandResultSchema,
  type CalendarCommandResult,
  type CalendarEntry,
  type CalendarEntryBody,
} from './schema'

/** The visible grid window as the API takes it: UTC instants (`toISOString()`, `Z` suffix), `to` exclusive. */
export interface VisibleRange {
  from: string
  to: string
}

export const toVisibleRange = (start: Date, end: Date): VisibleRange => ({ from: start.toISOString(), to: end.toISOString() })

/**
 * Calendar entries are PERSONAL data: every key is rooted in the active tenant AND the signed-in principal, so neither a
 * tenant switch nor a change of user on the same browser can surface another person's cached entries. `lib/auth`
 * additionally clears the cache on a switch; the key root is the second, independent guard.
 */
export const calendarKeys = {
  all: (tenantId: number | null, principalId: string | null) => ['calendar', tenantId, principalId] as const,
  entries: (tenantId: number | null, principalId: string | null) => [...calendarKeys.all(tenantId, principalId), 'entries'] as const,
  range: (tenantId: number | null, principalId: string | null, range: VisibleRange) =>
    [...calendarKeys.entries(tenantId, principalId), range] as const,
}

export function useCalendarIdentity() {
  const tenantId = useSessionStore((state) => state.activeTenantId)
  const principalId = useSessionStore((state) => state.user?.id ?? null)
  return { tenantId, principalId }
}

/**
 * An entry from whichever cached window holds it. Windows overlap and only the active ones are refetched after a
 * write, so an inactive window can hold an older copy: the highest `rowVersion` is the freshest, and it is the one a
 * drag must send as `expectedVersion`.
 */
export function findCachedEntry(queryClient: QueryClient, entriesKey: QueryKey, id: number): CalendarEntry | undefined {
  return queryClient
    .getQueriesData<CalendarEntry[]>({ queryKey: entriesKey })
    .flatMap(([, entries]) => entries ?? [])
    .filter((entry) => entry.id === id)
    .reduce<CalendarEntry | undefined>((freshest, entry) => (freshest && freshest.rowVersion >= entry.rowVersion ? freshest : entry), undefined)
}

/** Only a transient failure is worth an automatic retry; a 4xx (403, 422 range_too_large…) is an answer, not an incident. */
const retryTransient = (failureCount: number, error: unknown) => {
  const status = (error as Partial<ApiError>).status
  return (status === 0 || (status !== undefined && status >= 500)) && failureCount < 1
}

/** The page renders its own error state, so a failed read must not bubble to the app's error boundary. */
const READ_OPTIONS = { throwOnError: false, retry: retryTransient } as const

/**
 * Entries in the visible window. The request waits for the grid's first `datesSet` (`range` is `null` until then), and
 * previous data is kept while a navigation is in flight — but only within the SAME tenant and principal, so a switch never
 * shows the previous identity's entries as a placeholder.
 */
export function useCalendarEntries(range: VisibleRange | null) {
  const { tenantId, principalId } = useCalendarIdentity()
  return useQuery({
    ...READ_OPTIONS,
    queryKey: range ? calendarKeys.range(tenantId, principalId, range) : calendarKeys.entries(tenantId, principalId),
    enabled: tenantId !== null && principalId !== null && range !== null,
    placeholderData: (previousData, previousQuery) =>
      previousQuery?.queryKey[1] === tenantId && previousQuery.queryKey[2] === principalId ? previousData : undefined,
    queryFn: async (): Promise<CalendarEntry[]> => {
      const { data } = await apiClient.get<unknown>(endpoints.calendar.entries, { params: range })
      return (parseApiResponse(data, calendarListSchema) as z.output<typeof calendarListSchema>).items
    },
  })
}

const IDEMPOTENCY_HEADER = 'Idempotency-Key'

interface CommandOptions {
  /** Refetch the entries after success (default). Drag/resize does its own, once per burst. */
  refresh?: boolean
  /** Announce success with a toast (default). */
  toast?: boolean
}

export interface CreateEntryVariables {
  body: CalendarEntryBody
  idempotencyKey: string
}

/** Tenant and principal are never sent: the backend takes both from the authenticated actor. */
export function useCreateEntry() {
  const { t } = useTranslation('calendar')
  const { tenantId, principalId } = useCalendarIdentity()
  return useAppMutation<CalendarCommandResult, CreateEntryVariables>({
    name: 'calendar.createEntry',
    mutationFn: async ({ body, idempotencyKey }) => {
      const { data } = await apiClient.post<unknown>(endpoints.calendar.entries, body, { headers: { [IDEMPOTENCY_HEADER]: idempotencyKey } })
      return parseApiResponse(data, commandResultSchema)
    },
    invalidateKeys: () => [calendarKeys.entries(tenantId, principalId)],
    successToast: () => ({ title: t('toast.created') }),
    errorToast: () => null,
  })
}

export interface ReplaceEntryVariables {
  id: number
  expectedVersion: number
  body: CalendarEntryBody
  idempotencyKey: string
}

/** Full replace: `expectedVersion` rides in the body, the idempotency key in the header set at call time (so a 401 replay resends it). */
export function useReplaceEntry({ refresh = true, toast: announce = true }: CommandOptions = {}) {
  const { t } = useTranslation('calendar')
  const { tenantId, principalId } = useCalendarIdentity()
  return useAppMutation<CalendarCommandResult, ReplaceEntryVariables>({
    name: 'calendar.replaceEntry',
    mutationFn: async ({ id, expectedVersion, body, idempotencyKey }) => {
      const { data } = await apiClient.put<unknown>(
        endpoints.calendar.entry(id),
        { ...body, expectedVersion },
        { headers: { [IDEMPOTENCY_HEADER]: idempotencyKey } },
      )
      return parseApiResponse(data, commandResultSchema)
    },
    invalidateKeys: refresh ? () => [calendarKeys.entries(tenantId, principalId)] : undefined,
    successToast: announce ? () => ({ title: t('toast.updated') }) : undefined,
    errorToast: () => null,
  })
}

export interface DeleteEntryVariables {
  id: number
  expectedVersion: number
  idempotencyKey: string
}

/** `expectedVersion` is a query parameter here (a `DELETE` has no body); 204 on success, reported as `{ id }` so success is distinguishable from a failure. */
export function useDeleteEntry() {
  const { t } = useTranslation('calendar')
  const { tenantId, principalId } = useCalendarIdentity()
  return useAppMutation<{ id: number }, DeleteEntryVariables>({
    name: 'calendar.deleteEntry',
    mutationFn: async ({ id, expectedVersion, idempotencyKey }) => {
      await apiClient.delete(endpoints.calendar.entry(id), { params: { expectedVersion }, headers: { [IDEMPOTENCY_HEADER]: idempotencyKey } })
      return { id }
    },
    invalidateKeys: () => [calendarKeys.entries(tenantId, principalId)],
    successToast: () => ({ title: t('toast.deleted') }),
    errorToast: () => null,
  })
}

/** Everything one entry's drag/resize burst needs to stay ordered and to undo itself. */
interface MoveLane {
  tail: Promise<void>
  pending: number
  /** Bumped by a failure: moves queued before it were built on a state that no longer exists, so they are dropped. */
  epoch: number
  /** The `rowVersion` the server last confirmed for this entry within the burst; `null` until the first response. */
  version: number | null
  /** The last server-confirmed state: what a failed move rolls the optimistic write back to. */
  baseline: CalendarEntry
  failed: boolean
}

const mintKey = () => globalThis.crypto?.randomUUID?.() ?? `${Date.now().toString(16)}-${Math.random().toString(16).slice(2)}`

/**
 * Drag / resize → `PUT` with `expectedVersion`.
 *
 * - Optimistic: the grid shows the new time at once (cache write).
 * - Serialized per entry: one request in flight; the NEXT move waits and uses the `rowVersion` the previous response
 *   returned, so a quick second drag is not a self-inflicted 409.
 * - A failure (409 or otherwise) rolls the entry back to its last server-confirmed state, drops the moves queued behind
 *   it, and refetches so the grid shows the server's truth.
 * - One idempotency key per drag; the interactive retry story lives in the dialogs (`useKeyedCommand`).
 *
 * `useAppMutation` has no optimistic-update hooks, so the cache write and the rollback wrap `mutateAsync` here rather than
 * extending the shared wrapper.
 */
export function useMoveCalendarEntry() {
  const { t } = useTranslation('calendar')
  const queryClient = useQueryClient()
  const identity = useCalendarIdentity()
  const { mutateAsync } = useReplaceEntry({ refresh: false, toast: false })
  const lanes = useRef(new Map<number, MoveLane>())

  return useCallback(
    (id: number, range: DraggedRange): Promise<void> => {
      const entriesKey = calendarKeys.entries(identity.tenantId, identity.principalId)
      const patch = (update: (entry: CalendarEntry) => CalendarEntry) =>
        queryClient.setQueriesData<CalendarEntry[]>({ queryKey: entriesKey }, (entries) =>
          entries?.map((entry) => (entry.id === id ? update(entry) : entry)),
        )

      const current = findCachedEntry(queryClient, entriesKey, id)
      if (!current) return Promise.resolve()

      const timing = timingFromRange(range)
      const body: CalendarEntryBody = { ...entryToBody(current), ...timing }
      let lane = lanes.current.get(id)
      if (!lane) {
        lane = { tail: Promise.resolve(), pending: 0, epoch: 0, version: null, baseline: current, failed: false }
        lanes.current.set(id, lane)
      }
      const thisLane = lane
      const epoch = thisLane.epoch
      thisLane.pending += 1
      patch((entry) => ({ ...entry, ...timing }))

      thisLane.tail = thisLane.tail
        .then(async () => {
          if (thisLane.epoch !== epoch) return
          try {
            const result = await mutateAsync({
              id,
              expectedVersion: thisLane.version ?? current.rowVersion,
              body,
              idempotencyKey: mintKey(),
            })
            thisLane.version = result.rowVersion
            thisLane.baseline = { ...current, ...timing, rowVersion: result.rowVersion }
            patch((entry) => ({ ...entry, rowVersion: result.rowVersion }))
          } catch (error) {
            thisLane.epoch += 1
            thisLane.version = null
            thisLane.failed = true
            patch(() => thisLane.baseline)
            const { title, description } = describeProblem(t, toProblem(error as ApiError))
            toast.error(title, { description })
            void queryClient.invalidateQueries({ queryKey: entriesKey })
          }
        })
        .finally(() => {
          thisLane.pending -= 1
          if (thisLane.pending > 0) return
          lanes.current.delete(id)
          if (!thisLane.failed) void queryClient.invalidateQueries({ queryKey: entriesKey })
        })
      return thisLane.tail
    },
    [identity.tenantId, identity.principalId, mutateAsync, queryClient, t],
  )
}
