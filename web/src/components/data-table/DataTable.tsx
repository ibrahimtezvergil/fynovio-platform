import { FlexRender, Subscribe, type RowData, type TableFeatures } from '@tanstack/react-table'
import type { ReactNode } from 'react'
import { DataTableHeaderCell } from '@/components/data-table/DataTableHeaderCell'
import { buildColumnSizeVars, columnWidthStyle } from '@/components/data-table/lib/columnSizeVars'
import {
  hasApi,
  widenTable,
  type AnyRow,
  type DataTableColumnMeta,
  type DataTableDensity,
  type TableProp,
} from '@/components/data-table/types'
import { cn } from '@/lib/utils'

import { Skeleton } from '@/components/ui/skeleton'
/* Alignment contract: strings left; numbers, amounts, percentages and status
   badges right — and every right-aligned column is tabular. */
const TEXT_ALIGN = {
  left: 'text-left',
  center: 'text-center',
  right: 'text-right tnum',
} as const

/**
 * Visibility-aware when `columnVisibilityFeature` is registered, plain
 * otherwise. Called on the instance — v9 row/cell/column methods are
 * prototype-bound and break when destructured.
 */
function renderableCells<TData extends RowData>(row: AnyRow<TData>) {
  return hasApi(row, 'getVisibleCells') ? row.getVisibleCells() : row.getAllCells()
}

interface DataTableRowProps<TData extends RowData> {
  row: AnyRow<TData>
  resizable: boolean
  onRowClick?: (row: TData) => void
}

function DataTableBodyRow<TData extends RowData>({ row, resizable, onRowClick }: DataTableRowProps<TData>) {
  const cells = renderableCells(row)

  const renderRow = (selected: boolean) => (
    <tr
      data-selected={selected || undefined}
      onClick={onRowClick ? (event) => {
        // Checkboxes and row-menu controls own their click; opening a peek as
        // their side effect would make multi-select unusable.
        if ((event.target as HTMLElement).closest('button, input, [role="menuitem"]')) return
        onRowClick(row.original)
      } : undefined}
      // Height, zebra, hover and selection all resolve against the `--nx-d-*`
      // block the wrapper's `data-density` selects, so a density flip repaints
      // custom properties instead of re-rendering this component. Zebra and
      // hover live in `.nx-datagrid` (index.css) rather than as `even:` /
      // `hover:` utilities: at equal specificity the winner would be decided by
      // Tailwind's variant sort order, and a striped row that stops responding
      // to the pointer is not a bug worth leaving to that.
      className={cn('group/row h-[var(--nx-d-row-relaxed)] transition-colors', onRowClick && 'cursor-pointer')}
    >
      {cells.map((cell) => {
        const meta = (cell.column.columnDef.meta ?? {}) as DataTableColumnMeta
        return (
          <td
            key={cell.id}
            style={resizable ? columnWidthStyle(cell.column.id) : undefined}
            className={cn(
              'text-muted-foreground overflow-hidden border-b border-[var(--nx-d-divider)] align-middle text-ellipsis whitespace-nowrap',
              'px-[var(--nx-d-cell-x)] text-[length:var(--nx-d-text)]',
              TEXT_ALIGN[meta.align ?? 'left'],
              meta.cellClassName,
            )}
          >
            <FlexRender cell={cell} />
          </td>
        )
      })}
    </tr>
  )

  // Row selection is the one slice worth an per-row subscription: without it,
  // ticking one checkbox re-renders every row in the page. `table.atoms.*`
  // resolves to whoever owns the slice — internal atom or the external one
  // passed through `options.atoms` — so this works either way.
  const selectionAtom = row.table.atoms.rowSelection
  if (!selectionAtom) return renderRow(false)

  return (
    <Subscribe source={selectionAtom} selector={(selection) => Boolean(selection?.[row.id])}>
      {(selected) => renderRow(selected)}
    </Subscribe>
  )
}

export interface DataTableProps<
  TFeatures extends TableFeatures,
  TData extends RowData,
