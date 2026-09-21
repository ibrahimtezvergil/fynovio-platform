import { addDays, format, isSameDay, parseISO } from 'date-fns'
import { enUS, tr, type Locale } from 'date-fns/locale'
import type { CalendarEntry, CalendarTiming } from '../schema'

const pad = (value: number, length = 2) => String(value).padStart(length, '0')

/**
 * An instant as offset-bearing ISO 8601 (`2026-09-21T09:00:00+03:00`) in the given offset — by default the browser's
 * offset AT THAT INSTANT, so a date on the other side of a DST change gets its own offset. The API refuses an
 * offset-less timed value: a server-local reading of a wall clock must never enter the contract.
 */
export function formatOffsetIso(date: Date, offsetMinutes = -date.getTimezoneOffset()): string {
  const wall = new Date(date.getTime() + offsetMinutes * 60_000)
  const sign = offsetMinutes < 0 ? '-' : '+'
  const absolute = Math.abs(offsetMinutes)
  return (
    `${pad(wall.getUTCFullYear(), 4)}-${pad(wall.getUTCMonth() + 1)}-${pad(wall.getUTCDate())}` +
    `T${pad(wall.getUTCHours())}:${pad(wall.getUTCMinutes())}:${pad(wall.getUTCSeconds())}` +
    `${sign}${pad(Math.floor(absolute / 60))}:${pad(absolute % 60)}`
  )
}

/** A calendar day as `YYYY-MM-DD` in the browser's timezone. */
export const formatIsoDate = (date: Date) => format(date, 'yyyy-MM-dd')

/** `YYYY-MM-DD` shifted by whole calendar days (DST-safe: date arithmetic, never 86 400 000 ms). */
export const shiftIsoDate = (isoDate: string, days: number) => formatIsoDate(addDays(parseISO(isoDate), days))

/** Local wall clock → `Date`. A time that does not exist (the DST gap) resolves forward, like `new Date(y, m, d, h, min)`. */
export function combineLocal(isoDate: string, time: string): Date {
  const [year, month, day] = isoDate.split('-').map(Number)
  const [hours, minutes] = time.split(':').map(Number)
  return new Date(year, month - 1, day, hours, minutes)
}

/** What FullCalendar reports after a drag/resize/select. `end` is exclusive and `null` when the entry has no duration. */
export interface DraggedRange {
  start: Date
  end: Date | null
  allDay: boolean
}

/**
 * FullCalendar range → wire timing. An all-day entry always has an explicit exclusive end: FullCalendar reports `null`
 * for a one-day event, which the API does not accept, so it is that day + 1.
 */
export function timingFromRange({ start, end, allDay }: DraggedRange): CalendarTiming {
  if (allDay) {
    const startDate = formatIsoDate(start)
    const endDate = end ? formatIsoDate(end) : shiftIsoDate(startDate, 1)
    return { allDay: true, startAt: null, endAt: null, startDate, endDate: endDate > startDate ? endDate : shiftIsoDate(startDate, 1) }
  }
  return { allDay: false, startAt: formatOffsetIso(start), endAt: end ? formatOffsetIso(end) : null, startDate: null, endDate: null }
}

/** The dialog's editable timing: local strings, with an INCLUSIVE last day for all-day entries (what a person means). */
export interface TimingFields {
  allDay: boolean
  startDate: string
  startTime: string
  endDate: string
  endTime: string
}

export function timingFromFields(fields: TimingFields): CalendarTiming {
  if (fields.allDay) {
    const last = fields.endDate && fields.endDate >= fields.startDate ? fields.endDate : fields.startDate
    return { allDay: true, startAt: null, endAt: null, startDate: fields.startDate, endDate: shiftIsoDate(last, 1) }
  }
  const startAt = formatOffsetIso(combineLocal(fields.startDate, fields.startTime))
  const hasEnd = fields.endTime !== ''
  const endAt = hasEnd ? formatOffsetIso(combineLocal(fields.endDate || fields.startDate, fields.endTime)) : null
  return { allDay: false, startAt, endAt, startDate: null, endDate: null }
}

