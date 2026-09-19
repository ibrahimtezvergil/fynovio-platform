import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import { i18n } from '@/lib/i18n'

export const TIMEZONE_VALUES = ['Europe/Istanbul', 'Europe/Berlin', 'Europe/London'] as const
export type Timezone = (typeof TIMEZONE_VALUES)[number]

export const CURRENCY_VALUES = ['TRY', 'EUR', 'USD'] as const
export type Currency = (typeof CURRENCY_VALUES)[number]

/** Reactive label maps — re-render on a language change. Every value carries a label. */
export function useTimezoneLabels(): Record<Timezone, string> {
  const { t } = useTranslation('settings')
  return {
    'Europe/Istanbul': t('timezone.istanbul'),
    'Europe/Berlin': t('timezone.berlin'),
    'Europe/London': t('timezone.london'),
  }
}

export function useCurrencyLabels(): Record<Currency, string> {
  const { t } = useTranslation('settings')
  return {
    TRY: t('currency.try'),
    EUR: t('currency.eur'),
    USD: t('currency.usd'),
  }
}

export const NOTIFICATION_CHANNELS = ['app', 'email', 'digest'] as const
export type NotificationChannel = (typeof NOTIFICATION_CHANNELS)[number]

const notificationsSchema = z.object({
  stageChange: z.boolean(),
  quoteViewed: z.boolean(),
  weeklyDigest: z.boolean(),
  closingSoon: z.boolean(),
})

export type NotificationToggles = z.infer<typeof notificationsSchema>

const ts = (key: string) => i18n.t(key, { ns: 'settings' })

/**
 * Everything the settings form owns, and the only shape the store persists.
 *
 * Validation messages resolve from the `settings` catalog at module-load
 * time via the shared `i18n` instance (schemas are plain objects, not
 * components — no hook to re-run on a language change). See `stageMeta()` in
 * `StageBadge.tsx` for the same accepted tradeoff elsewhere.
 */
export const settingsSchema = z.object({
  name: z.string().trim().min(2, ts('validation.minChars2')).max(60, ts('validation.maxChars60')),
  jobTitle: z.string().trim().max(60, ts('validation.maxChars60')),
  email: z.email(ts('validation.email')),
  phone: z
    .string()
    .trim()
    .regex(/^[+\d][\d\s()-]{6,19}$/, ts('validation.phone')),
  timezone: z.enum(TIMEZONE_VALUES),
  currency: z.enum(CURRENCY_VALUES),
  signature: z.string().trim().max(240, ts('validation.maxChars240')),
  channel: z.enum(NOTIFICATION_CHANNELS),
  notifications: notificationsSchema,
})

export type SettingsValues = z.infer<typeof settingsSchema>

const MIN_PASSWORD_LENGTH = 12
const PASSWORD_RULE = () => i18n.t('validation.passwordRule', { ns: 'settings', min: MIN_PASSWORD_LENGTH })

/**
 * Changing a password is its own transaction, not part of the settings draft —
 * it is never counted as an unsaved change and never persisted with the rest.
 */
export const passwordSchema = z
  .object({
    currentPassword: z.string().min(1, ts('validation.currentPasswordRequired')),
    newPassword: z.string().min(MIN_PASSWORD_LENGTH, PASSWORD_RULE()).regex(/\d/, PASSWORD_RULE()),
    signOutOtherDevices: z.boolean(),
  })
  .refine((values) => values.newPassword !== values.currentPassword, {
    message: ts('validation.newPasswordMustDiffer'),
    path: ['newPassword'],
  })

export type PasswordValues = z.infer<typeof passwordSchema>
