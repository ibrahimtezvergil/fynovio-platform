import { useQueries, useQuery, useQueryClient, type QueryKey } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { z } from 'zod'
import { apiClient } from '@/api/client'
import { endpoints } from '@/api/endpoints'
import { parseApiResponse } from '@/api/response'
import { useSessionStore } from '@/lib/auth'
import { useAppMutation } from '@/lib/mutations/useAppMutation'
import type { ApiError } from '@/types'
import {
  assignablePrincipalSchema,
  availableActionsSchema,
  commandResultSchema,
  createPartyResultSchema,
  createResultSchema,
  opportunitySchema,
  opportunitySummarySchema,
  partyReferenceSchema,
  pipelineStageSchema,
  type OpportunityStatus,
} from './schema'

export const PAGE_SIZE = 25

/** Candidates offered by a picker per request; the server caps it at 50. */
export const PICKER_PAGE_SIZE = 20

export interface OpportunityListFilter {
  status?: OpportunityStatus
  archivedOnly?: boolean
  /** Zero-based page. */
  page: number
}

/**
 * Every key is rooted in the ACTIVE TENANT so a tenant switch can never surface another tenant's cached
 * Opportunity (spec §18). `lib/auth/sessionClient` additionally cancels and clears the whole cache on a
 * switch; the key root is the second, independent guard.
 */
export const opportunityKeys = {
  all: (tenantId: number | null) => ['opportunities', tenantId] as const,
  lists: (tenantId: number | null) => [...opportunityKeys.all(tenantId), 'list'] as const,
  list: (tenantId: number | null, filter: OpportunityListFilter) => [...opportunityKeys.lists(tenantId), filter] as const,
  detail: (tenantId: number | null, id: number) => [...opportunityKeys.all(tenantId), 'detail', id] as const,
  actions: (tenantId: number | null, id: number) => [...opportunityKeys.all(tenantId), 'actions', id] as const,
  stages: (tenantId: number | null, versionId: number) => ['crm-pipeline-stages', tenantId, versionId] as const,
  defaultStages: (tenantId: number | null) => ['crm-pipeline-stages', tenantId, 'default'] as const,
  assignable: (tenantId: number | null, id: number, search: string) => [...opportunityKeys.all(tenantId), 'assignable', id, search] as const,
}

/** Reference lookups (Parties) are tenant-rooted too — a switch must never show another tenant's customers. */
export const referenceKeys = {
  parties: (tenantId: number | null, search: string) => ['crm-references', tenantId, 'parties', 'search', search] as const,
  partyNames: (tenantId: number | null, ids: readonly number[]) => ['crm-references', tenantId, 'parties', 'ids', ...ids] as const,
}

export const useTenantId = () => useSessionStore((state) => state.activeTenantId)

/** Only a transient failure is worth an automatic retry; a 4xx is an answer, not an incident. */
const retryTransient = (failureCount: number, error: unknown) => {
  const status = (error as Partial<ApiError>).status
  return (status === 0 || (status !== undefined && status >= 500)) && failureCount < 1
}

/** These screens render their own loading/forbidden/not-found/error states, so errors must not bubble to a boundary. */
const READ_OPTIONS = { throwOnError: false, retry: retryTransient } as const

async function get<S extends z.ZodType>(url: string, schema: S, params?: Record<string, unknown>): Promise<z.output<S>> {
  const { data } = await apiClient.get<unknown>(url, { params })
  return parseApiResponse(data, schema) as z.output<S>
}

export function useOpportunityList(filter: OpportunityListFilter) {
  const tenantId = useTenantId()
  return useQuery({
    ...READ_OPTIONS,
    queryKey: opportunityKeys.list(tenantId, filter),
    enabled: tenantId !== null,
    // The list contract has no total: one extra row tells us whether a next page exists.
    queryFn: async () => {
      const rows = await get(endpoints.opportunities.list, opportunitySummarySchema.array(), {
        status: filter.status,
        skip: filter.page * PAGE_SIZE,
        take: PAGE_SIZE + 1,
        archivedOnly: filter.archivedOnly ?? false,
      })
      return { items: rows.slice(0, PAGE_SIZE), hasNext: rows.length > PAGE_SIZE }
    },
  })
}

