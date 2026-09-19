import {
  Subscribe,
  type Row,
  type RowData,
  type Table,
  type TableFeatures,
} from '@tanstack/react-table'
import { Check, Minus } from 'lucide-react'
import { useEffect, useRef, type ComponentProps } from 'react'
import { widenCoreTable, widenRow } from '@/components/data-table/types'
import { cn } from '@/lib/utils'

interface TableCheckboxProps extends Omit<ComponentProps<'input'>, 'type'> {
  indeterminate?: boolean
}

/**
 * A real `<input type="checkbox">`, on purpose.
 *
 * v9's shift-range selection reads modifier keys off the event it is handed.
 * React's synthetic `change` carries the original click through `nativeEvent`,
 * so a native input keeps Shift-click ranges working — a custom control that
 * emits `onCheckedChange(boolean)` throws that information away.
 */
export function TableCheckbox({ indeterminate, className, ...props }: TableCheckboxProps) {
  const ref = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (ref.current) ref.current.indeterminate = Boolean(indeterminate) && !props.checked
  }, [indeterminate, props.checked])

  return (
    <span className="relative inline-flex size-4 shrink-0 align-middle">
      <input
        ref={ref}
        type="checkbox"
        className={cn(
          'peer border-input bg-background size-4 cursor-pointer appearance-none rounded-[5px] border transition-colors outline-none',
          'checked:border-brand-graphic checked:bg-brand-graphic',
          'indeterminate:border-brand-graphic indeterminate:bg-brand-graphic',
          'focus-visible:ring-ring/50 focus-visible:border-ring focus-visible:ring-3',
          'disabled:cursor-not-allowed disabled:opacity-40',
          className,
        )}
        {...props}
      />
      <Check
        aria-hidden
        strokeWidth={3}
        className="pointer-events-none absolute inset-0 m-auto size-3 text-white opacity-0 peer-checked:opacity-100"
      />
      <Minus
        aria-hidden
        strokeWidth={3}
        className="pointer-events-none absolute inset-0 m-auto size-3 text-white opacity-0 peer-indeterminate:opacity-100"
      />
    </span>
  )
}

/**
 * Header checkbox for the selection column.
 *
 * Inside a header renderer `table` is the core instance, so this subscribes
 * through the standalone `Subscribe` rather than `table.Subscribe` — otherwise
 * the read hides behind `getIsAllPageRowsSelected()` and never re-renders.
 */
export function SelectAllHeaderCheckbox<TFeatures extends TableFeatures, TData extends RowData>({
  table: instance,
}: {
  table: Table<TFeatures, TData>
}) {
  const table = widenCoreTable<TData>(instance)
  const selectionAtom = table.atoms.rowSelection
  if (!selectionAtom) return null

  return (
    <Subscribe
      source={selectionAtom}
      selector={() => ({
        all: table.getIsAllPageRowsSelected(),
        some: table.getIsSomePageRowsSelected(),
      })}
    >
      {({ all, some }) => (
        <TableCheckbox
          aria-label={all ? 'Deselect all rows on this page' : 'Select all rows on this page'}
          checked={all}
          indeterminate={some && !all}
          onChange={table.getToggleAllPageRowsSelectedHandler()}
        />
      )}
    </Subscribe>
  )
}

/**
 * Row checkbox for the selection column.
 *
 * `getToggleSelectedHandler()` is what establishes and advances the Shift
 * anchor; calling `row.toggleSelected(checked)` by hand silently disables range
 * selection.
 */
export function SelectRowCheckbox<TFeatures extends TableFeatures, TData extends RowData>({
  row: instance,
}: {
  row: Row<TFeatures, TData>
}) {
  const row = widenRow<TData>(instance)
  const selectionAtom = row.table.atoms.rowSelection
  if (!selectionAtom) return null

  return (
    <Subscribe source={selectionAtom} selector={(selection) => Boolean(selection?.[row.id])}>
      {(selected) => (
        <TableCheckbox
          aria-label={`Select ${row.id}`}
          checked={selected}
          disabled={!row.getCanSelect()}
          onChange={row.getToggleSelectedHandler()}
        />
      )}
    </Subscribe>
  )
}
