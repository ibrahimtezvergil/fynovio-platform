import type { TFunction } from 'i18next'
import { z } from 'zod'
import { COLOR_PATTERN, NOTES_MAX, TITLE_MAX } from '../schema'
import { fieldsAreOrdered } from './time'

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/
const TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/

/**
 * The create/edit form, mirroring the server's validation so a mistake is caught before the request (the server still
 * decides). Times are `HH:mm` wall-clock strings, dates `YYYY-MM-DD`; `lib/time.ts` turns them into offset-bearing
 * instants on submit. The title is trimmed and single-line, as the API requires.
 */
export const entryFormSchema = (t: TFunction<'calendar'>) =>
  z
    .object({
      title: z
        .string()
        .transform((value) => value.trim())
        .pipe(
          z
            .string()
            .min(1, t('form.title.required'))
            .max(TITLE_MAX, t('form.title.tooLong', { max: TITLE_MAX }))
            .refine((value) => !/[\r\n]/.test(value), t('form.title.singleLine')),
        ),
      notes: z.string().max(NOTES_MAX, t('form.notes.tooLong', { max: NOTES_MAX })),
      color: z.string().regex(COLOR_PATTERN, t('form.color.invalid')),
      allDay: z.boolean(),
      startDate: z.string().regex(DATE_PATTERN, t('form.date.required')),
      startTime: z.string(),
      endDate: z.string(),
      endTime: z.string(),
    })
    .superRefine((values, context) => {
      const issue = (path: string, message: string) => context.addIssue({ code: 'custom', path: [path], message })
      if (!values.allDay) {
        if (!TIME_PATTERN.test(values.startTime)) issue('startTime', t('form.time.required'))
        if (values.endTime !== '' && !TIME_PATTERN.test(values.endTime)) issue('endTime', t('form.time.invalid'))
        if (values.endDate !== '' && !DATE_PATTERN.test(values.endDate)) issue('endDate', t('form.date.invalid'))
      } else if (values.endDate !== '' && !DATE_PATTERN.test(values.endDate)) {
        issue('endDate', t('form.date.invalid'))
      }
      if (context.issues.length === 0 && !fieldsAreOrdered(values)) {
        issue(values.allDay || values.endDate !== values.startDate ? 'endDate' : 'endTime', t('form.end.beforeStart'))
      }
    })

export type EntryFormInput = z.input<ReturnType<typeof entryFormSchema>>
export type EntryFormValues = z.output<ReturnType<typeof entryFormSchema>>
