import { describe, expect, it } from 'vitest'
import enCalendar from '@/locales/en/calendar'
import enOpportunities from '@/locales/en/opportunities'
import trCalendar from '@/locales/tr/calendar'
import trOpportunities from '@/locales/tr/opportunities'

/** Every leaf key as a dotted path. */
const keys = (value: unknown, prefix = ''): string[] =>
  Object.entries(value as Record<string, unknown>).flatMap(([key, child]) =>
    typeof child === 'object' && child !== null ? keys(child, `${prefix}${key}.`) : [`${prefix}${key}`],
  )

/** `{{placeholders}}` of every leaf, so a translation cannot silently drop one. */
const placeholders = (value: unknown): Record<string, string[]> =>
  Object.fromEntries(
    keys(value).map((path) => {
      const leaf = path.split('.').reduce<unknown>((node, part) => (node as Record<string, unknown>)[part], value) as string
      return [path, [...leaf.matchAll(/{{(\w+)}}/g)].map((match) => match[1]).sort()]
    }),
  )

describe('calendar locales', () => {
  it('tr and en have exactly the same keys', () => {
    expect(keys(trCalendar).sort()).toEqual(keys(enCalendar).sort())
  })

  it('tr and en interpolate the same placeholders in every string', () => {
    expect(placeholders(trCalendar)).toEqual(placeholders(enCalendar))
  })

  it('no string is empty', () => {
    for (const locale of [trCalendar, enCalendar]) for (const path of keys(locale)) expect(path.split('.').reduce<unknown>((n, p) => (n as Record<string, unknown>)[p], locale)).not.toBe('')
  })

  it('the removed event-kind vocabulary is gone from both languages', () => {
    for (const locale of [trCalendar, enCalendar]) {
      expect(locale).not.toHaveProperty('kind')
      expect(keys(locale).filter((path) => /kind|legend|eventKinds/i.test(path))).toEqual([])
      expect(locale.page).not.toHaveProperty('eventKindsHeading')
    }
  })

  it('the record count no longer hardcodes "4 views"', () => {
    expect(trCalendar.page.recordCount).not.toMatch(/görünüm/)
    expect(enCalendar.page.recordCount).not.toMatch(/views/)
  })

  it('the "Add to calendar" entry point is translated in the opportunities catalog', () => {
    expect(trOpportunities.detail.addToCalendar).toBeTruthy()
    expect(enOpportunities.detail.addToCalendar).toBeTruthy()
    expect(trOpportunities.detail.addToCalendar).not.toBe(enOpportunities.detail.addToCalendar)
  })
})
