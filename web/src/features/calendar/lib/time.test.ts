import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { CalendarEntry } from '../schema'
import {
  combineLocal,
  defaultFields,
  fieldsAreOrdered,
  fieldsFromEntry,
  fieldsFromRange,
  formatIsoDate,
  formatOffsetIso,
  formatWhen,
  shiftIsoDate,
  timingFromFields,
  timingFromRange,
} from './time'

const useTimezone = (zone: string) => vi.stubEnv('TZ', zone)
beforeEach(() => useTimezone('Europe/Istanbul'))
afterEach(() => vi.unstubAllEnvs())

describe('formatOffsetIso', () => {
  it('renders the given offset explicitly, positive, negative, zero and fractional', () => {
    const instant = new Date('2026-09-21T06:00:00Z')
    expect(formatOffsetIso(instant, 180)).toBe('2026-09-21T09:00:00+03:00')
    expect(formatOffsetIso(instant, -300)).toBe('2026-09-21T01:00:00-05:00')
    expect(formatOffsetIso(instant, 0)).toBe('2026-09-21T06:00:00+00:00')
    expect(formatOffsetIso(instant, 330)).toBe('2026-09-21T11:30:00+05:30')
    expect(formatOffsetIso(instant, -570)).toBe('2026-09-20T20:30:00-09:30')
  })

  it('round-trips to the same instant', () => {
    const instant = new Date('2026-09-21T06:00:00Z')
    for (const offset of [180, -300, 0, 330, -570]) {
      expect(new Date(formatOffsetIso(instant, offset)).getTime()).toBe(instant.getTime())
    }
  })

  it('uses the browser offset at that instant by default (Istanbul: +03:00 all year)', () => {
    expect(formatOffsetIso(new Date('2026-01-15T10:00:00Z'))).toBe('2026-01-15T13:00:00+03:00')
    expect(formatOffsetIso(new Date('2026-07-15T10:00:00Z'))).toBe('2026-07-15T13:00:00+03:00')
  })

  it('DST boundary: each side of the New York spring-forward change gets its own offset (negative offsets)', () => {
    useTimezone('America/New_York')
    expect(formatOffsetIso(new Date('2026-03-08T06:59:00Z'))).toBe('2026-03-08T01:59:00-05:00')
    expect(formatOffsetIso(new Date('2026-03-08T07:00:00Z'))).toBe('2026-03-08T03:00:00-04:00')
  })

  it('DST boundary: fall-back hour is unambiguous because the offset is explicit', () => {
    useTimezone('America/New_York')
    const first = formatOffsetIso(new Date('2026-11-01T05:30:00Z')) // 01:30 EDT
    const second = formatOffsetIso(new Date('2026-11-01T06:30:00Z')) // 01:30 EST
    expect(first).toBe('2026-11-01T01:30:00-04:00')
    expect(second).toBe('2026-11-01T01:30:00-05:00')
    expect(first).not.toBe(second)
  })
})

describe('calendar-day arithmetic', () => {
  it('shiftIsoDate crosses month, year and leap boundaries', () => {
    expect(shiftIsoDate('2026-09-30', 1)).toBe('2026-10-01')
    expect(shiftIsoDate('2026-12-31', 1)).toBe('2027-01-01')
    expect(shiftIsoDate('2028-02-28', 1)).toBe('2028-02-29')
    expect(shiftIsoDate('2026-03-01', -1)).toBe('2026-02-28')
  })

  it('shiftIsoDate is not a day short or long across a DST change', () => {
    useTimezone('America/New_York')
    expect(shiftIsoDate('2026-03-08', 1)).toBe('2026-03-09')
    expect(shiftIsoDate('2026-11-01', 1)).toBe('2026-11-02')
    expect(shiftIsoDate('2026-11-02', -1)).toBe('2026-11-01')
  })

  it('combineLocal resolves a wall-clock time that does not exist (the DST gap) forward, into a valid offset', () => {
    useTimezone('America/New_York')
    // 02:30 on 8 March 2026 never happens in New York: the clocks jump 02:00 → 03:00.
    expect(formatOffsetIso(combineLocal('2026-03-08', '02:30'))).toBe('2026-03-08T03:30:00-04:00')
    expect(formatOffsetIso(combineLocal('2026-03-08', '01:30'))).toBe('2026-03-08T01:30:00-05:00')
  })

  it('combineLocal reads a wall clock in the browser zone', () => {
    expect(formatIsoDate(combineLocal('2026-09-21', '23:30'))).toBe('2026-09-21')
    expect(combineLocal('2026-09-21', '09:15').getHours()).toBe(9)
    expect(combineLocal('2026-09-21', '09:15').getMinutes()).toBe(15)
  })
})

