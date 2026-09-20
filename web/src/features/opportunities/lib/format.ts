import { i18n } from '@/lib/i18n'

/** Money as the user's language writes it. A code Intl rejects still renders (amount + code) instead of throwing. */
export function formatMoney(amount: number | null | undefined, currency: string | null | undefined): string | null {
  if (amount == null) return null
  try {
    return new Intl.NumberFormat(i18n.language, { style: 'currency', currency: currency ?? undefined }).format(amount)
  } catch {
    return `${new Intl.NumberFormat(i18n.language, { minimumFractionDigits: 2 }).format(amount)} ${currency ?? ''}`.trim()
  }
}

export function formatDate(value: string | null | undefined): string | null {
  if (!value) return null
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? null : new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(date)
}
