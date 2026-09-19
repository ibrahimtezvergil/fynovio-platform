/**
 * Headless data-grid kit for TanStack Table v9.
 *
 * Nothing in here registers a feature or constructs a table. Each grid owns its
 * own `tableFeatures({ ... })` object and calls `useTable`; these components only
 * render whatever that instance exposes.
 */
export { DataTable, type DataTableProps } from '@/components/data-table/DataTable'
export { DataTableHeaderCell } from '@/components/data-table/DataTableHeaderCell'
export { DataTablePagination } from '@/components/data-table/DataTablePagination'
export { DataTableToolbar } from '@/components/data-table/DataTableToolbar'
export { DataTableViewOptions } from '@/components/data-table/DataTableViewOptions'
export {
  SelectAllHeaderCheckbox,
  SelectRowCheckbox,
  TableCheckbox,
} from '@/components/data-table/DataTableSelection'

export { useDebouncedValue } from '@/components/data-table/hooks/useDebouncedValue'
export {
  useTableSearchParams,
  type TableSearchParamsResult,
  type UseTableSearchParamsOptions,
} from '@/components/data-table/hooks/useTableSearchParams'

export {
  buildColumnSizeVars,
  columnSizeVar,
  columnWidthStyle,
} from '@/components/data-table/lib/columnSizeVars'
export { moveColumnOrder, normalizeColumnOrder } from '@/components/data-table/lib/columnOrder'
export * from '@/components/data-table/lib/urlState'
export {
  readPersistedViewState,
  useViewStateUserId,
  viewStateStorageKey,
  writePersistedViewState,
  type ViewState,
} from '@/components/data-table/lib/viewState'

export {
  hasApi,
  widenCoreTable,
  widenRow,
  widenTable,
  type AnyCell,
  type AnyCoreTable,
  type AnyHeader,
  type AnyRow,
  type AnyTable,
  type AnyTableFeatures,
  type DataTableColumnMeta,
  type DataTableDensity,
  type TableProp,
} from '@/components/data-table/types'
