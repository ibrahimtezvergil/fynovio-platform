import { describe, expect, it } from 'vitest'
import { accessibleLink, unavailableLink, wireAllDayEntry, wireEntry } from '@/test/calendar'
import { calendarEntrySchema, calendarListSchema, commandResultSchema } from './schema'

const ok = (value: unknown) => calendarEntrySchema.safeParse(value).success

describe('calendarEntrySchema — the frozen contract', () => {
  it('accepts a timed entry with a UTC instant and an all-day entry with exclusive dates', () => {
    expect(ok(wireEntry())).toBe(true)
    expect(ok(wireAllDayEntry())).toBe(true)
  })

  it('accepts a point in time (endAt null) and an offset other than UTC', () => {
    expect(ok(wireEntry({ endAt: null }))).toBe(true)
    expect(ok(wireEntry({ startAt: '2026-09-21T09:00:00+03:00', endAt: '2026-09-21T10:00:00-05:00' }))).toBe(true)
  })

  it('does not tighten beyond the contract: a rowVersion of 0 does not make a legitimate response throw', () => {
    expect(ok(wireEntry({ rowVersion: 0 }))).toBe(true)
  })

  it('tolerates a nullable field being omitted, treating it as null', () => {
    const { notes: _notes, endAt: _endAt, startDate: _startDate, endDate: _endDate, link: _link, ...rest } = wireEntry()
    const parsed = calendarEntrySchema.parse(rest)
    expect(parsed).toMatchObject({ notes: null, endAt: null, startDate: null, endDate: null, link: null })
  })

  describe('color flows into inline styles, so it must be a server-normalised #rrggbb', () => {
    it.each(['#3b82f6', '#000000', '#ffffff', '#a1b2c3'])('accepts %s', (color) => expect(ok(wireEntry({ color }))).toBe(true))
    it.each(['#3B82F6', '#fff', '#3b82f', '#3b82f6ff', '3b82f6', 'red', 'rgb(0,0,0)', 'url(javascript:alert(1))', '#3b82f6;background:url(x)', ''])(
      'rejects %j',
      (color) => expect(ok(wireEntry({ color }))).toBe(false),
    )
  })

  describe('timed vs all-day exclusivity', () => {
    it('a timed entry needs startAt, and carries no dates', () => {
      expect(ok(wireEntry({ startAt: null }))).toBe(false)
      expect(ok(wireEntry({ startDate: '2026-09-21', endDate: '2026-09-22' }))).toBe(false)
    })

    it('a timed entry ends after it starts', () => {
      expect(ok(wireEntry({ endAt: '2026-09-21T06:00:00+00:00' }))).toBe(false)
      expect(ok(wireEntry({ endAt: '2026-09-21T05:59:00+00:00' }))).toBe(false)
    })

    it('an all-day entry needs both dates, an exclusive end after the start, and no instants', () => {
      expect(ok(wireAllDayEntry({ startDate: null }))).toBe(false)
      expect(ok(wireAllDayEntry({ endDate: null }))).toBe(false)
      expect(ok(wireAllDayEntry({ endDate: '2026-09-21' }))).toBe(false)
      expect(ok(wireAllDayEntry({ endDate: '2026-09-20' }))).toBe(false)
      expect(ok(wireAllDayEntry({ startAt: '2026-09-21T06:00:00+00:00' }))).toBe(false)
    })

    it('rejects an offset-less timed instant and a malformed date', () => {
      expect(ok(wireEntry({ startAt: '2026-09-21T09:00:00' }))).toBe(false)
      expect(ok(wireAllDayEntry({ startDate: '21-09-2026' }))).toBe(false)
      expect(ok(wireEntry({ startAt: 'not a date' }))).toBe(false)
    })
  })

  describe('link states', () => {
    it('an accessible link carries its label and subtitle', () => {
      const parsed = calendarEntrySchema.parse(wireEntry({ link: accessibleLink() }))
      expect(parsed.link).toEqual({
        ref: { boundedContext: 'crm', entityType: 'opportunity', id: 17 },
        state: 'accessible',
        label: 'OPP-17 — Acme',
        subtitle: 'Proposal',
      })
    })

    it('an unavailable link has no label or subtitle (absent, not empty)', () => {
      const parsed = calendarEntrySchema.parse(wireEntry({ link: unavailableLink() }))
      expect(parsed.link).toEqual({ ref: { boundedContext: 'crm', entityType: 'opportunity', id: 17 }, state: 'unavailable', label: undefined, subtitle: undefined })
    })

    it('drops a label an unavailable link should never have carried, so no component can render it', () => {
      const parsed = calendarEntrySchema.parse(wireEntry({ link: { ...unavailableLink(), label: 'Secret deal', subtitle: 'Confidential' } }))
      expect(parsed.link?.label).toBeUndefined()
      expect(parsed.link?.subtitle).toBeUndefined()
    })

    it('rejects an unknown state and a link without its ref', () => {
      expect(ok(wireEntry({ link: { ...accessibleLink(), state: 'hidden' } }))).toBe(false)
      expect(ok(wireEntry({ link: { state: 'accessible', label: 'x' } }))).toBe(false)
    })
  })

  it('a list is `{ items }`, and one bad item fails the whole response rather than being silently skipped', () => {
    expect(calendarListSchema.safeParse({ items: [wireEntry(), wireAllDayEntry({ id: 43 })] }).success).toBe(true)
    expect(calendarListSchema.safeParse({ items: [wireEntry(), wireEntry({ color: 'red' })] }).success).toBe(false)
    expect(calendarListSchema.safeParse([wireEntry()]).success).toBe(false)
  })
})

describe('commandResultSchema', () => {
  it('is `{ id, rowVersion, replayed }`', () => {
    expect(commandResultSchema.parse({ id: 42, rowVersion: 4, replayed: false })).toEqual({ id: 42, rowVersion: 4, replayed: false })
    expect(commandResultSchema.safeParse({ id: 42, rowVersion: 4 }).success).toBe(false)
  })
})
