import { describe, expect, it } from 'vitest'
import { NO_ROW_FILTERS, filterRows, hasRowFilters, toRows, totalAmountLabel, type OpportunityRow } from './rows'

const row = (overrides: Partial<OpportunityRow> = {}): OpportunityRow => ({
  id: 1,
  partyId: 10,
  party: 'Acme',
  owner: 'account-7',
  status: 'Open',
  stageId: 30,
  stage: 'Qualification',
  amount: 100,
  currency: 'EUR',
  expiryDate: '2026-08-15T12:00:00Z',
  createdAt: null,
  updatedAt: null,
  totalAmount: null,
  needs: [],
  ...overrides,
})

describe('toRows', () => {
  it('resolves the stage inside the row’s own pipeline version', () => {
    const names = new Map([['3:30', 'Qualification'], ['4:30', 'Discovery']])
    const rows = toRows(
      [
        { id: 1, status: 'Open', pipelineDefinitionVersionId: 3, pipelineStageId: 30 },
        { id: 2, status: 'Open', pipelineDefinitionVersionId: 4, pipelineStageId: 30 },
      ],
      undefined,
      (versionId, stageId) => names.get(`${versionId}:${stageId}`),
    )
    expect(rows.map((entry) => entry.stage)).toEqual(['Qualification', 'Discovery'])
  })

  it('leaves names absent — never invented — when they cannot be resolved', () => {
    const [entry] = toRows([{ id: 1, status: 'Draft', partyId: 10, pipelineStageId: 30 }], undefined, () => undefined)
    expect(entry).toMatchObject({ party: null, partyId: 10, stage: null, stageId: 30, expiryDate: null })
  })
})

describe('filterRows', () => {
  const now = new Date('2026-08-20T10:00:00')

  it('keeps everything when no filter is set', () => {
    expect(hasRowFilters(NO_ROW_FILTERS)).toBe(false)
    expect(filterRows([row(), row({ id: 2 })], NO_ROW_FILTERS, now)).toHaveLength(2)
  })

  it('searches id, customer, owner and stage with Turkish casing', () => {
    const rows = [row({ id: 1, party: 'İstanbul Lojistik' }), row({ id: 2, party: 'Ege' })]
    expect(filterRows(rows, { ...NO_ROW_FILTERS, query: 'istanbul' }, now).map((entry) => entry.id)).toEqual([1])
    expect(filterRows(rows, { ...NO_ROW_FILTERS, query: '#2' }, now).map((entry) => entry.id)).toEqual([2])
  })

  it('narrows by owner', () => {
    const rows = [row({ id: 1, owner: 'a' }), row({ id: 2, owner: 'b' })]
    expect(filterRows(rows, { ...NO_ROW_FILTERS, owner: 'b' }, now).map((entry) => entry.id)).toEqual([2])
  })

  it('keeps only rows expiring in the current quarter, and drops rows with no expiry', () => {
    const rows = [
      row({ id: 1, expiryDate: '2026-09-30T12:00:00' }),
      row({ id: 2, expiryDate: '2026-10-01T12:00:00' }),
      row({ id: 3, expiryDate: null }),
    ]
    expect(filterRows(rows, { ...NO_ROW_FILTERS, quarterOnly: true }, now).map((entry) => entry.id)).toEqual([1])
  })
})

describe('totalAmountLabel', () => {
  it('sums rows that share a currency', () => {
    expect(totalAmountLabel([row({ amount: 100 }), row({ amount: 50 })])).toMatch(/150/)
  })

  it('refuses to add different currencies', () => {
    expect(totalAmountLabel([row({ currency: 'EUR' }), row({ currency: 'TRY' })])).toBeNull()
  })

  it('has no total when nothing carries an amount', () => {
    expect(totalAmountLabel([])).toBeNull()
    expect(totalAmountLabel([row({ amount: null })])).toBeNull()
  })
})
