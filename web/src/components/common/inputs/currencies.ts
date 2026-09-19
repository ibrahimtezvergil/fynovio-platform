import { i18n } from '@/lib/i18n'

/**
 * The currencies a Turkish B2B desk actually quotes in, plus their symbols.
 * `label` reads whatever language is active at module-load time — it has no
 * live consumer today, so this is not worth making reactive.
 */
export const CURRENCIES = {
  TRY: { symbol: '₺', get label() { return i18n.t('currencies.try', { ns: 'common' }) } },
  USD: { symbol: '$', get label() { return i18n.t('currencies.usd', { ns: 'common' }) } },
  EUR: { symbol: '€', get label() { return i18n.t('currencies.eur', { ns: 'common' }) } },
  GBP: { symbol: '£', get label() { return i18n.t('currencies.gbp', { ns: 'common' }) } },
} as const

export type CurrencyCode = keyof typeof CURRENCIES

export const CURRENCY_CODES = Object.keys(CURRENCIES) as CurrencyCode[]

/** Money as it is read back — symbol, grouping and two decimals, in tr-TR. */
export function formatMoney(
  value: number,
  currency: CurrencyCode = 'TRY',
  locale = 'tr-TR',
): string {
  return new Intl.NumberFormat(locale, {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value)
}

/** Grouped decimals with no currency — line quantities, weights, hour counts. */
export function formatDecimal(value: number, fractionDigits = 2, locale = 'tr-TR'): string {
  return new Intl.NumberFormat(locale, {
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  }).format(value)
}

/** Rounds to whole kuruş, so accumulated line totals never drift a cent. */
export function roundMoney(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100
}
