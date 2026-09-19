import { useCallback, useState } from 'react'
import {
  readPersistedViewState,
  useViewStateUserId,
  viewStateStorageKey,
  writePersistedViewState,
} from '@/components/data-table'
import type { FilterState, SavedView, SortRule } from '@/features/demo-filters/types'

const TABLE_ID = 'demo-filters.orders'
const SUFFIX = 'personalViews'

function randomId(): string {
  return `personal-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
}

function loadPersonalViews(userId: string | null): SavedView[] {
  return readPersistedViewState<SavedView[]>(viewStateStorageKey(TABLE_ID, userId, SUFFIX)) ?? []
}

function storePersonalViews(userId: string | null, views: SavedView[]): void {
  writePersistedViewState(viewStateStorageKey(TABLE_ID, userId, SUFFIX), views)
}

/**
 * User-created views, alongside the curated `SAVED_VIEWS`. Personal only —
 * team-shared views need a backend to resolve ownership (see `SavedView.shared`);
 * this only persists to the current browser, keyed per user.
 */
export function usePersonalViews() {
  const userId = useViewStateUserId()
  const [loadedFor, setLoadedFor] = useState(userId)
  const [views, setViews] = useState<SavedView[]>(() => loadPersonalViews(userId))

  // Re-derive during render rather than in an effect — an identity switch is
  // a prop change, not an external event, so there is nothing to synchronize.
  if (loadedFor !== userId) {
    setLoadedFor(userId)
    setViews(loadPersonalViews(userId))
  }

  const save = useCallback(
    (label: string, filter: Partial<FilterState>, sort: SortRule[]): SavedView => {
      const view: SavedView = {
        id: randomId(),
        label,
        description: '',
        filter,
        sort,
        shared: false,
      }
      setViews((prev) => {
        const next = [...prev, view]
        storePersonalViews(userId, next)
        return next
      })
      return view
    },
    [userId],
  )

  const remove = useCallback(
    (id: string) => {
      setViews((prev) => {
        const next = prev.filter((view) => view.id !== id)
        storePersonalViews(userId, next)
        return next
      })
    },
    [userId],
  )

  return { views, save, remove }
}
