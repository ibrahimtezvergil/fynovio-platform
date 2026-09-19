import { create } from 'zustand'
import { i18n } from '@/lib/i18n'
import type { SettingsValues } from '@/features/settings/schema'

/**
 * `jobTitle`/`signature` resolve from the `settings` catalog at module-load
 * time via the shared `i18n` instance (a store's initial state is plain data,
 * not a component) — see `stageMeta()` in `StageBadge.tsx` for the same
 * accepted tradeoff elsewhere.
 */
const SAVED: SettingsValues = {
  name: 'Deniz Kaya',
  jobTitle: i18n.t('seed.jobTitle', { ns: 'settings' }),
  email: 'deniz@fynovio.com',
  phone: '+90 532 000 00 00',
  timezone: 'Europe/Istanbul',
  currency: 'TRY',
  signature: i18n.t('seed.signature', { ns: 'settings' }),
  channel: 'app',
  notifications: {
    stageChange: true,
    quoteViewed: true,
    weeklyDigest: false,
    closingSoon: true,
  },
}

interface SettingsState {
  /** The last committed copy — the form's `defaultValues`, nothing more. */
  saved: SettingsValues
  save: (values: SettingsValues) => void
}

/**
 * The draft deliberately does not live here: react-hook-form owns it, which is
 * what makes validation, dirty tracking and discard free. Once a backend
 * exists this store is replaced outright by a query plus a mutation.
 */
export const useSettingsStore = create<SettingsState>((set) => ({
  saved: SAVED,
  save: (values) => set({ saved: values }),
}))
