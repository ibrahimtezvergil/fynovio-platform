import { useQuery, useQueryClient, type QueryKey } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { z } from 'zod'
import { apiClient } from '@/api/client'
import { endpoints } from '@/api/endpoints'
import { parseApiResponse } from '@/api/response'
import { useSessionStore } from '@/lib/auth'
import { useAppMutation } from '@/lib/mutations/useAppMutation'
import type { ApiError } from '@/types'
import {
  availableActionsSchema,
  commandResultSchema,
  createResultSchema,
  opportunitySchema,
  opportunitySummarySchema,
  pipelineStageSchema,
  type OpportunityStatus,
} from './schema'

export const PAGE_SIZE = 25

export interface OpportunityListFilter {
  status?: OpportunityStatus
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

export function usePipelineStages(versionId: number | null | undefined) {
  const tenantId = useTenantId()
  return useQuery({
    ...READ_OPTIONS,
    queryKey: opportunityKeys.stages(tenantId, versionId ?? -1),
    enabled: tenantId !== null && versionId != null,
    staleTime: 5 * 60_000, // tenant configuration, not record data
    queryFn: async () => (await get(endpoints.pipelines.stages(versionId as number), pipelineStageSchema.array())).toSorted((a, b) => a.sortOrder - b.sortOrder),
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
      const { url, body } = toRequest(variables)
      const { data } = await apiClient.post<unknown>(url, body, { headers: { 'Idempotency-Key': variables.idempotencyKey } })
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
 * Bound to the real contract and unit-tested, but NOT wired to any control: the API takes a raw
 * (issuer, subject) and there is no eligible-assignee source, so no UI may collect a principal (plan OD1).
 */
export const useReassignOpportunity = () =>
  useOpportunityCommand<CommandBase & { newPrincipalIssuer: string; newPrincipalSubject: string }>(
    'reassign',
    ({ id, expectedVersion, newPrincipalIssuer, newPrincipalSubject }) => ({
      url: endpoints.opportunities.reassign(id),
      body: { expectedVersion, newPrincipalIssuer, newPrincipalSubject },
    }),
  )
