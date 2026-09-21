import { describe, expect, it } from 'vitest'
import { i18n } from '@/lib/i18n'
import { entryFormSchema } from './form'

const schema = entryFormSchema(i18n.getFixedT('en', 'calendar'))
const valid = { title: 'Call', notes: '', color: '#3c8cf0', allDay: false, startDate: '2026-09-21', startTime: '09:00', endDate: '2026-09-21', endTime: '10:00' }
const issues = (values: Record<string, unknown>) => {
  const result = schema.safeParse({ ...valid, ...values })
  return result.success ? [] : result.error.issues.map((issue) => [String(issue.path[0]), issue.message] as const)
}

describe('entryFormSchema', () => {
  it('accepts a valid timed and all-day form', () => {
    expect(schema.safeParse(valid).success).toBe(true)
    expect(schema.safeParse({ ...valid, allDay: true, startTime: '', endTime: '', endDate: '2026-09-23' }).success).toBe(true)
  })

  it('trims the title and rejects an empty, over-long or multi-line one', () => {
    expect(schema.parse({ ...valid, title: '  Call  ' }).title).toBe('Call')
    expect(issues({ title: '   ' })).toEqual([['title', 'A title is required.']])
    expect(issues({ title: 'x'.repeat(201) })[0][0]).toBe('title')
    expect(schema.safeParse({ ...valid, title: 'x'.repeat(200) }).success).toBe(true)
    expect(issues({ title: 'a\nb' })[0][0]).toBe('title')
  })

  it('caps notes at 4000 characters', () => {
    expect(schema.safeParse({ ...valid, notes: 'x'.repeat(4000) }).success).toBe(true)
    expect(issues({ notes: 'x'.repeat(4001) })[0][0]).toBe('notes')
  })

  it('requires a lowercase #rrggbb colour', () => {
    expect(issues({ color: 'red' })[0][0]).toBe('color')
    expect(issues({ color: '#ABCDEF' })[0][0]).toBe('color')
  })

  it('a timed entry needs a start time; an end time is optional; an inverted end is rejected on the end time', () => {
    expect(issues({ startTime: '' })).toEqual([['startTime', 'Enter a time.']])
    expect(schema.safeParse({ ...valid, endTime: '' }).success).toBe(true)
    expect(issues({ endTime: '09:00' })).toEqual([['endTime', 'The end must be after the start.']])
    expect(issues({ endTime: '08:30' })[0][0]).toBe('endTime')
  })

  it('a timed entry may end on a later day, even at an earlier clock time', () => {
    expect(schema.safeParse({ ...valid, startTime: '22:00', endDate: '2026-09-22', endTime: '01:00' }).success).toBe(true)
    expect(issues({ endDate: '2026-09-20' })[0][0]).toBe('endDate')
  })

  it('an all-day entry ignores the time fields and rejects a last day before the first', () => {
    expect(schema.safeParse({ ...valid, allDay: true, startTime: 'garbage', endTime: 'garbage' }).success).toBe(true)
    expect(issues({ allDay: true, endDate: '2026-09-20' })).toEqual([['endDate', 'The end must be after the start.']])
  })

  it('requires a valid start date', () => {
    expect(issues({ startDate: '' })[0][0]).toBe('startDate')
  })
})
