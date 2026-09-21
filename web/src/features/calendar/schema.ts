import { z } from 'zod'

/** Server-normalised `#rrggbb`. The value flows into inline styles, so it is validated at the boundary. */
export const COLOR_PATTERN = /^#[0-9a-f]{6}$/
const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/
/** An instant that carries its offset (`Z` or `±hh:mm`); the API rejects offset-less timed values. */
const OFFSET_INSTANT_PATTERN = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})$/

export const TITLE_MAX = 200
export const NOTES_MAX = 4000

/** A field the contract calls "null when absent": tolerate the key being omitted as well as explicit `null`. */
const nullable = <T extends z.ZodType>(schema: T) => schema.nullish().transform((value) => value ?? null)

const instant = z.string().regex(OFFSET_INSTANT_PATTERN).refine((value) => !Number.isNaN(Date.parse(value)))
const isoDate = z.string().regex(DATE_PATTERN)

const linkRefSchema = z.object({
  boundedContext: z.string(),
  entityType: z.string(),
  id: z.number().int(),
})
export type CalendarLinkRef = z.infer<typeof linkRefSchema>

/**
 * `label`/`subtitle` exist only for an accessible target. If a response ever carried them for an unavailable one they are
 * dropped here, so no later component can render a name the caller is no longer allowed to see.
 */
const linkSchema = z
  .object({
    ref: linkRefSchema,
    state: z.enum(['accessible', 'unavailable']),
    label: z.string().nullish(),
    subtitle: z.string().nullish(),
  })
  .transform(({ ref, state, label, subtitle }) =>
    state === 'accessible'
      ? { ref, state, label: label ?? undefined, subtitle: subtitle ?? undefined }
      : { ref, state, label: undefined, subtitle: undefined },
  )
export type CalendarLink = z.output<typeof linkSchema>

export const calendarEntrySchema = z
  .object({
    id: z.number().int(),
    rowVersion: z.number().int(),
    title: z.string(),
    notes: nullable(z.string()),
    color: z.string().regex(COLOR_PATTERN),
    allDay: z.boolean(),
    startAt: nullable(instant),
    endAt: nullable(instant),
    startDate: nullable(isoDate),
    endDate: nullable(isoDate),
    link: nullable(linkSchema),
  })
  .superRefine((entry, context) => {
    const fail = (message: string) => context.addIssue({ code: 'custom', message })
    if (entry.allDay) {
      if (!entry.startDate || !entry.endDate) fail('An all-day entry needs startDate and endDate.')
      else if (entry.endDate <= entry.startDate) fail('An all-day entry ends (exclusive) after it starts.')
      if (entry.startAt !== null || entry.endAt !== null) fail('An all-day entry has no instants.')
    } else {
      if (!entry.startAt) fail('A timed entry needs startAt.')
      else if (entry.endAt && Date.parse(entry.endAt) <= Date.parse(entry.startAt)) fail('A timed entry ends after it starts.')
      if (entry.startDate !== null || entry.endDate !== null) fail('A timed entry has no dates.')
    }
  })
export type CalendarEntry = z.output<typeof calendarEntrySchema>

export const calendarListSchema = z.object({ items: z.array(calendarEntrySchema) })

/** `{ id, rowVersion, replayed }` — the answer to create and replace. */
export const commandResultSchema = z.object({
  id: z.number().int(),
  rowVersion: z.number().int(),
  replayed: z.boolean(),
})
export type CalendarCommandResult = z.infer<typeof commandResultSchema>

/** The timing half of an entry, in wire shape. Exactly one of the two pairs is populated. */
export interface CalendarTiming {
  allDay: boolean
  startAt: string | null
  endAt: string | null
  startDate: string | null
  endDate: string | null
}

/** The full-replace body of `POST`/`PUT` (`expectedVersion` is added by the caller for `PUT`). */
export interface CalendarEntryBody extends CalendarTiming {
  title: string
  notes: string | null
  color: string
  link: CalendarLinkRef | null
}
