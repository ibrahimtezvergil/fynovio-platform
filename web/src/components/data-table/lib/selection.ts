export interface SelectionState {
  /** `include` selects listed IDs; `exclude` selects every result except listed IDs. */
  mode: 'include' | 'exclude'
  ids: Set<string>
}

export const emptySelection = (): SelectionState => ({ mode: 'include', ids: new Set() })

export function isRowSelected(state: SelectionState, id: string): boolean {
  return state.mode === 'include' ? state.ids.has(id) : !state.ids.has(id)
}

export function resolveSelectionCount(state: SelectionState, totalCount: number): number {
  return state.mode === 'include' ? state.ids.size : Math.max(0, totalCount - state.ids.size)
}

/** Switches between explicit IDs and the compact “all results except these IDs” form. */
export function toggleSelectAllResults(state: SelectionState): SelectionState {
  return state.mode === 'exclude' ? emptySelection() : { mode: 'exclude', ids: new Set() }
}

/** The mock mutations still need explicit IDs until the backend exposes bulk-by-query endpoints. */
export function resolveSelectionIds<T extends { id: string }>(state: SelectionState, rows: readonly T[]): string[] {
  return rows.filter((row) => isRowSelected(state, row.id)).map((row) => row.id)
}
