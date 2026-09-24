import { describe, expect, it } from 'vitest'
import { i18n } from '@/lib/i18n'
import enOpportunities from '@/locales/en/opportunities'
import trOpportunities from '@/locales/tr/opportunities'
import { addLineFormSchema, availableActionsSchema, createOpportunityFormSchema, endOfLocalDay, opportunitySchema, opportunitySummarySchema, openFormSchema, reasonFormSchema } from './schema'

const t = i18n.getFixedT('tr', 'opportunities')

describe('lifecycle wire adapter', () => {
  it.each([[0, 'Draft'], [1, 'Open'], [2, 'Won'], [3, 'Lost']])('integer %i → %s', (wire, status) => {
    expect(opportunitySummarySchema.parse({ id: 1, status: wire }).status).toBe(status)
  })

  it('also accepts the status name, and rejects anything else', () => {
    expect(opportunitySummarySchema.parse({ id: 1, status: 'Won' }).status).toBe('Won')
    expect(opportunitySummarySchema.safeParse({ id: 1, status: 9 }).success).toBe(false)
    expect(opportunitySummarySchema.safeParse({ id: 1, status: 'Waiting' }).success).toBe(false)
  })
})

describe('opportunitySchema', () => {
  it('tolerates omitted (masked) fields — an absent field stays absent, it is never reconstructed', () => {
    const parsed = opportunitySchema.parse({ id: 1, status: 0, rowVersion: 1 })
    expect(parsed.estimatedAmount).toBeUndefined()
    expect(parsed.partyId).toBeUndefined()
    expect(parsed.lines).toEqual([])
  })

  it('requires the concurrency token', () => {
    expect(opportunitySchema.safeParse({ id: 1, status: 0 }).success).toBe(false)
  })
})

describe('availableActionsSchema', () => {
  it('requires the full projection (a partial one must not be treated as "allowed")', () => {
    expect(availableActionsSchema.safeParse({ canWin: true }).success).toBe(false)
  })
})

describe('form schemas', () => {
  const create = createOpportunityFormSchema(t)

  it('accepts a valid create form and normalises the currency', () => {
    expect(create.parse({ partyId: '1001', currency: ' try ', estimatedAmount: '250.5' })).toEqual({ partyId: 1001, currency: 'TRY', estimatedAmount: 250.5 })
  })

  it('accepts Turkish decimal and thousands separators, while keeping blank distinct from zero', () => {
    expect(create.parse({ partyId: '1001', currency: 'TRY', estimatedAmount: '1250,50' }).estimatedAmount).toBe(1250.5)
    expect(create.parse({ partyId: '1001', currency: 'TRY', estimatedAmount: '1.250,50' }).estimatedAmount).toBe(1250.5)
    expect(create.parse({ partyId: '1001', currency: 'TRY', estimatedAmount: '0' }).estimatedAmount).toBe(0)
    expect(create.safeParse({ partyId: '1001', currency: 'TRY', estimatedAmount: '' }).success).toBe(false)
  })

  it.each([
    [{ partyId: '', currency: 'TRY', estimatedAmount: '1' }, 'partyId'],
    [{ partyId: '0', currency: 'TRY', estimatedAmount: '1' }, 'partyId'],
    [{ partyId: '1.5', currency: 'TRY', estimatedAmount: '1' }, 'partyId'],
    [{ partyId: '1', currency: 'TR', estimatedAmount: '1' }, 'currency'],
    [{ partyId: '1', currency: 'GBP', estimatedAmount: '1' }, 'currency'],
    [{ partyId: '1', currency: 'TRY', estimatedAmount: '-1' }, 'estimatedAmount'],
    [{ partyId: '1', currency: 'TRY', estimatedAmount: '1.234' }, 'estimatedAmount'],
    [{ partyId: '1', currency: 'TRY', estimatedAmount: 'abc' }, 'estimatedAmount'],
  ])('rejects %j on %s', (input, field) => {
    const result = create.safeParse(input)
    expect(result.success).toBe(false)
    expect(result.error?.issues.some((issue) => issue.path[0] === field)).toBe(true)
  })

  it('validates a line and a blank reason', () => {
    expect(addLineFormSchema(t).safeParse({ productId: '5', quantity: '2', unitPrice: '9.99', isOptional: false }).success).toBe(true)
    expect(addLineFormSchema(t).safeParse({ productId: '5', quantity: '0', unitPrice: '9.99', isOptional: false }).success).toBe(false)
    expect(reasonFormSchema(t).safeParse({ reason: '   ' }).success).toBe(false)
  })

  it('an expiry must be a future day; the day is sent as its END so "today" is not silently already past', () => {
    const today = new Date()
    const isoToday = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`
    expect(openFormSchema(t).safeParse({ expiryDate: '' }).success).toBe(false)
    expect(openFormSchema(t).safeParse({ expiryDate: '2000-01-01' }).success).toBe(false)
    expect(openFormSchema(t).safeParse({ expiryDate: '2999-01-01' }).success).toBe(true)
    expect(endOfLocalDay(isoToday).getHours()).toBe(23)
  })
})

describe('locale catalogs', () => {
  const keys = (value: unknown, prefix = ''): string[] =>
    typeof value === 'object' && value !== null
      ? Object.entries(value).flatMap(([key, child]) => keys(child, `${prefix}${key}.`))
      : [prefix.slice(0, -1)]

  it('English and Turkish define exactly the same keys', () => {
    expect(keys(trOpportunities).sort()).toEqual(keys(enOpportunities).sort())
  })
})
