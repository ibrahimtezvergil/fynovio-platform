import type {
  ColumnOrderState,
  ColumnSizingState,
  ColumnVisibilityState,
  PaginationState,
  SortingState,
} from '@tanstack/react-table'
import { useAuthStore } from '@/features/auth/store/useAuthStore'
import type { Density } from '@/types'

/**
 * The full shape of "what a table view is".
 *
 * `useTableSearchParams` owns pagination/sorting/the global filter in the
 * URL, `columnVisibilityFeature`/`columnSizingFeature`/`columnOrderingFeature`
 * each own one column-layout slice, and density lives in `useAppStore` — four
 * independent owners for one idea. This type is the vocabulary that lets a
 * saved view or a persisted layout describe more than one of them at once;
 * it does not change who owns each slice at runtime.
 *
 * `TFilter` is deliberately open: a URL-owned grid has no query object of its
 * own, while a grid built on the Filter AST (`demo-filters/lib/filterAst.ts`)
 * would use `FilterNode[]`. Most callers only ever populate a handful of
 * fields — hence `Partial<ViewState<...>>` at every call site.
 */
export interface ViewState<TFilter = unknown> {
  pagination: PaginationState
  sorting: SortingState
  filter: TFilter
  columnVisibility: ColumnVisibilityState
  columnSizing: ColumnSizingState
  columnOrder: ColumnOrderState
  density: Density
}

/** `<userId ?? 'anon'>.<tableId>[.<suffix>]` — one key per table per person. */
export function viewStateStorageKey(
  tableId: string,
  userId: string | null | undefined,
  suffix?: string,
): string {
  const base = `fynovio.viewState.${userId ?? 'anon'}.${tableId}`
  return suffix ? `${base}.${suffix}` : base
}

/**
 * Reads a JSON-serialised view-state slice back out of `localStorage`.
 *
 * Returns `null` on a cold key, corrupt JSON, or when storage itself throws
 * (private browsing, quota) — persistence here is a convenience, not a
 * guarantee, so every caller already has a default to fall back to.
 */
export function readPersistedViewState<T>(key: string): T | null {
  try {
    const raw = localStorage.getItem(key)
    return raw ? (JSON.parse(raw) as T) : null
  } catch {
    return null
  }
}

/** Same trade-off as the read side: a write that fails is silently skipped. */
export function writePersistedViewState<T>(key: string, value: T): void {
  try {
    localStorage.setItem(key, JSON.stringify(value))
  } catch {
    // best-effort persistence — a private window or a full quota must not break the grid.
  }
}

/**
 * The user half of a personal view's storage key.
 *
 * Lives here rather than in a calling feature because `features/auth` may
 * not be imported by another feature — shared library code is the one place
 * allowed to depend on it (`lib/permissions/usePermission.ts` does the same).
 */
export function useViewStateUserId(): string | null {
  return useAuthStore((state) => state.user?.id ?? null)
}