export function fieldsFromEntry(entry: Pick<CalendarEntry, 'allDay' | 'startAt' | 'endAt' | 'startDate' | 'endDate'>): TimingFields {
  if (entry.allDay && entry.startDate && entry.endDate) {
    return { allDay: true, startDate: entry.startDate, startTime: '09:00', endDate: shiftIsoDate(entry.endDate, -1), endTime: '' }
  }
  const start = new Date(entry.startAt ?? Date.now())
  const end = entry.endAt ? new Date(entry.endAt) : null
  return {
    allDay: false,
    startDate: formatIsoDate(start),
    startTime: format(start, 'HH:mm'),
    endDate: formatIsoDate(end ?? start),
    endTime: end ? format(end, 'HH:mm') : '',
  }
}

/** The dialog's default for a brand-new timed entry: the next full hour, one hour long. */
export function defaultFields(now = new Date()): TimingFields {
  const start = new Date(now)
  start.setHours(start.getHours() + 1, 0, 0, 0)
  const end = new Date(start.getTime() + 3_600_000)
  return {
    allDay: false,
    startDate: formatIsoDate(start),
    startTime: format(start, 'HH:mm'),
    endDate: formatIsoDate(end),
    endTime: format(end, 'HH:mm'),
  }
}

/** Fields for a range selected on the grid (all-day when the selection was on the all-day row / month grid). */
export function fieldsFromRange({ start, end, allDay }: DraggedRange): TimingFields {
  if (allDay) {
    const startDate = formatIsoDate(start)
    const lastDay = end ? shiftIsoDate(formatIsoDate(end), -1) : startDate
    return { allDay: true, startDate, startTime: '09:00', endDate: lastDay >= startDate ? lastDay : startDate, endTime: '' }
  }
  const finish = end ?? new Date(start.getTime() + 3_600_000)
  return {
    allDay: false,
    startDate: formatIsoDate(start),
    startTime: format(start, 'HH:mm'),
    endDate: formatIsoDate(finish),
    endTime: format(finish, 'HH:mm'),
  }
}

/** Fields that make an entry valid: the end is not before the start (same-day timed entries compare by time). */
export function fieldsAreOrdered(fields: TimingFields): boolean {
  if (fields.allDay) return !fields.endDate || fields.endDate >= fields.startDate
  if (fields.endTime === '') return true
  return combineLocal(fields.endDate || fields.startDate, fields.endTime).getTime() > combineLocal(fields.startDate, fields.startTime).getTime()
}

export const dateLocale = (language: string): Locale => (language.toLowerCase().startsWith('en') ? enUS : tr)

/**
 * "8 September Monday, 14:00 – 15:30" · "8 – 11 September" for all-day spans. FullCalendar's all-day end is exclusive; the
 * label has to be inclusive, and the last day is found by calendar arithmetic so a DST week is not a day short.
 */
export function formatWhen(entry: Pick<CalendarEntry, 'allDay' | 'startAt' | 'endAt' | 'startDate' | 'endDate'>, language = 'tr'): string {
  const locale = dateLocale(language)
  if (entry.allDay && entry.startDate && entry.endDate) {
    const start = parseISO(entry.startDate)
    const lastDay = addDays(parseISO(entry.endDate), -1)
    if (isSameDay(start, lastDay)) return format(start, 'd MMMM EEEE', { locale })
    return `${format(start, 'd MMMM', { locale })} – ${format(lastDay, 'd MMMM', { locale })}`
  }
  const start = new Date(entry.startAt ?? Date.now())
  const day = format(start, 'd MMMM EEEE', { locale })
  if (!entry.endAt) return `${day}, ${format(start, 'HH:mm')}`
  const end = new Date(entry.endAt)
  return isSameDay(start, end)
    ? `${day}, ${format(start, 'HH:mm')} – ${format(end, 'HH:mm')}`
    : `${day} ${format(start, 'HH:mm')} – ${format(end, 'd MMMM EEEE', { locale })} ${format(end, 'HH:mm')}`
}
