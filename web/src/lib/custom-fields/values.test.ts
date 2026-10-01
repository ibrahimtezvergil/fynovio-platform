import { describe, expect, it } from 'vitest'
import { customFieldErrors } from './errors'
import type { CustomFieldDefinition } from './schema'
import { activeFields, filterableFields, formatCustomFieldValue, fromDraft, keyFromLabel, missingRequired, toDraft, toDrafts, toPayload } from './values'

const field = (overrides: Partial<CustomFieldDefinition>): CustomFieldDefinition => ({
  id: 1, fieldName: 'budget_code', label: 'Budget code', fieldType: 'text', isRequired: false,
  config: {}, status: 'Active', sortOrder: 0, rowVersion: 1, ...overrides,
})

const budget = field({ id: 1, fieldName: 'budget_code', isRequired: true, sortOrder: 20 })
const score = field({ id: 2, fieldName: 'score', label: 'Score', fieldType: 'decimal', config: { scale: 2 }, sortOrder: 10 })
const tags = field({ id: 3, fieldName: 'tags', label: 'Tags', fieldType: 'multi_select', config: { options: [{ key: 'a', label: 'Alpha', isDeprecated: false }, { key: 'b', label: 'Beta', isDeprecated: true }] } })
const legacy = field({ id: 4, fieldName: 'legacy', label: 'Legacy', status: 'Deprecated' })

describe('custom field values', () => {
  it('orders active fields and leaves deprecated ones out', () => {
    expect(activeFields([budget, score, tags, legacy]).map((definition) => definition.fieldName)).toEqual(['tags', 'score', 'budget_code'])
  })

  it('round-trips stored values through drafts into a payload without empty keys or deprecated fields', () => {
    const drafts = toDrafts([budget, score, tags], { budget_code: 'B-1', score: 12.5, tags: ['a'], legacy: 'kept' })
    expect(drafts).toEqual({ budget_code: 'B-1', score: '12.5', tags: ['a'] })

    expect(toPayload([budget, score, tags, legacy], { ...drafts, score: '', tags: [] })).toEqual({ budget_code: 'B-1' })
  })

  it('accepts a decimal comma and hands anything unparseable to the server as text', () => {
    expect(fromDraft(score, '3,75')).toBe(3.75)
    expect(fromDraft(score, 'abc')).toBe('abc')
    expect(fromDraft(field({ fieldType: 'boolean' }), false)).toBeUndefined()
  })

  it('reports required active fields left empty', () => {
    expect(missingRequired([budget, score], { budget_code: '  ', score: '' })).toEqual(['budget_code'])
  })

  it('formats option labels, booleans and numbers for display', () => {
    expect(formatCustomFieldValue(tags, ['a', 'b'], { yes: 'Yes', no: 'No' })).toBe('Alpha, Beta')
    expect(formatCustomFieldValue(field({ fieldType: 'boolean' }), true, { yes: 'Yes', no: 'No' })).toBe('Yes')
    expect(formatCustomFieldValue(score, 3.5, { yes: 'Yes', no: 'No' }, 'en')).toBe('3.50')
    expect(formatCustomFieldValue(score, undefined, { yes: 'Yes', no: 'No' })).toBe('')
  })

  it('derives a valid key from a Turkish label', () => {
    expect(keyFromLabel('Bütçe Kodu')).toBe('butce_kodu')
    expect(keyFromLabel('İl / Şehir')).toBe('il_sehir')
    expect(keyFromLabel('2026 hedef')).toBe('f_2026_hedef')
  })

  it('words a 422 by its machine code and ignores other errors', () => {
    const t = ((key: string) => key) as never
    expect(customFieldErrors(t, { status: 422, message: 'x', code: 'custom_field_invalid', fieldCodes: { score: ['out_of_range'] } }))
      .toEqual({ score: 'customFields.errors.out_of_range' })
    expect(customFieldErrors(t, { status: 409, message: 'x', code: 'concurrency_conflict' })).toEqual({})
  })
})

describe('reference fields', () => {
  const account = field({ id: 9, fieldName: 'account', label: 'Account', fieldType: 'reference', config: { target: { boundedContext: 'masterdata', entityType: 'party' } } })

  it('keeps a stored id as a draft even when this reader cannot see the record', () => {
    expect(toDraft(account, 7, { id: 7, accessible: true, label: 'Acme' })).toEqual({ id: 7, label: 'Acme', accessible: true })
    expect(toDraft(account, 7, { id: 7, accessible: false, label: 'leaked?' })).toEqual({ id: 7, label: null, accessible: false })
    expect(toDraft(account, 7)).toEqual({ id: 7, label: null, accessible: false })
    expect(toDraft(account, undefined)).toBe('')
    expect(toDraft(account, 'seven')).toBe('')
  })

  it('sends the bare id, and nothing when cleared', () => {
    expect(fromDraft(account, { id: 7, label: 'Acme', accessible: true })).toBe(7)
    expect(fromDraft(account, { id: 7, label: null, accessible: false })).toBe(7)
    expect(fromDraft(account, '')).toBeUndefined()
    expect(toPayload([account], { account: { id: 7, label: 'Acme', accessible: true } })).toEqual({ account: 7 })
  })

  it('shows the label when available and the caller\'s unavailable text otherwise — never the raw id', () => {
    const labels = { yes: 'Yes', no: 'No', unavailable: 'Not available' }
    expect(formatCustomFieldValue(account, 7, labels, 'en', { id: 7, accessible: true, label: 'Acme' })).toBe('Acme')
    expect(formatCustomFieldValue(account, 7, labels, 'en', { id: 7, accessible: false, label: null })).toBe('Not available')
    expect(formatCustomFieldValue(account, 7, labels, 'en')).toBe('Not available')
    expect(formatCustomFieldValue(account, undefined, labels, 'en')).toBe('')
  })

  it('is not filterable and counts as required when left empty', () => {
    expect(filterableFields([account])).toEqual([])
    expect(missingRequired([{ ...account, isRequired: true }], { account: '' })).toEqual(['account'])
    expect(missingRequired([{ ...account, isRequired: true }], { account: { id: 7, label: null, accessible: false } })).toEqual([])
  })
})
