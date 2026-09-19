import type { PaginationState, SortingState } from '@tanstack/react-table'

/**
 * URL codecs for the table state slices that must survive a refresh or a shared
 * link. Everything here is pure so it can be unit-tested without a router.
 *
 * Wire format, chosen to look hand-editable rather than machine-generated:
 *
 * ```
 * ?page=3&size=25&sort=-monthlySpend,name&q=nordwind
 * ```
 *
 * `page` is 1-based on the wire and 0-based in table state — the off-by-one
 * lives here and nowhere else.
 */
export interface TableSearchParamKeys {
  page: string
  size: string
  sort: string
  query: string
}

export const DEFAULT_PARAM_KEYS: TableSearchParamKeys = {
  page: 'page',
  size: 'size',
  sort: 'sort',
  query: 'q',
}

/** Namespaces the keys so two grids can share one URL: `grid.page`, `grid.q`… */
export function prefixParamKeys(prefix: string | undefined): TableSearchParamKeys {
  if (!prefix) return DEFAULT_PARAM_KEYS
  return {
    page: `${prefix}.${DEFAULT_PARAM_KEYS.page}`,
    size: `${prefix}.${DEFAULT_PARAM_KEYS.size}`,
    sort: `${prefix}.${DEFAULT_PARAM_KEYS.sort}`,
    query: `${prefix}.${DEFAULT_PARAM_KEYS.query}`,
  }
}

function parsePositiveInt(raw: string | null, fallback: number): number {
  const parsed = Number.parseInt(raw ?? '', 10)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback
}

export function parsePagination(
  params: URLSearchParams,
  keys: TableSearchParamKeys,
  defaultPageSize: number,
): PaginationState {
  return {
    pageIndex: parsePositiveInt(params.get(keys.page), 1) - 1,
    pageSize: parsePositiveInt(params.get(keys.size), defaultPageSize),
  }
}

/** `-id` means descending, matching the convention most REST APIs already use. */
export function parseSorting(params: URLSearchParams, keys: TableSearchParamKeys): SortingState {
  const raw = params.get(keys.sort)
  if (!raw) return []

  return raw
    .split(',')
    .map((token) => token.trim())
    .filter(Boolean)
    .map((token) =>
      token.startsWith('-') ? { id: token.slice(1), desc: true } : { id: token, desc: false },
    )
    .filter((entry) => entry.id.length > 0)
}

export function serializeSorting(sorting: SortingState): string {
  return sorting.map((entry) => (entry.desc ? `-${entry.id}` : entry.id)).join(',')
}

export function parseGlobalFilter(
  params: URLSearchParams,
  keys: TableSearchParamKeys,
): string {
  return params.get(keys.query) ?? ''
}

/** Drops the key entirely when the value is at its default — no `?page=1` noise. */
export function setOrDelete(params: URLSearchParams, key: string, value: string, fallback: string) {
  if (value === fallback || value === '') params.delete(key)
  else params.set(key, value)
}
