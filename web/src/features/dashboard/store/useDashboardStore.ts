import { create } from 'zustand'

export type DateRange = 'current' | 'previous' | 'yearly'

interface DashboardState {
  range: DateRange
  setRange: (range: DateRange) => void
}

/**
 * View state only — server data belongs to React Query, not here.
 *
 * Density used to live here too. It is a preference that outlives the page and
 * is set from several of them, so it moved to `useAppStore` and is persisted.
 */
export const useDashboardStore = create<DashboardState>((set) => ({
  range: 'current',
  setRange: (range) => set({ range }),
}))
