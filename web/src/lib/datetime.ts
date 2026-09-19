/**
 * Time formatting shared by anything that shows *when* something happened —
 * the notification center, activity feeds, audit trails, detail drawers.
 *
 * Everything takes an ISO string, because that is what the backend will send.
 */

import { i18n } from '@/lib/i18n'

const t = (key: string) => i18n.t(key, { ns: 'common' })

const LOCALE = 'tr-TR'

const relative = new Intl.RelativeTimeFormat(LOCALE, { numeric: 'auto' })
const timeOnly = new Intl.DateTimeFormat(LOCALE, { hour: '2-digit', minute: '2-digit' })
const dayLong = new Intl.DateTimeFormat(LOCALE, { day: 'numeric', month: 'long', year: 'numeric' })
const dayShort = new Intl.DateTimeFormat(LOCALE, { day: 'numeric', month: 'short' })

/** Largest unit first — the loop stops at the first one the gap fills. */
const UNITS: readonly (readonly [Intl.RelativeTimeFormatUnit, number])[] = [
  ['year', 31_536_000_000],
  ['month', 2_592_000_000],
  ['week', 604_800_000],
  ['day', 86_400_000],
  ['hour', 3_600_000],
  ['minute', 60_000],
]

/** "3 dakika önce", "dün", "2 ay önce". Anything under a minute is "şimdi". */
export function relativeTime(iso: string, now: number = Date.now()): string {
  const diff = new Date(iso).getTime() - now
  const distance = Math.abs(diff)
  if (distance < 60_000) return t('relativeTime.now')
  for (const [unit, ms] of UNITS) {
    if (distance >= ms) return relative.format(Math.round(diff / ms), unit)
  }
  return t('relativeTime.now')
}

/** "14:32" — the only part that varies inside one day's group. */
export function formatTime(iso: string): string {
  return timeOnly.format(new Date(iso))
}

/** "12 Eyl" — for a dense row that still needs a date. */
export function formatShortDate(iso: string): string {
  return dayShort.format(new Date(iso))
}

/**
 * The heading a feed puts above one day's entries. The two most recent days
 * get names rather than dates: a reader scanning a feed wants "Bugün" before
 * they want "6 Eylül 2026".
 */
export function formatDayHeading(iso: string, now: number = Date.now()): string {
  const days = calendarDaysBetween(new Date(iso), new Date(now))
  if (days === 0) return t('relativeTime.today')
  if (days === 1) return t('relativeTime.yesterday')
  return dayLong.format(new Date(iso))
}

/** Whole calendar days apart, ignoring the clock — 23:59 and 00:01 are 1 day. */
function calendarDaysBetween(a: Date, b: Date): number {
  const startA = new Date(a.getFullYear(), a.getMonth(), a.getDate()).getTime()
  const startB = new Date(b.getFullYear(), b.getMonth(), b.getDate()).getTime()
  return Math.round((startB - startA) / 86_400_000)
}

/** Groups an already-sorted feed by calendar day, keeping the incoming order. */
export function groupByDay<T>(entries: readonly T[], at: (entry: T) => string): [string, T[]][] {
  const groups = new Map<string, T[]>()
  for (const entry of entries) {
    const key = new Date(at(entry)).toDateString()
    const bucket = groups.get(key)
    if (bucket) bucket.push(entry)
    else groups.set(key, [entry])
  }
  return [...groups.values()].map((bucket) => [at(bucket[0]!), bucket])
}

/**
 * Anchored at module load, so mock fixtures read "5 dakika önce" forever
 * instead of drifting to "8 ay önce" the way a hard-coded date would.
 */
const ANCHOR = Date.now()

/** An ISO timestamp `minutes` before this session started. Mock data only. */
export function minutesAgo(minutes: number): string {
  return new Date(ANCHOR - minutes * 60_000).toISOString()
}

/** An ISO timestamp `minutes` after this session started. Mock data only. */
export function minutesAhead(minutes: number): string {
  return new Date(ANCHOR + minutes * 60_000).toISOString()
}
