import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { apiClient } from '@/api/client'
import { endpoints } from '@/api/endpoints'
import { parseApiResponse } from '@/api/response'
import { NO_DEALS } from '@/features/pipeline/data/deals'
import { useAppMutation } from '@/lib/mutations/useAppMutation'
import type { Deal, Stage } from '@/types'
import { dealSchema } from '@/types/schemas'

export const pipelineKeys = {
  all: ['pipeline'] as const,
  deals: () => [...pipelineKeys.all, 'deals'] as const,
}

/**
 * Served by the MSW handler at `src/mocks/handlers/pipeline.ts` until the
 * backend is up — the mutations below already write through the cache, so a
 * real endpoint only needs its own `mutationFn`.
 */
export function usePipelineDeals() {
  return useQuery({
    queryKey: pipelineKeys.deals(),
    queryFn: async (): Promise<Deal[]> => {
      const { data } = await apiClient.get<unknown>(endpoints.pipeline.deals)
      return parseApiResponse(data, dealSchema.array())
    },
  })
}

type Patch = Partial<Pick<Deal, 'stage' | 'owner'>>

type DealsPatch = { ids: string[]; patch: Patch }

/** Applies one patch to every id in `ids` — stage change and reassignment both go through this. */
export function useUpdateDeals() {
  const { t } = useTranslation('pipeline')
  const queryClient = useQueryClient()
  return useAppMutation<DealsPatch, DealsPatch>({
    name: 'pipeline.updateDeals',
    mutationFn: async (variables) => variables,
    onSuccess: ({ ids, patch }) => {
      const targeted = new Set(ids)
      queryClient.setQueryData<Deal[]>(pipelineKeys.deals(), (deals = NO_DEALS) =>
        deals.map((deal) => (targeted.has(deal.id) ? { ...deal, ...patch } : deal)),
      )
    },
    // The same patch shape drives two different user-facing actions; the
    // toast text follows which field the caller actually changed.
    successToast: (_data, { ids, patch }) => {
      if (patch.stage) return { title: t('page.toastStageUpdated', { count: ids.length }) }
      if (patch.owner) return { title: t('page.toastAssigned', { count: ids.length, owner: patch.owner }) }
      return null
    },
  })
}

export function useRemoveDeals() {
  const { t } = useTranslation('pipeline')
  const queryClient = useQueryClient()
  return useAppMutation<string[], string[]>({
    name: 'pipeline.removeDeals',
    mutationFn: async (ids) => ids,
    onSuccess: (ids) => {
      const targeted = new Set(ids)
      queryClient.setQueryData<Deal[]>(pipelineKeys.deals(), (deals = NO_DEALS) =>
        deals.filter((deal) => !targeted.has(deal.id)),
      )
    },
    successToast: (ids) => ({ title: t('page.toastRemoved', { count: ids.length }) }),
  })
}

export type { Patch as DealPatch, Stage }
