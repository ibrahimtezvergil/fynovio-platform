import { z } from 'zod'
import { EVENT_KINDS, type CalendarEvent } from '@/features/calendar/types'

export const calendarEventSchema: z.ZodType<CalendarEvent> = z.object({
  id: z.string(),
  title: z.string(),
  start: z.string(),
  end: z.string().nullable(),
  allDay: z.boolean(),
  kind: z.enum(EVENT_KINDS),
  owner: z.string(),
  location: z.string().optional(),
  account: z.string().optional(),
  notes: z.string().optional(),
})
