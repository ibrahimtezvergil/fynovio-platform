/** Number formatting shared by every chart on the gallery page. */

import { i18n } from '@/lib/i18n'

const tryFull = new Intl.NumberFormat('tr-TR', {
  style: 'currency',
  currency: 'TRY',
  maximumFractionDigits: 0,
})

const tryCompact = new Intl.NumberFormat('tr-TR', {
  style: 'currency',
  currency: 'TRY',
  notation: 'compact',
  maximumFractionDigits: 1,
})

const countCompact = new Intl.NumberFormat('tr-TR', {
  notation: 'compact',
  maximumFractionDigits: 1,
})

/** Full amount — tooltips and direct labels, where the exact figure is the point. */
export const money = (value: number) => tryFull.format(value)

/** Short amount — axis ticks, where the magnitude is the point. */
export const moneyShort = (value: number) => tryCompact.format(value)

export const count = (value: number) => countCompact.format(value)

/** Turkish writes the sign first: %24,5 */
export const percent = (value: number, fractionDigits = 0) =>
  `%${value.toLocaleString('tr-TR', {
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  })}`

export const days = (value: number) =>
  `${value.toLocaleString('tr-TR')} ${i18n.t('data.daysSuffix', { ns: 'demo-charts' })}`
