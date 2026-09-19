import { FlexRender, type RowData } from '@tanstack/react-table'
import { ArrowDown, ArrowUp, ChevronsUpDown } from 'lucide-react'
import { columnWidthStyle } from '@/components/data-table/lib/columnSizeVars'
import { hasApi, type AnyHeader, type DataTableColumnMeta } from '@/components/data-table/types'
import { cn } from '@/lib/utils'

const TEXT_ALIGN = {
  left: 'text-left',
  center: 'text-center',
  right: 'text-right tnum',
} as const

const FLEX_ALIGN = {
  left: 'justify-start',
  center: 'justify-center',
  right: 'justify-end',
} as const

interface DataTableHeaderCellProps<TData extends RowData> {
  header: AnyHeader<TData>
  /** Grid-level switch: only sizing-enabled tables get width vars and handles. */
  resizable: boolean
}

/**
 * All header chrome lives here, not in the column definitions.
 *
 * Column defs stay declarative (`helper.accessor('name', { header: 'Name' })`)
 * and this cell decides — from the APIs the table actually exposes — whether to
 * render a sort button, a resize handle, or plain text. A grid that never
 * registers `rowSortingFeature` has no `column.getCanSort`, so no affordance is
 * rendered and nothing needs to be configured to turn it off.
 */
export function DataTableHeaderCell<TData extends RowData>({
  header,
  resizable,
}: DataTableHeaderCellProps<TData>) {
  const { column } = header
  const meta = (column.columnDef.meta ?? {}) as DataTableColumnMeta
  const align = meta.align ?? 'left'

  const canSort = hasApi(column, 'getCanSort') && column.getCanSort()
  const sorted = hasApi(column, 'getIsSorted') ? column.getIsSorted() : false
  const sortIndex = canSort && hasApi(column, 'getSortIndex') ? column.getSortIndex() : -1
  const canResize = resizable && hasApi(column, 'getCanResize') && column.getCanResize()
  const isResizing = hasApi(column, 'getIsResizing') && column.getIsResizing()

  const SortIcon = sorted === 'asc' ? ArrowUp : sorted === 'desc' ? ArrowDown : ChevronsUpDown

  const label = header.isPlaceholder ? null : <FlexRender header={header} />

  return (
    <th
      scope="col"
      colSpan={header.colSpan}
      // `aria-sort` is the only part of sorting a screen reader ever sees.
      aria-sort={sorted === 'asc' ? 'ascending' : sorted === 'desc' ? 'descending' : 'none'}
      style={resizable ? columnWidthStyle(column.id) : undefined}
      className={cn(
        'text-muted-foreground relative border-b border-[var(--nx-hairline)] text-[10.5px] font-[650] tracking-[0.06em] whitespace-nowrap uppercase',
        'bg-[var(--nx-glass)] backdrop-blur-[24px] backdrop-saturate-[170%]',
        'h-[var(--nx-d-head)]',
        TEXT_ALIGN[align],
        meta.headerClassName,
      )}
    >
      {canSort ? (
        <button
          type="button"
          onClick={column.getToggleSortingHandler()}
          className={cn(
            // `uppercase` again: preflight resets text-transform on <button>,
            // so the th's casing does not reach the label.
            'flex w-full cursor-pointer items-center gap-1 px-[var(--nx-d-cell-x)] py-1 uppercase select-none',
            'hover:text-foreground rounded-sm transition-colors duration-[250ms] ease-fluid',
            sorted && 'text-accent-foreground',
            FLEX_ALIGN[align],
          )}
        >
          {label}
          <SortIcon aria-hidden className="size-3 shrink-0 opacity-70" strokeWidth={2} />
          {sortIndex > 0 && (
            <span className="bg-muted text-muted-foreground rounded-full px-1 text-[10px] leading-4">
              {sortIndex + 1}
            </span>
          )}
        </button>
      ) : (
        <div
          className={cn('flex items-center gap-1 px-[var(--nx-d-cell-x)] py-1', FLEX_ALIGN[align])}
        >
          {label}
        </div>
      )}

      {canResize && (
        // Mouse and touch are wired separately on purpose: the shipped handler
        // branches on `touchstart`, and a lone `pointerdown` listener leaves
        // touch resizing dead.
        <div
          role="separator"
          aria-orientation="vertical"
          aria-label={`Resize ${meta.label ?? column.id}`}
          data-resizing={isResizing || undefined}
          onMouseDown={header.getResizeHandler()}
          onTouchStart={header.getResizeHandler()}
          onDoubleClick={() => column.resetSize()}
          className={cn(
            'absolute top-1 right-0 bottom-1 z-10 w-3 translate-x-1/2 cursor-col-resize touch-none select-none',
            'after:absolute after:inset-y-0 after:left-1/2 after:w-px after:-translate-x-1/2 after:bg-[var(--nx-hairline)] after:transition-colors',
            'hover:after:bg-brand-graphic hover:after:w-0.5',
            'data-resizing:after:bg-brand-graphic data-resizing:after:w-0.5',
          )}
        />
      )}
    </th>
  )
}