export function useOpportunity(id: number) {
  const tenantId = useTenantId()
  return useQuery({
    ...READ_OPTIONS,
    queryKey: opportunityKeys.detail(tenantId, id),
    // A malformed route id must never reach the API as a request for /opportunities/0 or /opportunities/NaN.
    enabled: tenantId !== null && Number.isInteger(id) && id > 0,
    queryFn: () => get(endpoints.opportunities.detail(id), opportunitySchema),
  })
}

/** The server's authoritative projection of what the caller may do NOW. */
export function useAvailableActions(id: number, enabled = true) {
  const tenantId = useTenantId()
  return useQuery({
    ...READ_OPTIONS,
    queryKey: opportunityKeys.actions(tenantId, id),
    enabled: enabled && tenantId !== null,
    queryFn: () => get(endpoints.opportunities.actions(id), availableActionsSchema),
  })
}

/**
 * The same actions projection, fetched imperatively (through the query cache) for a moment that needs the answer
 * before any component could subscribe — the board loads it when a card is picked up, not for all cards up front.
 */
export function useFetchAvailableActions() {
  const queryClient = useQueryClient()
  const tenantId = useTenantId()
  return (id: number) =>
    queryClient.fetchQuery({
      queryKey: opportunityKeys.actions(tenantId, id),
      staleTime: 5_000,
      queryFn: () => get(endpoints.opportunities.actions(id), availableActionsSchema),
    })
}

const STAGES_STALE_TIME = 5 * 60_000 // tenant configuration, not record data

async function fetchStages(versionId: number) {
  return (await get(endpoints.pipelines.stages(versionId), pipelineStageSchema.array())).toSorted((a, b) => a.sortOrder - b.sortOrder)
}

export function usePipelineStages(versionId: number | null | undefined) {
  const tenantId = useTenantId()
  return useQuery({
    ...READ_OPTIONS,
    queryKey: opportunityKeys.stages(tenantId, versionId ?? -1),
    enabled: tenantId !== null && versionId != null,
    staleTime: STAGES_STALE_TIME,
    queryFn: () => fetchStages(versionId as number),
  })
}

export function useDefaultPipelineStages(enabled = true) {
  const tenantId = useTenantId()
  return useQuery({
    ...READ_OPTIONS,
    queryKey: opportunityKeys.defaultStages(tenantId),
    enabled: enabled && tenantId !== null,
    staleTime: STAGES_STALE_TIME,
    queryFn: async () => (await get(endpoints.pipelines.defaultStages, pipelineStageSchema.array())).toSorted((a, b) => a.sortOrder - b.sortOrder),
  })
}

/** A stage id only means something inside its pipeline version, so names are keyed by the pair. */
export const stageKey = (versionId: number, stageId: number) => `${versionId}:${stageId}`

/**
 * Stage names for every pipeline version present on a page of rows (one request per distinct version, shared with
 * `usePipelineStages` through the same cache key). Best effort: a version the caller cannot read is simply absent.
 */
export function usePipelineStageNames(versionIds: readonly number[]) {
  const tenantId = useTenantId()
  return useQueries({
    queries: versionIds.map((versionId) => ({
      ...READ_OPTIONS,
      queryKey: opportunityKeys.stages(tenantId, versionId),
      enabled: tenantId !== null,
      staleTime: STAGES_STALE_TIME,
      queryFn: () => fetchStages(versionId),
    })),
    combine: (results) =>
      new Map(results.flatMap((result, index) => (result.data ?? []).map((stage) => [stageKey(versionIds[index], stage.id), stage.name] as const))),
  })
}

/**
 * Who the SERVER offers as a new owner for this opportunity: authorization-aware and already filtered by the
 * backend, so callers render the list as-is. Never reused across searches for long — assignability changes with grants.
 * Returns a searcher for `AsyncCombobox`; it goes through the query cache so a tenant switch discards it.
 */
export function useAssigneeSearcher(id: number) {
  const tenantId = useTenantId()
  const queryClient = useQueryClient()
  return (search: string) =>
    queryClient.fetchQuery({
      queryKey: opportunityKeys.assignable(tenantId, id, search),
      staleTime: 0,
      gcTime: 30_000,
      retry: false,
      queryFn: () =>
        get(endpoints.opportunities.assignablePrincipals(id), assignablePrincipalSchema.array(), { search: search || undefined, take: PICKER_PAGE_SIZE }),
    })
}

