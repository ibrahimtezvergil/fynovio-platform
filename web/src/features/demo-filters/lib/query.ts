import {
  ORDER_STATUSES,
  type FilterState,
  type OrderRecord,
  type SortRule,
} from '@/features/demo-filters/types'
import { applyFilterNodes, describeFilterNodes, filterStateToNodes } from '@/features/demo-filters/lib/filterAst'

const money = new Intl.NumberFormat('tr-TR', {
  style: 'currency',
  currency: 'TRY',
  maximumFractionDigits: 0,
})

const dayFormat = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'short' })

export const formatMoney = (value: number) => money.format(value)
export const formatDay = (iso: string) => dayFormat.format(new Date(iso))

/** Midnight-normalised, so a range's endpoints include their whole day. */
function dayValue(input: Date | string): number {
  const date = typeof input === 'string' ? new Date(input) : input
  return new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime()
}

/**
 * Every predicate is an AND, and an empty control is not a predicate.
 *
 * That second half is the rule people get wrong: an untouched multi-select
 * must mean "don't narrow on this", never "match nothing". A filter panel
 * that starts by hiding every row teaches users not to open it.
 */
export function applyFilter(records: readonly OrderRecord[], filter: FilterState): OrderRecord[] {
  return applyFilterNodes(filterStateToNodes(filter), records)
}

function compare(a: OrderRecord, b: OrderRecord, rule: SortRule): number {
  const sign = rule.direction === 'asc' ? 1 : -1
  switch (rule.field) {
    case 'amount':
      return (a.amount - b.amount) * sign
    case 'items':
      return (a.items - b.items) * sign
    case 'createdAt':
      return (dayValue(a.createdAt) - dayValue(b.createdAt)) * sign
    case 'account':
      // Turkish collation: "İ" and "ı" sort where a reader expects them to.
      return a.account.localeCompare(b.account, 'tr-TR') * sign
    case 'status':
      // Status is ordinal, not alphabetical — the process order is the order.
      return (ORDER_STATUSES.indexOf(a.status) - ORDER_STATUSES.indexOf(b.status)) * sign
  }
}

/**
 * Multi-sort: rules apply in priority order, and the first one that separates
 * two rows wins. `id` breaks the final tie so the order is total — without it,
 * two equal rows swap places on every unrelated re-render.
 */
export function applySort(records: readonly OrderRecord[], rules: readonly SortRule[]): OrderRecord[] {
  return [...records].sort((a, b) => {
    for (const rule of rules) {
      const result = compare(a, b, rule)
      if (result !== 0) return result
    }
    return a.id.localeCompare(b.id)
  })
}

/** One removable chip per *value*, not per field — a set of three narrows three ways. */
export interface FilterChip {
  key: string
  /** What the chip narrows, spelled out: "Durum: Beklemede". */
  label: string
  /** The patch that removes exactly this chip. */
  clear: Partial<FilterState>
}

/** Compatibility entry point for callers that still provide the flat form state. */
export function describeFilter(filter: FilterState): FilterChip[] {
  return describeFilterNodes(filterStateToNodes(filter))
}
