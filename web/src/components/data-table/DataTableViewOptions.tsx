import type { ColumnOrderState, RowData, TableFeatures } from '@tanstack/react-table'
import { ChevronDown, ChevronUp, Columns3, RotateCcw } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { moveColumnOrder, normalizeColumnOrder } from '@/components/data-table/lib/columnOrder'
import {
  hasApi,
  widenTable,
  type DataTableColumnMeta,
  type TableProp,
} from '@/components/data-table/types'

interface DataTableViewOptionsProps<TFeatures extends TableFeatures, TData extends RowData>
  extends TableProp<TFeatures, TData> {
  label?: string
}

/**
 * Column visibility (+ order, where registered) menu.
 *
 * Three rules the features make easy to get wrong, all handled here:
 *
 * - The menu is built from `getAllLeafColumns()`, not the visible ones — a
 *   hidden column must still be listed so it can come back.
 * - `getCanHide()` gates the entry. A column marked `enableHiding: false` is
 *   still hideable through state; it just must not be offered in the UI —
 *   the same column is never offered a reorder control either, so a
 *   structural column (a selection checkbox) never moves.
 * - `getAllLeafColumns()` returns definition order, not `columnOrder` — the
 *   list is re-sorted through the same merge `columnOrderingFeature` applies
 *   internally, or the move buttons would reorder a grid the menu still
 *   renders in the old order.
 */
export function DataTableViewOptions<TFeatures extends TableFeatures, TData extends RowData>({
  table: instance,
  label = 'Columns',
}: DataTableViewOptionsProps<TFeatures, TData>) {
  const table = widenTable<TData>(instance)
  if (!hasApi(table, 'setColumnVisibility')) return null

  const allColumns = table.getAllLeafColumns()
  const columns = allColumns.filter((column) => hasApi(column, 'getCanHide') && column.getCanHide())
  const hiddenCount = columns.filter((column) => !column.getIsVisible()).length

  const supportsOrdering = hasApi(table, 'setColumnOrder')
  const allIds = allColumns.map((column) => column.id)
  const movableIds = columns.map((column) => column.id)
  const currentOrder = supportsOrdering
    ? ((table.atoms?.columnOrder?.get() as ColumnOrderState | undefined) ?? [])
    : []
  const normalizedOrder = normalizeColumnOrder(currentOrder, allIds)
  const movableSequence = normalizedOrder.filter((id) => movableIds.includes(id))

  const orderedColumns = supportsOrdering
    ? movableSequence
        .map((id) => columns.find((column) => column.id === id))
        .filter((column): column is (typeof columns)[number] => Boolean(column))
    : columns

  const moveColumn = (columnId: string, direction: -1 | 1) => {
    table.setColumnOrder(moveColumnOrder(currentOrder, allIds, movableIds, columnId, direction))
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button variant="outline" size="sm">
            <Columns3 aria-hidden strokeWidth={1.75} />
            {label}
            {hiddenCount > 0 && (
              <span className="bg-muted text-muted-foreground rounded-full px-1.5 text-[10px] leading-4 tabular-nums">
                {hiddenCount}
              </span>
            )}
          </Button>
        }
      />
      <DropdownMenuContent align="end" className="w-60">
        <DropdownMenuLabel>Visible columns</DropdownMenuLabel>
        <DropdownMenuSeparator />
        {orderedColumns.map((column, index) => {
          const meta = (column.columnDef.meta ?? {}) as DataTableColumnMeta
          const columnLabel = meta.label ?? column.id
          return (
            <DropdownMenuCheckboxItem
              key={column.id}
              checked={column.getIsVisible()}
              onCheckedChange={(checked) => column.toggleVisibility(checked)}
              closeOnClick={false}
            >
              {supportsOrdering && (
                <span className="flex items-center gap-0.5">
                  <button
                    type="button"
                    aria-label={`Move ${columnLabel} earlier`}
                    disabled={index === 0}
                    onClick={(event) => {
                      event.stopPropagation()
                      moveColumn(column.id, -1)
                    }}
                    className="text-muted-foreground hover:bg-accent hover:text-accent-foreground disabled:pointer-events-none disabled:opacity-30 rounded-sm p-0.5"
                  >
                    <ChevronUp aria-hidden className="size-3" strokeWidth={2.2} />
                  </button>
                  <button
                    type="button"
                    aria-label={`Move ${columnLabel} later`}
                    disabled={index === orderedColumns.length - 1}
                    onClick={(event) => {
                      event.stopPropagation()
                      moveColumn(column.id, 1)
                    }}
                    className="text-muted-foreground hover:bg-accent hover:text-accent-foreground disabled:pointer-events-none disabled:opacity-30 rounded-sm p-0.5"
                  >
                    <ChevronDown aria-hidden className="size-3" strokeWidth={2.2} />
                  </button>
                </span>
              )}
              <span className="min-w-0 flex-1 truncate">{columnLabel}</span>
            </DropdownMenuCheckboxItem>
          )
        })}
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={() => table.resetColumnVisibility()}>
          <RotateCcw aria-hidden strokeWidth={1.75} />
          Reset to defaults
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
