import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { apiClient } from '@/api/client'
import { endpoints } from '@/api/endpoints'
import { parseApiResponse } from '@/api/response'
import { calendarEventSchema } from '@/features/calendar/schema'
import type { CalendarEvent } from '@/features/calendar/types'
import { useAppMutation } from '@/lib/mutations/useAppMutation'

export const calendarKeys = {
  all: ['calendar'] as const,
  events: () => [...calendarKeys.all, 'events'] as const,
}

/** Served by the MSW handler at `src/mocks/handlers/calendar.ts` until the backend is up. */
export function useCalendarEvents() {
  return useQuery({
    queryKey: calendarKeys.events(),
    queryFn: async (): Promise<CalendarEvent[]> => {
      const { data } = await apiClient.get<unknown>(endpoints.calendar.events)
      return parseApiResponse(data, calendarEventSchema.array())
    },
  })
}

type MoveEvent = { id: string; patch: Pick<CalendarEvent, 'start' | 'end' | 'allDay'> }

/**
 * Drag / resize. With a backend, `mutationFn` becomes the real request and
 * `onError` rolls the optimistic write back — the cache write below is
 * already the right shape for that (#13, Optimistic Update Layer).
 */
export function useMoveCalendarEvent() {
  const { t } = useTranslation('calendar')
  const queryClient = useQueryClient()
  return useAppMutation<MoveEvent, MoveEvent>({
    name: 'calendar.moveEvent',
    mutationFn: async (variables) => variables,
    onSuccess: ({ id, patch }) => {
      queryClient.setQueryData<CalendarEvent[]>(calendarKeys.events(), (events) =>
        events?.map((event) => (event.id === id ? { ...event, ...patch } : event)),
      )
    },
    successToast: () => ({
      title: t('page.movedToast'),
      description: t('page.movedToastDescription'),
    }),
  })
}