describe('timingFromRange (drag / resize / select)', () => {
  it('timed range keeps both instants with offsets', () => {
    const timing = timingFromRange({ start: new Date('2026-09-21T06:00:00Z'), end: new Date('2026-09-21T07:30:00Z'), allDay: false })
    expect(timing).toEqual({
      allDay: false,
      startAt: '2026-09-21T09:00:00+03:00',
      endAt: '2026-09-21T10:30:00+03:00',
      startDate: null,
      endDate: null,
    })
  })

  it('a timed entry without an end stays a point in time', () => {
    expect(timingFromRange({ start: new Date('2026-09-21T06:00:00Z'), end: null, allDay: false }).endAt).toBeNull()
  })

  it('an all-day range keeps FullCalendar’s exclusive end', () => {
    expect(timingFromRange({ start: new Date(2026, 8, 8), end: new Date(2026, 8, 12), allDay: true })).toEqual({
      allDay: true,
      startAt: null,
      endAt: null,
      startDate: '2026-09-08',
      endDate: '2026-09-12',
    })
  })

  it('an all-day entry FullCalendar reports without an end becomes one day (start + 1)', () => {
    expect(timingFromRange({ start: new Date(2026, 8, 30), end: null, allDay: true })).toMatchObject({ startDate: '2026-09-30', endDate: '2026-10-01' })
  })

  it('never produces an all-day end on or before the start', () => {
    expect(timingFromRange({ start: new Date(2026, 8, 8), end: new Date(2026, 8, 8), allDay: true }).endDate).toBe('2026-09-09')
  })
})

