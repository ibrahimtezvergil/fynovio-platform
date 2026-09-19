import { functionalUpdate, type OnChangeFn, type PaginationState, type SortingState } from '@tanstack/react-table'
import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import {
  parseGlobalFilter,
  parsePagination,
  parseSorting,
  prefixParamKeys,
  serializeSorting,
  setOrDelete,
} from '@/components/data-table/lib/urlState'

export interface UseTableSearchParamsOptions {
  /** Namespaces every key so two grids can live in one URL. */
  prefix?: string
  defaultPageSize?: number
  /** `true` (default) keeps paging out of the back-button history. */
  replaceHistory?: boolean
}

export interface TableSearchParamsResult {
  pagination: PaginationState
  sorting: SortingState
  globalFilter: string
  onPaginationChange: OnChangeFn<PaginationState>
  onSortingChange: OnChangeFn<SortingState>
  onGlobalFilterChange: OnChangeFn<string>
  /** Everything back to defaults, in one navigation. */
  reset: () => void
  hasActiveState: boolean
}

/**
 * Makes the URL the owner of pagination, sorting and the global filter.
 *
 * This is ownership option 4 from the v9 state guide — external `state` plus a
 * matching `on<Slice>Change` — with the browser URL standing in for `useState`.
 * The table never holds these slices; it reads them from `options.state` and
 * writes them back through these handlers, so a refresh, a shared link and the
 * back button all reproduce the same view.
 *
 * Two details make it safe:
 *
 * 1. Every setter resolves through `functionalUpdate`, because table APIs pass
 *    updater functions (`table.nextPage()` sends `old => ({ ...old })`), not
 *    plain values.
 * 2. The previous value is read from `prev` inside `setSearchParams`, never
 *    from a captured render value, so rapid successive updates can't clobber
 *    each other.
 */
export function useTableSearchParams({
  prefix,
  defaultPageSize = 10,
  replaceHistory = true,
}: UseTableSearchParamsOptions = {}): TableSearchParamsResult {
  const [searchParams, setSearchParams] = useSearchParams()
  const search = searchParams.toString()

  const keys = useMemo(() => prefixParamKeys(prefix), [prefix])

  // Memoized on the serialized URL, so the objects handed to `options.state`
  // keep their identity until the URL actually changes.
  const { pagination, sorting, globalFilter } = useMemo(() => {
    const params = new URLSearchParams(search)
    return {
      pagination: parsePagination(params, keys, defaultPageSize),
      sorting: parseSorting(params, keys),
      globalFilter: parseGlobalFilter(params, keys),
    }
  }, [search, keys, defaultPageSize])

  const commit = useCallback(
    (mutate: (params: URLSearchParams) => void) => {
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev)
          mutate(next)
          return next
        },
        { replace: replaceHistory },
      )
    },
    [setSearchParams, replaceHistory],
  )

  const onPaginationChange = useCallback<OnChangeFn<PaginationState>>(
    (updater) => {
      commit((params) => {
        const previous = parsePagination(params, keys, defaultPageSize)
        const next = functionalUpdate(updater, previous)
        setOrDelete(params, keys.page, String(next.pageIndex + 1), '1')
        setOrDelete(params, keys.size, String(next.pageSize), String(defaultPageSize))
      })
    },
    [commit, keys, defaultPageSize],
  )

  const onSortingChange = useCallback<OnChangeFn<SortingState>>(
    (updater) => {
      commit((params) => {
        const next = functionalUpdate(updater, parseSorting(params, keys))
        setOrDelete(params, keys.sort, serializeSorting(next), '')
        // A new order makes the old page number meaningless.
        params.delete(keys.page)
      })
    },
    [commit, keys],
  )

  const onGlobalFilterChange = useCallback<OnChangeFn<string>>(
    (updater) => {
      commit((params) => {
        const next = functionalUpdate(updater, parseGlobalFilter(params, keys)) ?? ''
        setOrDelete(params, keys.query, next, '')
        params.delete(keys.page)
      })
    },
    [commit, keys],
  )

  const reset = useCallback(() => {
    commit((params) => {
      params.delete(keys.page)
      params.delete(keys.size)
      params.delete(keys.sort)
      params.delete(keys.query)
    })
  }, [commit, keys])

  const hasActiveState =
    pagination.pageIndex > 0 ||
    pagination.pageSize !== defaultPageSize ||
    sorting.length > 0 ||
    globalFilter.length > 0

  return {
    pagination,
    sorting,
    globalFilter,
    onPaginationChange,
    onSortingChange,
    onGlobalFilterChange,
    reset,
    hasActiveState,
  }
}
