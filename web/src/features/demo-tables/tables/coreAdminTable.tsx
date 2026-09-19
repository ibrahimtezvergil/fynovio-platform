import {
  columnFilteringFeature,
  createColumnHelper,
  createFilteredRowModel,
  createPaginatedRowModel,
  createSortedRowModel,
  filterFn_includesString,
  globalFilteringFeature,
  rowPaginationFeature,
  rowSortingFeature,
  sortFn_alphanumeric,
  sortFn_datetime,
  sortFn_text,
  tableFeatures,
} from '@tanstack/react-table'
import type { DataTableColumnMeta } from '@/components/data-table'
import { StatusBadge } from '@/features/demo-tables/components/StatusBadge'
import { formatDate, money } from '@/features/demo-tables/data/format'
import type { Employee } from '@/features/demo-tables/data/employees'

/**
 * Grid 1 — the core admin list: pagination + sorting + global search.
 *
 * Every entry is here because a capability on screen needs it, and nothing
 * else is:
 *
 * | Capability      | Feature plugin           | Row-model slot                    |
 * | --------------- | ------------------------ | --------------------------------- |
 * | Global search   | `columnFilteringFeature` + `globalFilteringFeature` | `filteredRowModel`  |
 * | Column sorting  | `rowSortingFeature`      | `sortedRowModel`                  |
 * | Pagination      | `rowPaginationFeature`   | `paginatedRowModel`               |
 *
 * `filterFns` / `sortFns` are feature slots, not table options: registering
 * `includesString` here is what makes `globalFilterFn: 'includesString'`
 * type-check below. Importing the whole `filterFns` / `sortFns` registry object
 * would work too, at the cost of bundling every built-in.
 *
 * Defined at module scope — a `tableFeatures({ ... })` call inside a component
 * would hand the table a new feature set on every render.
 */
export const coreAdminFeatures = tableFeatures({
  columnFilteringFeature,
  globalFilteringFeature,
  filteredRowModel: createFilteredRowModel(),
  filterFns: { includesString: filterFn_includesString },

  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
  sortFns: {
    alphanumeric: sortFn_alphanumeric,
    text: sortFn_text,
    datetime: sortFn_datetime,
  },

  rowPaginationFeature,
  paginatedRowModel: createPaginatedRowModel(),

  // Type-only slot: gives `columnDef.meta` a shape without global declaration
  // merging. The value is phantom and stripped at runtime.
  columnMeta: {} as DataTableColumnMeta,
})

const helper = createColumnHelper<typeof coreAdminFeatures, Employee>()

export const coreAdminColumns = helper.columns([
  helper.accessor('name', {
    header: 'Member',
    sortFn: 'text',
    meta: { label: 'Member' },
    cell: (info) => (
      <div className="flex min-w-0 flex-col">
        <span className="truncate font-medium">{info.getValue()}</span>
        <span className="text-muted-foreground truncate text-[11px]">
          {info.row.original.email}
        </span>
      </div>
    ),
  }),
  helper.accessor('department', {
    header: 'Department',
    sortFn: 'text',
    meta: { label: 'Department' },
  }),
  helper.accessor('role', {
    header: 'Role',
    sortFn: 'text',
    meta: { label: 'Role' },
    cell: (info) => <span className="text-muted-foreground">{info.getValue()}</span>,
  }),
  helper.accessor('status', {
    header: 'Status',
    sortFn: 'text',
    meta: { label: 'Status', align: 'right' },
    cell: (info) => <StatusBadge status={info.getValue()} />,
  }),
  helper.accessor('lastActive', {
    header: 'Last active',
    // Dates are ISO strings here; the datetime comparator parses them instead
    // of comparing them lexically.
    sortFn: 'datetime',
    sortUndefined: 'last',
    meta: { label: 'Last active', align: 'right', cellClassName: 'text-muted-foreground tabular-nums' },
    cell: (info) => formatDate(info.getValue()),
  }),
  helper.accessor('monthlySpend', {
    header: 'Monthly spend',
    sortFn: 'alphanumeric',
    // Money reads high-to-low first; ascending money is rarely the question.
    sortDescFirst: true,
    meta: { label: 'Monthly spend', align: 'right', cellClassName: 'tabular-nums' },
    cell: (info) => money.format(info.getValue()),
  }),
])

/**
 * Stable identity for every row. Index-based ids break the moment sorting,
 * filtering or a page change moves a row.
 */
export const getEmployeeRowId = (row: Employee) => row.id

/**
 * Default eligibility only inspects the first core row and accepts strings and
 * numbers, which would quietly pull `monthlySpend` and the ISO `lastActive`
 * string into a text search. Spell the rule out instead — and keep it identical
 * to the server's `SEARCHABLE` list so the client/server switch is invisible.
 */
export const searchableColumns = new Set(['name', 'department', 'role'])

// Typed by the shape it reads, not by `Column<TFeatures, TData, TValue>`: the
// option is a generic callback, and only a structural parameter satisfies it
// for every feature set.
export const canGlobalFilterColumn = (column: { id: string }) => searchableColumns.has(column.id)
