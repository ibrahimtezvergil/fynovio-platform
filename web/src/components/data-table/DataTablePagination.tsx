import type { RowData, TableFeatures } from '@tanstack/react-table'
import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from 'lucide-react'
import type { ReactNode } from 'react'
import { Button } from '@/components/ui/button'
import { hasApi, widenTable, type TableProp } from '@/components/data-table/types'
import { cn } from '@/lib/utils'

const PAGE_SIZES = [10, 25, 50, 100] as const

interface DataTablePaginationProps<TFeatures extends TableFeatures, TData extends RowData>
  extends TableProp<TFeatures, TData> {
  pageSizes?: readonly number[]
  /** Extra context on the left, e.g. "3 selected". */
  summary?: ReactNode
  className?: string
}

/**
 * Pagination controls for `rowPaginationFeature`.
 *
 * Everything shown here is derived, never stored: `getRowCount`, `getPageCount`
 * and `getCan*Page` read the client model in client mode and the `rowCount`
 * option in manual mode, so this component is identical either way.
 */
export function DataTablePagination<TFeatures extends TableFeatures, TData extends RowData>({
  table: instance,
  pageSizes = PAGE_SIZES,
  summary,
  className,
}: DataTablePaginationProps<TFeatures, TData>) {
  const table = widenTable<TData>(instance)
  if (!hasApi(table, 'getPageCount')) return null

  const { pageIndex, pageSize } = table.state.pagination ?? table.atoms.pagination?.get() ?? {
    pageIndex: 0,
    pageSize: 10,
  }
  const rowCount = table.getRowCount()
  const pageCount = table.getPageCount()
  const firstRow = rowCount === 0 ? 0 : pageIndex * pageSize + 1
  const lastRow = Math.min(rowCount, (pageIndex + 1) * pageSize)

  return (
    <div
      className={cn(
        'border-border flex flex-wrap items-center justify-between gap-3 border-t px-4 py-2.5',
        className,
      )}
    >
      <div className="text-muted-foreground flex items-center gap-3 text-xs">
        <span className="tabular-nums">
          {firstRow}–{lastRow} of {rowCount}
        </span>
        {summary}
      </div>

      <div className="flex items-center gap-3">
        <label className="text-muted-foreground flex items-center gap-1.5 text-xs">
          Rows
          <select
            value={pageSize}
            onChange={(event) => table.setPageSize(Number(event.target.value))}
            className="border-input bg-background focus-visible:border-ring focus-visible:ring-ring/50 h-7 rounded-lg border px-1.5 text-xs outline-none focus-visible:ring-3"
          >
            {pageSizes.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
        </label>

        <span className="text-muted-foreground text-xs tabular-nums">
          Page {pageCount === 0 ? 0 : pageIndex + 1} / {pageCount}
        </span>

        <div className="flex items-center gap-1">
          <Button
            variant="outline"
            size="icon-sm"
            aria-label="First page"
            disabled={!table.getCanPreviousPage()}
            onClick={() => table.firstPage()}
          >
            <ChevronsLeft aria-hidden />
          </Button>
          <Button
            variant="outline"
            size="icon-sm"
            aria-label="Previous page"
            disabled={!table.getCanPreviousPage()}
            onClick={() => table.previousPage()}
          >
            <ChevronLeft aria-hidden />
          </Button>
          <Button
            variant="outline"
            size="icon-sm"
            aria-label="Next page"
            disabled={!table.getCanNextPage()}
            onClick={() => table.nextPage()}
          >
            <ChevronRight aria-hidden />
          </Button>
          <Button
            variant="outline"
            size="icon-sm"
            aria-label="Last page"
            disabled={!table.getCanLastPage()}
            onClick={() => table.lastPage()}
          >
            <ChevronsRight aria-hidden />
          </Button>
        </div>
      </div>
    </div>
  )
}
