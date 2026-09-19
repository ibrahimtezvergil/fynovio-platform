import {
  columnOrderingFeature,
  columnResizingFeature,
  columnSizingFeature,
  columnVisibilityFeature,
  createColumnHelper,
  rowSelectionFeature,
  tableFeatures,
  type ColumnVisibilityState,
} from '@tanstack/react-table'
import {
  SelectAllHeaderCheckbox,
  SelectRowCheckbox,
  type DataTableColumnMeta,
} from '@/components/data-table'
import { StatusBadge } from '@/features/demo-tables/components/StatusBadge'
import { formatDate, money } from '@/features/demo-tables/data/format'
import type { Employee } from '@/features/demo-tables/data/employees'

/**
 * Grid 2 — the advanced SaaS grid: visibility + selection + resizing + order.
 *
 * | Capability          | Feature plugin            | Row-model slot |
 * | ------------------- | ------------------------- | -------------- |
 * | Column visibility   | `columnVisibilityFeature` | none           |
 * | Multi-row selection | `rowSelectionFeature`     | none           |
 * | Column resizing     | `columnSizingFeature` + `columnResizingFeature` | none |
 * | Column order        | `columnOrderingFeature`   | none           |
 *
 * None of the four has a `create*RowModel` slot — they do not reshape the row
 * pipeline, they annotate it. `columnSizingFeature` is not optional decoration
 * either: resizing writes into the sizing state, and `tableFeatures()` rejects
 * `columnResizingFeature` without it at compile time.
 *
 * There is no `rowSortingFeature` here, deliberately. `column.getCanSort` then
 * does not exist, and the shared header cell renders plain labels — a feature
 * you do not register is not a feature you have to switch off.
 */
export const saasGridFeatures = tableFeatures({
  columnVisibilityFeature,
  rowSelectionFeature,
  columnSizingFeature,
  columnResizingFeature,
  columnOrderingFeature,
  columnMeta: {} as DataTableColumnMeta,
})

const helper = createColumnHelper<typeof saasGridFeatures, Employee>()

export const saasGridColumns = helper.columns([
  helper.display({
    id: 'select',
    size: 44,
    enableResizing: false,
    // The checkbox column must never be hidden — it is the only way out of a
    // selection.
    enableHiding: false,
    meta: { label: 'Selection', align: 'center' },
    header: ({ table }) => <SelectAllHeaderCheckbox table={table} />,
    cell: ({ row }) => <SelectRowCheckbox row={row} />,
  }),
  helper.accessor('name', {
    header: 'Member',
    size: 200,
    minSize: 140,
    enableHiding: false,
    meta: { label: 'Member', cellClassName: 'font-medium' },
  }),
  helper.accessor('email', {
    header: 'Email',
    size: 220,
    minSize: 160,
    meta: { label: 'Email', cellClassName: 'text-muted-foreground' },
  }),
  helper.accessor('department', {
    header: 'Department',
    size: 140,
    meta: { label: 'Department' },
  }),
  helper.accessor('role', {
    header: 'Role',
    size: 190,
    meta: { label: 'Role', cellClassName: 'text-muted-foreground' },
  }),
  helper.accessor('status', {
    header: 'Status',
    size: 130,
    meta: { label: 'Status', align: 'right' },
    cell: (info) => <StatusBadge status={info.getValue()} />,
  }),
  helper.accessor('location', {
    header: 'Location',
    size: 130,
    meta: { label: 'Location', cellClassName: 'text-muted-foreground' },
  }),
  helper.accessor('seats', {
    header: 'Seats',
    size: 90,
    meta: { label: 'Seats', align: 'right', cellClassName: 'tabular-nums' },
  }),
  helper.accessor('monthlySpend', {
    header: 'Monthly spend',
    size: 150,
    meta: { label: 'Monthly spend', align: 'right', cellClassName: 'tabular-nums' },
    cell: (info) => money.format(info.getValue()),
  }),
  helper.accessor('lastActive', {
    header: 'Last active',
    size: 140,
    meta: {
      label: 'Last active',
      align: 'right',
      cellClassName: 'text-muted-foreground tabular-nums',
    },
    cell: (info) => formatDate(info.getValue()),
  }),
])

/**
 * `enableHiding: false` says "do not offer this in the menu"; it does not make a
 * column visible. Hiding a column at startup is state, and lives here.
 */
export const saasGridInitialVisibility: ColumnVisibilityState = {
  location: false,
  seats: false,
}

/** Sizing floor and ceiling for every column that does not override them. */
export const saasGridDefaultColumn = {
  size: 160,
  minSize: 80,
  maxSize: 460,
}

/** View-state persistence key for this grid's column layout — see `ViewState`. */
export const SAAS_GRID_TABLE_ID = 'demo-tables.saas-grid'

export const getEmployeeRowId = (row: Employee) => row.id
