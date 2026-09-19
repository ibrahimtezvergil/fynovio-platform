import { create } from 'zustand'
import type { Stage } from '@/types'

export type PipelineView = 'grid' | 'board'
export type CloseWindow = 'all' | 'quarter'

/**
 * Filter state for the pipeline page. Density is deliberately absent: it is a
 * preference that outlives this page, so it lives in `useAppStore` and is
 * persisted there.
 */
interface PipelineState {
  view: PipelineView
  /** Free-text search over title, account and owner. */
  query: string
  /** Empty means "every stage" — the pill reads the count, not the emptiness. */
  stages: Stage[]
  owner: string | 'all'
  closeWindow: CloseWindow
  minValue: number
  setView: (view: PipelineView) => void
  setQuery: (query: string) => void
  toggleStage: (stage: Stage) => void
  setStages: (stages: Stage[]) => void
  setOwner: (owner: string | 'all') => void
  setCloseWindow: (window: CloseWindow) => void
  setMinValue: (value: number) => void
  reset: () => void
}

const INITIAL = {
  view: 'grid' as const,
  query: '',
  stages: [] as Stage[],
  owner: 'all' as const,
  closeWindow: 'all' as const,
  minValue: 0,
}

export const usePipelineStore = create<PipelineState>((set) => ({
  ...INITIAL,
  setView: (view) => set({ view }),
  setQuery: (query) => set({ query }),
  toggleStage: (stage) =>
    set((state) => ({
      stages: state.stages.includes(stage)
        ? state.stages.filter((s) => s !== stage)
        : [...state.stages, stage],
    })),
  setStages: (stages) => set({ stages }),
  setOwner: (owner) => set({ owner }),
  setCloseWindow: (closeWindow) => set({ closeWindow }),
  setMinValue: (minValue) => set({ minValue }),
  reset: () => set(INITIAL),
}))