> extends TableProp<TFeatures, TData> {
  /**
   * Pins the grid to one density. Omit it — the normal case — and the grid
   * inherits whatever `data-density` is in scope, which is the app-wide
   * preference on `<html>` unless a nearer region overrode it.
   */
  density?: DataTableDensity
  /**
   * Switches the grid to fixed layout driven by CSS width variables. Required
   * for `columnResizingFeature`; pointless without it.
   */
  resizable?: boolean
  isLoading?: boolean
  /** Rendered in place of `<tbody>` rows when the row model is empty. */
  empty?: ReactNode
  /** Minimum width before the horizontal scroller kicks in (non-resizable grids). */
  minWidth?: number
  caption?: string
  className?: string
  /** A detail/peek affordance for grids that keep readers in context. */
  onRowClick?: (row: TData) => void
}

/**
 * The renderer. It owns markup, Tailwind classes, ARIA and nothing else —
 * every value it prints comes from the table instance handed to it.
 *
 * It never imports a feature and never calls `useTable`: which capabilities
 * light up is decided entirely by the `tableFeatures({ ... })` object of
 * whoever constructs the table.
 */
export function DataTable<TFeatures extends TableFeatures, TData extends RowData>({
  table: instance,
  density,
  resizable = false,
  isLoading = false,
  empty,
  minWidth = 720,
  caption,
  className,
  onRowClick,
}: DataTableProps<TFeatures, TData>) {
  const table = widenTable<TData>(instance)
  const headerGroups = table.getHeaderGroups()
  const rows = table.getRowModel().rows

  // Widths are collected once per commit and written to the `<table>` element as
  // CSS custom properties. The win is not the O(columns) loop — it is that no
  // `<td>` ever calls `column.getSize()`, which with
  // `columnResizeMode: 'onChange'` would run once per cell per animation frame
  // of a drag. The component re-renders here because its `useTable` selector
  // includes `columnSizing`.
  const sizeVars = resizable ? buildColumnSizeVars(table) : undefined

  const columnCount = headerGroups.at(-1)?.headers.length ?? 1

  return (
    // Every grid is a density region: `.nx-dense` rebinds the height tokens
    // its cells and inline controls read, so a grid resizes even when the page
    // around it was never wrapped. `data-density` is written only when the
    // caller pinned one — otherwise the `--nx-d-*` values are inherited from
    // whatever region, or `<html>`, is in scope.
    <div data-density={density} className={cn('nx-dense w-full overflow-x-auto', className)}>
      <table
        style={{
          ...sizeVars,
          ...(resizable
            ? { width: 'calc(var(--table-total-size) * 1px)', minWidth: '100%' }
            : { minWidth }),
        }}
        className={cn(
          'nx-datagrid w-full border-separate border-spacing-0',
          resizable && 'table-fixed',
        )}
      >
        {caption && <caption className="sr-only">{caption}</caption>}

        <thead className="sticky top-0 z-10">
          {headerGroups.map((group) => (
            <tr key={group.id}>
              {group.headers.map((header) => (
                <DataTableHeaderCell key={header.id} header={header} resizable={resizable} />
              ))}
            </tr>
          ))}
        </thead>

        <tbody className={cn(isLoading && rows.length > 0 && 'opacity-50 transition-opacity')}>
          {isLoading &&
            rows.length === 0 &&
            Array.from({ length: 6 }).map((_, index) => (
              <tr key={`skeleton-${index}`}>
                <td
                  colSpan={columnCount}
                  className="h-[var(--nx-d-row-relaxed)] border-b border-[var(--nx-d-divider)] px-[var(--nx-d-cell-x)] py-[var(--nx-d-cell-y)]"
                >
                  <Skeleton className="h-6 rounded-md" />
                </td>
              </tr>
            ))}

          {!isLoading && rows.length === 0 && (
            <tr>
              <td colSpan={columnCount}>{empty}</td>
            </tr>
          )}

          {rows.map((row) => (
            <DataTableBodyRow key={row.id} row={row} resizable={resizable} onRowClick={onRowClick} />
          ))}
        </tbody>
      </table>
    </div>
  )
}