/** Type-ahead over the tenant's Parties (blank search = the first alphabetical page), as a searcher for `AsyncCombobox`. */
export function usePartySearcher() {
  const tenantId = useTenantId()
  const queryClient = useQueryClient()
  return (search: string) =>
    queryClient.fetchQuery({
      queryKey: referenceKeys.parties(tenantId, search),
      staleTime: 30_000,
      gcTime: 60_000,
      retry: false,
      queryFn: () => get(endpoints.references.parties, partyReferenceSchema.array(), { search: search || undefined, take: PICKER_PAGE_SIZE }),
    })
}

/**
 * Display names for parties already referenced by a record. Best effort by design: a caller without the
 * party-search permission gets a 403 here and the UI keeps showing the bare id — a name is never required.
 */
export function usePartyNames(ids: readonly number[]) {
  const tenantId = useTenantId()
  const wanted = ids.filter((id) => Number.isInteger(id) && id > 0)
  return useQuery({
    ...READ_OPTIONS,
    queryKey: referenceKeys.partyNames(tenantId, wanted),
    enabled: tenantId !== null && wanted.length > 0,
    staleTime: 60_000,
    queryFn: async () => {
      const rows = await get(endpoints.references.parties, partyReferenceSchema.array(), { ids: wanted.join(',') })
      return new Map(rows.map((row) => [row.id, row.displayName] as const))
    },
  })
}

/** Forces the detail and its action projection to be re-read from the server (the "Reload latest" of a conflict). */
export function useReloadOpportunity(id: number) {
  const queryClient = useQueryClient()
  const tenantId = useTenantId()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: opportunityKeys.detail(tenantId, id) }),
      queryClient.invalidateQueries({ queryKey: opportunityKeys.actions(tenantId, id) }),
    ])
}

// ---- commands ----

/** Carried by every command: the record's concurrency token as the user saw it, and the logical action's idempotency key. */
export interface CommandBase {
  id: number
  expectedVersion: number
  idempotencyKey: string
}

interface CommandRequest {
  url: string
  body: unknown
  /** POST unless the endpoint is a full replacement. */
  method?: 'post' | 'put'
}

/**
 * One request in flight, server-confirmed (no optimistic update — spec §19). The idempotency key travels
 * as an explicit header set HERE, at call time, so the 401 → refresh → replay resends the identical key.
 * Failures are rendered inline by the caller (`toProblem`), so the wrapper's default error toast is muted.
 */
function useOpportunityCommand<V extends CommandBase>(name: string, toRequest: (variables: V) => CommandRequest) {
  const { t } = useTranslation('opportunities')
  const tenantId = useTenantId()
  return useAppMutation<z.infer<typeof commandResultSchema>, V>({
    name: `opportunities.${name}`,
    mutationFn: async (variables) => {
      const { url, body, method = 'post' } = toRequest(variables)
      const { data } = await apiClient[method]<unknown>(url, body, { headers: { 'Idempotency-Key': variables.idempotencyKey } })
      return parseApiResponse(data, commandResultSchema)
    },
    invalidateKeys: (_data, variables): QueryKey[] => [
      opportunityKeys.detail(tenantId, variables.id),
      opportunityKeys.actions(tenantId, variables.id),
      opportunityKeys.lists(tenantId),
    ],
    successToast: () => ({ title: t(`toast.${name}`) }),
    errorToast: () => null,
  })
}

export interface CreateOpportunityVariables {
  partyId: number
  currency: string
  estimatedAmount: number
  /** Omitted when the tenant has no active fields; validated by the server against the definitions. */
  customFields?: Record<string, unknown>
  idempotencyKey: string
}

/** Tenant and principal are never sent: the backend takes both from the authenticated actor. */
export function useCreateOpportunity() {
  const { t } = useTranslation('opportunities')
  const tenantId = useTenantId()
  return useAppMutation<z.infer<typeof createResultSchema>, CreateOpportunityVariables>({
    name: 'opportunities.create',
    mutationFn: async ({ idempotencyKey, ...body }) => {
      const { data } = await apiClient.post<unknown>(endpoints.opportunities.create, body, { headers: { 'Idempotency-Key': idempotencyKey } })
      return parseApiResponse(data, createResultSchema)
    },
    invalidateKeys: () => [opportunityKeys.lists(tenantId)],
    successToast: () => ({ title: t('toast.create') }),
    errorToast: () => null,
  })
}

