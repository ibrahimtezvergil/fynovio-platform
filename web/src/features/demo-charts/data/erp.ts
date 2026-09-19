import { i18n } from '@/lib/i18n'

/**
 * Deterministic ERP sample data — finance, inventory, procurement and
 * production, in the shapes those modules actually report.
 *
 * Category labels (months, ageing bands, criteria, weeks, line names) are
 * resolved against the active language at import time — see `stageMeta()` in
 * `StageBadge.tsx` for why this module doesn't switch live.
 */

const t = (key: string) => i18n.t(key, { ns: 'demo-charts' })

/** Income, expense and the net that falls out of them. One currency, one axis. */
export interface CashFlowPoint {
  month: string
  income: number
  expense: number
  net: number
}

export const CASH_FLOW: CashFlowPoint[] = [
  { month: t('data.months.Nis'), income: 4_180_000, expense: 3_240_000, net: 940_000 },
  { month: t('data.months.May'), income: 4_420_000, expense: 3_610_000, net: 810_000 },
  { month: t('data.months.Haz'), income: 5_060_000, expense: 3_980_000, net: 1_080_000 },
  { month: t('data.months.Tem'), income: 4_740_000, expense: 4_310_000, net: 430_000 },
  { month: t('data.months.Agu'), income: 5_290_000, expense: 4_120_000, net: 1_170_000 },
  { month: t('data.months.Eyl'), income: 5_860_000, expense: 4_405_000, net: 1_455_000 },
]

/** Stock value per warehouse, split by production stage. */
export interface WarehouseStock {
  warehouse: string
  raw: number
  wip: number
  finished: number
}

export const WAREHOUSE_STOCK: WarehouseStock[] = [
  { warehouse: 'Gebze', raw: 3_240_000, wip: 1_480_000, finished: 2_960_000 },
  { warehouse: 'İzmir', raw: 2_110_000, wip: 940_000, finished: 2_380_000 },
  { warehouse: 'Adana', raw: 1_620_000, wip: 610_000, finished: 1_540_000 },
  { warehouse: 'Ankara', raw: 980_000, wip: 420_000, finished: 1_120_000 },
]

/** Ageing bands, oldest last. Magnitude along one hue — the ramp is the age. */
export interface AgingBand {
  key: string
  label: string
  value: number
}

export const STOCK_AGING: AgingBand[] = [
  { key: 'b0', label: t('data.stockAging.b0'), value: 6_420_000 },
  { key: 'b1', label: t('data.stockAging.b1'), value: 4_180_000 },
  { key: 'b2', label: t('data.stockAging.b2'), value: 2_240_000 },
  { key: 'b3', label: t('data.stockAging.b3'), value: 1_310_000 },
]

/** Two suppliers scored on the same five criteria, 0–100. */
export interface SupplierScore {
  criterion: string
  aegean: number
  nordic: number
}

export const SUPPLIER_SCORES: SupplierScore[] = [
  { criterion: t('data.supplierCriteria.delivery'), aegean: 92, nordic: 74 },
  { criterion: t('data.supplierCriteria.quality'), aegean: 86, nordic: 91 },
  { criterion: t('data.supplierCriteria.price'), aegean: 68, nordic: 88 },
  { criterion: t('data.supplierCriteria.communication'), aegean: 90, nordic: 72 },
  { criterion: t('data.supplierCriteria.flexibility'), aegean: 79, nordic: 64 },
]

/** Order-to-delivery lead time by week, against the promised SLA. */
export interface LeadTimePoint {
  week: string
  days: number
}

export const LEAD_TIME: LeadTimePoint[] = [
  { week: t('data.leadTimeWeeks.H27'), days: 6.8 },
  { week: t('data.leadTimeWeeks.H28'), days: 7.4 },
  { week: t('data.leadTimeWeeks.H29'), days: 8.9 },
  { week: t('data.leadTimeWeeks.H30'), days: 8.1 },
  { week: t('data.leadTimeWeeks.H31'), days: 6.2 },
  { week: t('data.leadTimeWeeks.H32'), days: 5.6 },
  { week: t('data.leadTimeWeeks.H33'), days: 5.9 },
  { week: t('data.leadTimeWeeks.H34'), days: 5.1 },
]

export const LEAD_TIME_SLA = 7

/** Each production line as one point: how hard it runs vs. what it scraps. */
export interface ProductionLine {
  line: string
  utilization: number
  defectRate: number
  output: number
}

export const PRODUCTION_LINES: ProductionLine[] = [
  { line: t('data.productionLines.line1'), utilization: 94, defectRate: 2.8, output: 18_400 },
  { line: t('data.productionLines.line2'), utilization: 88, defectRate: 1.6, output: 22_100 },
  { line: t('data.productionLines.line3'), utilization: 76, defectRate: 3.9, output: 12_600 },
  { line: t('data.productionLines.line4'), utilization: 82, defectRate: 1.1, output: 15_900 },
  { line: t('data.productionLines.line5'), utilization: 68, defectRate: 0.7, output: 26_800 },
  { line: t('data.productionLines.line6'), utilization: 58, defectRate: 4.6, output: 7_400 },
]
