import type { CalendarEntry, CalendarEntryBody } from '../schema'

/** The full-replace body for an entry as it stands (a `PUT` replaces every field, `link` included). */
export function entryToBody(entry: CalendarEntry): CalendarEntryBody {
  return {
    title: entry.title,
    notes: entry.notes,
    color: entry.color,
    allDay: entry.allDay,
    startAt: entry.startAt,
    endAt: entry.endAt,
    startDate: entry.startDate,
    endDate: entry.endDate,
    link: entry.link ? { ...entry.link.ref } : null,
  }
}