describe('form fields ⇄ wire timing', () => {
  it('a one-day all-day entry is start → start + 1 (exclusive), and reads back inclusive', () => {
    const fields = { allDay: true, startDate: '2026-09-21', startTime: '09:00', endDate: '2026-09-21', endTime: '' }
    const timing = timingFromFields(fields)
    expect(timing).toMatchObject({ allDay: true, startDate: '2026-09-21', endDate: '2026-09-22', startAt: null, endAt: null })
    expect(fieldsFromEntry({ ...timing, allDay: true })).toMatchObject({ allDay: true, startDate: '2026-09-21', endDate: '2026-09-21' })
  })

  it('a multi-day all-day span includes its last day', () => {
    const timing = timingFromFields({ allDay: true, startDate: '2026-09-08', startTime: '09:00', endDate: '2026-09-11', endTime: '' })
    expect(timing.endDate).toBe('2026-09-12')
  })

  it('an all-day entry with an empty or earlier last day collapses to one day', () => {
    expect(timingFromFields({ allDay: true, startDate: '2026-09-08', startTime: '', endDate: '', endTime: '' }).endDate).toBe('2026-09-09')
    expect(timingFromFields({ allDay: true, startDate: '2026-09-08', startTime: '', endDate: '2026-09-01', endTime: '' }).endDate).toBe('2026-09-09')
  })

  it('a timed entry is sent with offsets; an empty end time means no end', () => {
    const timed = timingFromFields({ allDay: false, startDate: '2026-09-21', startTime: '09:00', endDate: '2026-09-21', endTime: '10:30' })
    expect(timed).toEqual({ allDay: false, startAt: '2026-09-21T09:00:00+03:00', endAt: '2026-09-21T10:30:00+03:00', startDate: null, endDate: null })
    expect(timingFromFields({ allDay: false, startDate: '2026-09-21', startTime: '09:00', endDate: '2026-09-21', endTime: '' }).endAt).toBeNull()
  })

  it('a timed entry can end on another day', () => {
    expect(timingFromFields({ allDay: false, startDate: '2026-09-21', startTime: '22:00', endDate: '2026-09-22', endTime: '01:00' }).endAt).toBe(
      '2026-09-22T01:00:00+03:00',
    )
  })

  it('reads a timed entry stored in UTC back as local wall-clock fields', () => {
    const entry = { allDay: false, startAt: '2026-09-21T06:00:00+00:00', endAt: '2026-09-21T07:30:00+00:00', startDate: null, endDate: null }
    expect(fieldsFromEntry(entry)).toEqual({ allDay: false, startDate: '2026-09-21', startTime: '09:00', endDate: '2026-09-21', endTime: '10:30' })
  })

  it('reads a negative-offset instant correctly', () => {
    const entry = { allDay: false, startAt: '2026-09-21T23:30:00-05:00', endAt: null, startDate: null, endDate: null }
    // 23:30-05:00 is 04:30Z next day = 07:30 in Istanbul.
    expect(fieldsFromEntry(entry)).toMatchObject({ startDate: '2026-09-22', startTime: '07:30', endTime: '' })
  })

  it('fieldsAreOrdered rejects an end that is not after the start', () => {
    const base = { allDay: false, startDate: '2026-09-21', startTime: '10:00', endDate: '2026-09-21', endTime: '10:00' }
    expect(fieldsAreOrdered(base)).toBe(false)
    expect(fieldsAreOrdered({ ...base, endTime: '09:59' })).toBe(false)
    expect(fieldsAreOrdered({ ...base, endTime: '10:01' })).toBe(true)
    expect(fieldsAreOrdered({ ...base, endTime: '' })).toBe(true)
    expect(fieldsAreOrdered({ ...base, endDate: '2026-09-22', endTime: '01:00' })).toBe(true)
    expect(fieldsAreOrdered({ allDay: true, startDate: '2026-09-21', startTime: '', endDate: '2026-09-20', endTime: '' })).toBe(false)
  })

  it('fieldsFromRange turns FullCalendar’s exclusive all-day end into an inclusive last day', () => {
    expect(fieldsFromRange({ start: new Date(2026, 8, 8), end: new Date(2026, 8, 12), allDay: true })).toMatchObject({ startDate: '2026-09-08', endDate: '2026-09-11' })
    expect(fieldsFromRange({ start: new Date(2026, 8, 8), end: new Date(2026, 8, 9), allDay: true })).toMatchObject({ startDate: '2026-09-08', endDate: '2026-09-08' })
  })

  it('defaultFields is the next full hour, one hour long', () => {
    expect(defaultFields(new Date(2026, 8, 21, 9, 20))).toEqual({
      allDay: false,
      startDate: '2026-09-21',
      startTime: '10:00',
      endDate: '2026-09-21',
      endTime: '11:00',
    })
  })
})

describe('formatWhen', () => {
  const timed = (startAt: string, endAt: string | null): Pick<CalendarEntry, 'allDay' | 'startAt' | 'endAt' | 'startDate' | 'endDate'> => ({
    allDay: false,
    startAt,
    endAt,
    startDate: null,
    endDate: null,
  })

  it('timed, same day', () => {
    expect(formatWhen(timed('2026-09-21T06:00:00+00:00', '2026-09-21T07:30:00+00:00'), 'en')).toBe('21 September Monday, 09:00 – 10:30')
  })

  it('timed without end', () => {
    expect(formatWhen(timed('2026-09-21T06:00:00+00:00', null), 'en')).toBe('21 September Monday, 09:00')
  })

  it('all-day single day reads inclusively', () => {
    expect(formatWhen({ allDay: true, startAt: null, endAt: null, startDate: '2026-09-21', endDate: '2026-09-22' }, 'en')).toBe('21 September Monday')
  })

  it('all-day span shows the inclusive last day, also across a month and a DST change', () => {
    expect(formatWhen({ allDay: true, startAt: null, endAt: null, startDate: '2026-09-29', endDate: '2026-10-02' }, 'en')).toBe('29 September – 1 October')
    useTimezone('America/New_York')
    expect(formatWhen({ allDay: true, startAt: null, endAt: null, startDate: '2026-11-01', endDate: '2026-11-03' }, 'en')).toBe('1 November – 2 November')
  })

  it('uses the Turkish locale for tr', () => {
    expect(formatWhen({ allDay: true, startAt: null, endAt: null, startDate: '2026-09-21', endDate: '2026-09-22' }, 'tr')).toBe('21 Eylül Pazartesi')
  })
})
