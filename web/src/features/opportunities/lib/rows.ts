import type { OpportunityStatus, OpportunitySummary } from '../schema'
import { formatMoney } from './format'

/** One line of the list table: the API's summary with the names a reader needs resolved (each one best effort). */
export interface OpportunityRow {
  id: number
  partyId: number | null
  /** Display name of the customer; null when the caller cannot read party names (the table then shows the bare id). */
  party: string | null
  /** The assignee's subject: the list contract carries no display name for a principal. */
  owner: string | null
  status: OpportunityStatus
  stageId: number | null
  stage: string | null
  amount: number | null
  currency: string | null
  expiryDate: string | null
}

export function toRows(
  items: readonly OpportunitySummary[],
  partyNames: ReadonlyMap<number, string> | undefined,
  stageName: (versionId: number, stageId: number) => string | undefined,
): OpportunityRow[] {
  return items.map((item) => ({
    id: item.id,
    partyId: item.partyId ?? null,
    party: item.partyId == null ? null : (partyNames?.get(item.partyId) ?? null),
    owner: item.assignedPrincipalSubject ?? null,
    status: item.status,
    stageId: item.pipelineStageId ?? null,
    stage: item.pipelineDefinitionVersionId == null || item.pipelineStageId == null ? null : (stageName(item.pipelineDefinitionVersionId, item.pipelineStageId) ?? null),
    amount: item.estimatedAmount ?? null,
    currency: item.currency ?? null,
    expiryDate: item.expiryDate ?? null,
  }))
}

/** Filters that run over the loaded page. Status and paging are the server's and never appear here. */
export interface RowFilters {
  query: string
  owner: string | 'all'
  quarterOnly: boolean
}

export const NO_ROW_FILTERS: RowFilters = { query: '', owner: 'all', quarterOnly: false }

export const hasRowFilters = (filters: RowFilters) => filters.query.trim() !== '' || filters.owner !== 'all' || filters.quarterOnly

/** Start and end of the calendar quarter `now` falls in. */
function currentQuarter(now: Date): { start: Date; end: Date } {
  const quarter = Math.floor(now.getMonth() / 3)
  return {
    start: new Date(now.getFullYear(), quarter * 3, 1),
    end: new Date(now.getFullYear(), quarter * 3 + 3, 0, 23, 59, 59, 999),
  }
}

export function filterRows(rows: readonly OpportunityRow[], filters: RowFilters, now = new Date()): OpportunityRow[] {
  // `tr` casing, not the default: this is a Turkish UI and "İstanbul" only lowercases correctly under its rules.
  const needle = filters.query.trim().toLocaleLowerCase('tr')
  const { start, end } = currentQuarter(now)
  return rows.filter((row) => {
    if (filters.owner !== 'all' && row.owner !== filters.owner) return false
    if (filters.quarterOnly) {
      if (!row.expiryDate) return false
      const expiry = new Date(row.expiryDate)
      if (expiry < start || expiry > end) return false
    }
    if (needle === '') return true
    return [`#${row.id}`, row.party ?? '', row.owner ?? '', row.stage ?? ''].some((field) => field.toLocaleLowerCase('tr').includes(needle))
  })
}

/** Σ estimated amount, only when every counted row shares one currency — adding EUR to TRY says nothing. */
export function totalAmountLabel(rows: readonly OpportunityRow[]): string | null {
  const priced = rows.filter((row) => row.amount != null)
  if (priced.length === 0) return null
  const currency = priced[0].currency
  if (priced.some((row) => row.currency !== currency)) return null
  return formatMoney(
    priced.reduce((sum, row) => sum + (row.amount ?? 0), 0),
    currency,
  )
}