export const useOpenOpportunity = () =>
  useOpportunityCommand<CommandBase & { expiryDate: string }>('open', ({ id, expectedVersion, expiryDate }) => ({
    url: endpoints.opportunities.open(id),
    body: { expectedVersion, expiryDate },
  }))

export const useChangeStage = () =>
  useOpportunityCommand<CommandBase & { targetStageId: number }>('changeStage', ({ id, expectedVersion, targetStageId }) => ({
    url: endpoints.opportunities.changeStage(id),
    body: { expectedVersion, targetStageId },
  }))

export const useUpdateOpportunityCustomFields = () =>
  useOpportunityCommand<CommandBase & { customFields: Record<string, unknown> }>('updateCustomFields', ({ id, expectedVersion, customFields }) => ({
    url: endpoints.opportunities.customFields(id),
    body: { expectedVersion, customFields },
    method: 'put',
  }))

export const useWinOpportunity = () =>
  useOpportunityCommand<CommandBase>('win', ({ id, expectedVersion }) => ({
    url: endpoints.opportunities.win(id),
    body: { expectedVersion },
  }))

export const useLoseOpportunity = () =>
  useOpportunityCommand<CommandBase & { lostReason: string }>('lose', ({ id, expectedVersion, lostReason }) => ({
    url: endpoints.opportunities.lose(id),
    body: { expectedVersion, lostReason },
  }))

export const useSetOpportunityArchive = (archive: boolean) =>
  useOpportunityCommand<CommandBase & { confirmOpenOpportunity?: boolean; stageId?: number }>(archive ? 'archive' : 'restore',
    ({ id, expectedVersion, confirmOpenOpportunity, stageId }) => ({
      url: archive ? endpoints.opportunities.archive(id) : endpoints.opportunities.restore(id),
      body: archive ? { expectedVersion, confirmOpenOpportunity: confirmOpenOpportunity ?? false } : { expectedVersion, stageId },
    }))

export const useAddLine = () =>
  useOpportunityCommand<CommandBase & { productId: number; quantity: number; unitPrice: number; isOptional: boolean; sortOrder: number }>(
    'addLine',
    ({ id, expectedVersion, productId, quantity, unitPrice, isOptional, sortOrder }) => ({
      url: endpoints.opportunities.addLine(id),
      body: { expectedVersion, productId, quantity, unitPrice, isOptional, sortOrder },
    }),
  )

export const useCancelLine = () =>
  useOpportunityCommand<CommandBase & { lineId: number; cancelReason: string }>('cancelLine', ({ id, expectedVersion, lineId, cancelReason }) => ({
    url: endpoints.opportunities.cancelLine(id, lineId),
    body: { expectedVersion, cancelReason },
  }))

/**
 * The API takes a raw (issuer, subject); the only control that supplies one is the assignee picker, which offers
 * exactly the principals `useAssignablePrincipals` returned. The server re-validates the target regardless.
 */
export const useReassignOpportunity = () =>
  useOpportunityCommand<CommandBase & { newPrincipalIssuer: string; newPrincipalSubject: string }>(
    'reassign',
    ({ id, expectedVersion, newPrincipalIssuer, newPrincipalSubject }) => ({
      url: endpoints.opportunities.reassign(id),
      body: { expectedVersion, newPrincipalIssuer, newPrincipalSubject },
    }),
  )

export interface CreatePartyVariables {
  partyType: 'Person' | 'Organization'
  name: string
  surname: string | null
  phone: string | null
  email: string | null
  idempotencyKey: string
}

/** Adds a customer the person could not find; the dialog shows the failure inline, so no error toast. */
export function useCreateParty() {
  const { t } = useTranslation('opportunities')
  return useAppMutation<z.infer<typeof createPartyResultSchema>, CreatePartyVariables>({
    name: 'opportunities.createParty',
    mutationFn: async ({ idempotencyKey, ...body }) => {
      const { data } = await apiClient.post<unknown>(endpoints.references.parties, body, { headers: { 'Idempotency-Key': idempotencyKey } })
      return parseApiResponse(data, createPartyResultSchema)
    },
    successToast: () => ({ title: t('newParty.created') }),
    errorToast: () => null,
  })
}
