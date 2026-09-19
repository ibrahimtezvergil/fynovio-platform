import { buildEntityRef } from '@/lib/entity'
import { paths } from '@/routes/paths'
import type { Deal } from '@/types'
import type { EntityRef } from '@/types/entity'

export const money = new Intl.NumberFormat('tr-TR', {
  style: 'currency',
  currency: 'TRY',
  maximumFractionDigits: 0,
})

const dateFormat = new Intl.DateTimeFormat('tr-TR', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
})

/** A parked deal has no forecast date; the grid still owes the column a cell. */
export function formatCloseDate(value: string | null): string {
  return value ? dateFormat.format(new Date(value)) : '—'
}

/** One decimal, Turkish separator: 42,5 rather than 42.5. */
export const percent = new Intl.NumberFormat('tr-TR', {
  minimumFractionDigits: 1,
  maximumFractionDigits: 1,
})

/** Σ value × probability — what the pipeline is actually expected to be worth. */
export function weightedValue(deals: { value: number; probability: number }[]): number {
  return deals.reduce((sum, deal) => sum + (deal.value * deal.probability) / 100, 0)
}

/** Pipeline has no detail route yet, so a deal reference resolves to its shared workspace. */
export function dealToEntityRef(deal: Deal): EntityRef {
  return buildEntityRef('deal', deal.id, deal.title, paths.crmPipeline, deal.account)
}
