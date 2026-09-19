import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/api/client'
import { endpoints } from '@/api/endpoints'
import { parseApiResponse } from '@/api/response'
import {
  attentionItemSchema,
  recentWorkItemSchema,
  teamActivityItemSchema,
  type AttentionItem,
  type RecentWorkItem,
  type TeamActivityItem,
} from './schema'

export const homeKeys = {
  all: ['home'] as const,
  attention: () => [...homeKeys.all, 'attention'] as const,
  recentWork: () => [...homeKeys.all, 'recent-work'] as const,
  teamActivity: () => [...homeKeys.all, 'team-activity'] as const,
}

/** Served by the MSW handler at `src/mocks/handlers/home.ts` until a real Attention Engine exists. */
export function useAttentionItems() {
  return useQuery({
    queryKey: homeKeys.attention(),
    queryFn: async (): Promise<AttentionItem[]> => {
      const { data } = await apiClient.get<unknown>(endpoints.home.attention)
      return parseApiResponse(data, attentionItemSchema.array())
    },
  })
}

export function useRecentWork() {
  return useQuery({
    queryKey: homeKeys.recentWork(),
    queryFn: async (): Promise<RecentWorkItem[]> => {
      const { data } = await apiClient.get<unknown>(endpoints.home.recentWork)
      return parseApiResponse(data, recentWorkItemSchema.array())
    },
  })
}

export function useTeamActivity() {
  return useQuery({
    queryKey: homeKeys.teamActivity(),
    queryFn: async (): Promise<TeamActivityItem[]> => {
      const { data } = await apiClient.get<unknown>(endpoints.home.teamActivity)
      return parseApiResponse(data, teamActivityItemSchema.array())
    },
  })
}
