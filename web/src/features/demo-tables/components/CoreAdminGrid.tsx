import { useTable } from '@tanstack/react-table'
import { Inbox, RotateCcw } from 'lucide-react'
import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import { EmptyState } from '@/components/common/EmptyState'
import { SegmentedControl, type Segment } from '@/components/common/SegmentedControl'
import {
  DataTable,
  DataTablePagination,
  DataTableToolbar,
  useTableSearchParams,
} from '@/components/data-table'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { useAllEmployees, useEmployeePage } from '@/features/demo-tables/data/api'
import { NO_EMPLOYEES } from '@/features/demo-tables/data/employees'
import {
  canGlobalFilterColumn,
  coreAdminColumns,
  coreAdminFeatures,
  getEmployeeRowId,
} from '@/features/demo-tables/tables/coreAdminTable'

type ProcessingMode = 'client' | 'server'

const MODE_SEGMENTS: readonly Segment<ProcessingMode>[] = [
  { value: 'client', label: 'Client-side' },
  { value: 'server', label: 'Server-side' },
]

const MODE_PARAM = 'admin.mode'
const URL_PREFIX = 'admin'

/**
 * Grid 1 — pagination, sorting and global search, over the same dataset either
 * way.
 *
 * The interesting part is what *doesn't* change when the mode switch flips:
 * the columns, the renderer, the URL contract and every control below are
 * identical. Only three `manual*` booleans and the source of `data` move.
 */
export function CoreAdminGrid() {
  const [searchParams, setSearchParams] = useSearchParams()
  const mode: ProcessingMode = searchParams.get(MODE_PARAM) === 'server' ? 'server' : 'client'
  const isServer = mode === 'server'

  const setMode = useCallback(
    (next: ProcessingMode) => {
      setSearchParams(
        (prev) => {
          const params = new URLSearchParams(prev)
          if (next === 'server') params.set(MODE_PARAM, 'server')
          else params.delete(MODE_PARAM)
          // Page numbers don't survive a change of who does the paging.
          params.delete(`${URL_PREFIX}.page`)
          return params
        },
        { replace: true },
      )
    },
    [setSearchParams],
  )

  // The URL owns pagination, sorting and the global filter.
  const urlState = useTableSearchParams({ prefix: URL_PREFIX, defaultPageSize: 10 })
  const { pagination, sorting, globalFilter } = urlState

  const pageRequest = useMemo(
    () => ({
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
      sorting,
      search: globalFilter,
    }),
    [pagination, sorting, globalFilter],
  )

  const serverQuery = useEmployeePage(pageRequest, isServer)
  const clientQuery = useAllEmployees(!isServer)

  // `NO_EMPLOYEES` is a module constant: `?? []` would hand the table a new
  // array identity on every render and invalidate every row model with it.
  const data = isServer ? (serverQuery.data?.rows ?? NO_EMPLOYEES) : (clientQuery.data ?? NO_EMPLOYEES)
  const isLoading = isServer ? serverQuery.isFetching : clientQuery.isLoading

  const state = useMemo(
    () => ({ pagination, sorting, globalFilter }),
    [pagination, sorting, globalFilter],
  )

  const table = useTable({
    features: coreAdminFeatures,
    data,
    columns: coreAdminColumns,
    getRowId: getEmployeeRowId,

    // Ownership: external `state` + matching `on<Slice>Change`, backed by the URL.
    state,
    onPaginationChange: urlState.onPaginationChange,
    onSortingChange: urlState.onSortingChange,
    onGlobalFilterChange: urlState.onGlobalFilterChange,

    globalFilterFn: 'includesString',
    getColumnCanGlobalFilter: canGlobalFilterColumn,
    enableMultiSort: true,
    enableSortingRemoval: true,

    // `manual*` means "the rows I hand you are already processed" — it never
    // triggers a request. Fetching from the same state is the hook's job.
    manualPagination: isServer,
    manualSorting: isServer,
    manualFiltering: isServer,
    rowCount: isServer ? (serverQuery.data?.rowCount ?? 0) : undefined,

    // The URL is the owner; the table must not silently rewrite the page index
    // when the data reference changes.
    autoResetPageIndex: false,
  })

  // Routed through the table API rather than straight to the URL setter, so the
  // feature's own reset behaviour (page index back to 0) still runs.
  const handleSearch = useCallback((value: string) => table.setGlobalFilter(value), [table])

  return (
    <Card className="gap-0 overflow-hidden p-0">
      <div className="border-border border-b px-4 py-3">
        <DataTableToolbar
          value={globalFilter}
          onChange={handleSearch}
          placeholder="Search members, departments, roles…"
        >
          <SegmentedControl
            aria-label="Processing mode"
            segments={MODE_SEGMENTS}
            value={mode}
            onChange={setMode}
          />
          <Button
            variant="ghost"
            size="sm"
            onClick={urlState.reset}
            disabled={!urlState.hasActiveState}
          >
            <RotateCcw aria-hidden strokeWidth={1.75} />
            Reset
          </Button>
        </DataTableToolbar>

        <p className="text-muted-foreground mt-2.5 font-mono text-[11px]">
          <span className="text-foreground/70">URL state:</span>{' '}
          {searchParams.toString() ? `?${searchParams.toString()}` : '(defaults — nothing to persist)'}
        </p>
      </div>

      <DataTable
        table={table}
        isLoading={isLoading}
        caption="Workspace members, paginated and sortable"
        empty={
          <EmptyState
            icon={Inbox}
            title="No members match this search"
            description="Clear the search box or widen the filter to see the full member list again."
          />
        }
      />

      <DataTablePagination
        table={table}
        summary={
          <span className="border-border rounded-full border px-2 py-0.5 text-[10px] tracking-wide uppercase">
            {isServer ? 'server-side' : 'client-side'}
          </span>
        }
      />
    </Card>
  )
}
