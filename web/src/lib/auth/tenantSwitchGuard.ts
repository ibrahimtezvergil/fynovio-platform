import { create } from 'zustand'

type TenantSwitchConfirmation = () => Promise<boolean>

interface TenantSwitchGuardState {
  confirm: TenantSwitchConfirmation | null
  setConfirmation: (confirmation: TenantSwitchConfirmation | null) => void
}

/**
 * A route can register a short-lived confirmation before the global tenant switcher replaces the
 * active tenant token. It is deliberately not persisted: a browser reload has no unsaved form.
 */
export const useTenantSwitchGuard = create<TenantSwitchGuardState>((set) => ({
  confirm: null,
  setConfirmation: (confirm) => set({ confirm }),
}))
