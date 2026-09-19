import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/api/client'
import { endpoints } from '@/api/endpoints'
import { parseApiResponse } from '@/api/response'
import type { Activity, Deal, StageBucket } from '@/types'
import { activitySchema, dealSchema, stageBucketSchema } from '@/types/schemas'

export const dashboardKeys = {
  all: ['dashboard'] as const,
  deals: () => [...dashboardKeys.all, 'deals'] as const,
  stages: () => [...dashboardKeys.all, 'stages'] as const,
  activities: () => [...dashboardKeys.all, 'activities'] as const,
}

/** Served by the MSW handler at `src/mocks/handlers/dashboard.ts` until the backend is up. */
export function useDeals() {
  return useQuery({
    queryKey: dashboardKeys.deals(),
    queryFn: async (): Promise<Deal[]> => {
      const { data } = await apiClient.get<unknown>(endpoints.dashboard.deals)
      return parseApiResponse(data, dealSchema.array())
    },
  })
}

export function useStageDistribution() {
  return useQuery({
    queryKey: dashboardKeys.stages(),
    queryFn: async (): Promise<StageBucket[]> => {
      const { data } = await apiClient.get<unknown>(endpoints.dashboard.stages)
      return parseApiResponse(data, stageBucketSchema.array())
    },
  })
}

export function useActivities() {
  return useQuery({
    queryKey: dashboardKeys.activities(),
    queryFn: async (): Promise<Activity[]> => {
      const { data } = await apiClient.get<unknown>(endpoints.dashboard.activities)
      return parseApiResponse(data, activitySchema.array())
    },
  })
}
