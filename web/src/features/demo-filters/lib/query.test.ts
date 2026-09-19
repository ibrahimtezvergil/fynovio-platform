import { describe, expect, it } from 'vitest'
import { applyFilter, applySort, describeFilter, formatMoney } from '@/features/demo-filters/lib/query'
import { EMPTY_FILTER, type OrderRecord } from '@/features/demo-filters/types'

const RECORDS: OrderRecord[] = [
  {
    id: 'o-1',
    account: 'Nordwind Lojistik',
    owner: 'Deniz Kaya',
    city: 'İstanbul',
    channel: 'direct',
    status: 'pending',
    amount: 1000,
    items: 3,
    createdAt: '2026-01-10',
    tags: ['acil'],
  },
  {
    id: 'o-2',
    account: 'Baltic Freight AB',
    owner: 'Selin Arslan',
    city: 'Ankara',
    channel: 'partner',
    status: 'paid',
    amount: 5000,
    items: 1,
    createdAt: '2026-02-15',
    tags: [],
  },
  {
    id: 'o-3',
    account: 'Ege Yapı Malzeme',
    owner: 'Deniz Kaya',
    city: 'İzmir',
    channel: 'online',
    status: 'cancelled',
    amount: 250,
    items: 8,
    createdAt: '2025-12-01',
    tags: ['iade'],
  },
]

describe('applyFilter', () => {
  it('returns every record when the filter is empty', () => {
    expect(applyFilter(RECORDS, EMPTY_FILTER)).toHaveLength(3)
  })

  it('an untouched multi-select narrows nothing', () => {
    const result = applyFilter(RECORDS, { ...EMPTY_FILTER, statuses: [] })
    expect(result).toEqual(RECORDS)
  })

  it('filters by status', () => {
    const result = applyFilter(RECORDS, { ...EMPTY_FILTER, statuses: ['paid'] })
    expect(result.map((r) => r.id)).toEqual(['o-2'])
  })

  it('filters by a case-insensitive Turkish-collated search across account/owner/city/id', () => {
    // "İzmir" lowercases to dotted "izmir" under tr-TR — not the dotless "ızmir".
    const result = applyFilter(RECORDS, { ...EMPTY_FILTER, search: 'izmir' })
    expect(result.map((r) => r.id)).toEqual(['o-3'])
  })

  it('filters by amount range, inclusive at both ends', () => {
    const result = applyFilter(RECORDS, { ...EMPTY_FILTER, amount: { min: 250, max: 1000 } })
    expect(result.map((r) => r.id).sort()).toEqual(['o-1', 'o-3'])
  })

  it('filters to only tagged records', () => {
    const result = applyFilter(RECORDS, { ...EMPTY_FILTER, onlyTagged: true })
    expect(result.map((r) => r.id).sort()).toEqual(['o-1', 'o-3'])
  })

  it('combines predicates with AND', () => {
    const result = applyFilter(RECORDS, {
      ...EMPTY_FILTER,
      owners: ['Deniz Kaya'],
      statuses: ['pending'],
    })
    expect(result.map((r) => r.id)).toEqual(['o-1'])
  })
})

describe('applySort', () => {
  it('sorts by a single rule', () => {
    const result = applySort(RECORDS, [{ field: 'amount', direction: 'asc' }])
    expect(result.map((r) => r.id)).toEqual(['o-3', 'o-1', 'o-2'])
  })

  it('applies rules in priority order, falling through only when the primary rule ties', () => {
    const tied: OrderRecord[] = [
      { ...RECORDS[0], id: 'a', account: 'Z Şirketi', amount: 100 },
      { ...RECORDS[0], id: 'b', account: 'A Şirketi', amount: 100 },
    ]
    const result = applySort(tied, [
      { field: 'amount', direction: 'asc' },
      { field: 'account', direction: 'asc' },
    ])
    expect(result.map((r) => r.id)).toEqual(['b', 'a'])
  })

  it('never mutates the input array', () => {
    const copy = [...RECORDS]
    applySort(RECORDS, [{ field: 'amount', direction: 'desc' }])
    expect(RECORDS).toEqual(copy)
  })
})

describe('describeFilter', () => {
  it('produces no chips for an empty filter', () => {
    expect(describeFilter(EMPTY_FILTER)).toEqual([])
  })

  it('produces one chip per selected status, not one chip for the whole field', () => {
    const chips = describeFilter({ ...EMPTY_FILTER, statuses: ['pending', 'paid'] })
    expect(chips.map((c) => c.key)).toEqual(['status-pending', 'status-paid'])
  })

  it("clearing a status chip removes only that status", () => {
    const chips = describeFilter({ ...EMPTY_FILTER, statuses: ['pending', 'paid'] })
    const pendingChip = chips.find((c) => c.key === 'status-pending')
    expect(pendingChip?.clear).toEqual({ statuses: ['paid'] })
  })
})

describe('formatMoney', () => {
  it('formats as Turkish lira with no decimals', () => {
    expect(formatMoney(1000)).toContain('1.000')
  })
})
