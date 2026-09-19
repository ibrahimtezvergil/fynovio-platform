import { i18n } from '@/lib/i18n'
import type { Stage } from '@/types'

/**
 * Deterministic CRM sample data, shaped the way a sales backend would return
 * it. Every series here is fed straight into a chart — no derived state, no
 * randomness, so two renders always draw the same picture.
 *
 * Category labels (months, funnel stages, lead sources, quarters) are
 * resolved against the active language at import time — see `stageMeta()` in
 * `StageBadge.tsx` for why this module doesn't switch live.
 */

const t = (key: string) => i18n.t(key, { ns: 'demo-charts' })

/** Rolling twelve months, revenue split by where it came from. */
export interface RevenuePoint {
  month: string
  newBusiness: number
  renewal: number
}

export const REVENUE_TREND: RevenuePoint[] = [
  { month: t('data.months.Eki'), newBusiness: 1_240_000, renewal: 2_180_000 },
  { month: t('data.months.Kas'), newBusiness: 1_410_000, renewal: 2_215_000 },
  { month: t('data.months.Ara'), newBusiness: 1_960_000, renewal: 2_290_000 },
  { month: t('data.months.Oca'), newBusiness: 1_120_000, renewal: 2_340_000 },
  { month: t('data.months.Sub'), newBusiness: 1_385_000, renewal: 2_402_000 },
  { month: t('data.months.Mar'), newBusiness: 1_640_000, renewal: 2_455_000 },
  { month: t('data.months.Nis'), newBusiness: 1_505_000, renewal: 2_510_000 },
  { month: t('data.months.May'), newBusiness: 1_820_000, renewal: 2_588_000 },
  { month: t('data.months.Haz'), newBusiness: 2_070_000, renewal: 2_640_000 },
  { month: t('data.months.Tem'), newBusiness: 1_740_000, renewal: 2_702_000 },
  { month: t('data.months.Agu'), newBusiness: 1_890_000, renewal: 2_766_000 },
  { month: t('data.months.Eyl'), newBusiness: 2_310_000, renewal: 2_845_000 },
]

/** The funnel, widest step first. `onhold` is not a step, so it is not here. */
export interface FunnelStep {
  stage: Exclude<Stage, 'onhold'>
  label: string
  count: number
  value: number
}

export const FUNNEL: FunnelStep[] = [
  { stage: 'new', label: t('data.funnel.new'), count: 248, value: 18_400_000 },
  { stage: 'contacted', label: t('data.funnel.contacted'), count: 176, value: 14_120_000 },
  { stage: 'quoted', label: t('data.funnel.quoted'), count: 104, value: 9_650_000 },
  { stage: 'meeting', label: t('data.funnel.meeting'), count: 61, value: 6_480_000 },
  { stage: 'ready', label: t('data.funnel.ready'), count: 34, value: 4_215_000 },
]

/** Closed-deal outcomes per quarter — three mutually exclusive buckets. */
export interface OutcomePoint {
  period: string
  won: number
  lost: number
  postponed: number
}

export const OUTCOMES: OutcomePoint[] = [
  { period: t('data.outcomePeriods.p1'), won: 42, lost: 26, postponed: 11 },
  { period: t('data.outcomePeriods.p2'), won: 38, lost: 31, postponed: 14 },
  { period: t('data.outcomePeriods.p3'), won: 51, lost: 24, postponed: 9 },
  { period: t('data.outcomePeriods.p4'), won: 58, lost: 22, postponed: 12 },
]

/** Where the pipeline comes from, ranked. Magnitude, not identity — one hue. */
export interface LeadSource {
  source: string
  deals: number
}

export const LEAD_SOURCES: LeadSource[] = [
  { source: t('data.leadSources.organic'), deals: 186 },
  { source: t('data.leadSources.referral'), deals: 142 },
  { source: t('data.leadSources.paidAds'), deals: 96 },
  { source: t('data.leadSources.tradeShow'), deals: 58 },
  { source: t('data.leadSources.coldCall'), deals: 31 },
]

/** Rep leaderboard — one measure, ranked, so the bar itself carries the value. */
export interface RepPerformance {
  rep: string
  revenue: number
  quotaPercent: number
}

export const REP_PERFORMANCE: RepPerformance[] = [
  { rep: 'Deniz Kaya', revenue: 4_820_000, quotaPercent: 121 },
  { rep: 'Selin Arslan', revenue: 4_150_000, quotaPercent: 104 },
  { rep: 'Jonas Weber', revenue: 3_390_000, quotaPercent: 92 },
  { rep: 'Mira Sandström', revenue: 2_940_000, quotaPercent: 81 },
  { rep: 'Kerem Yıldız', revenue: 2_280_000, quotaPercent: 66 },
]

/** Lead → customer conversion against the committed rate. */
export interface ConversionPoint {
  month: string
  rate: number
}

export const CONVERSION_TREND: ConversionPoint[] = [
  { month: t('data.months.Nis'), rate: 18.4 },
  { month: t('data.months.May'), rate: 19.1 },
  { month: t('data.months.Haz'), rate: 21.6 },
  { month: t('data.months.Tem'), rate: 20.2 },
  { month: t('data.months.Agu'), rate: 23.8 },
  { month: t('data.months.Eyl'), rate: 25.4 },
]

export const CONVERSION_TARGET = 22

/** Quarter-to-date quota attainment, as a single share of a whole. */
export const QUOTA_ATTAINMENT = {
  percent: 78,
  closed: 18_640_000,
  target: 23_900_000,
}
