import { Boxes, Gauge, Link2, Radio, TriangleAlert, type LucideIcon } from 'lucide-react'

export interface ConfigDoc {
  id: string
  icon: LucideIcon
  title: string
  summary: string
  code?: string
  codeFilename?: string
  points: { term: string; description: string }[]
}

export const CONFIG_DOCS: ConfigDoc[] = [
  {
    id: 'table-features',
    icon: Boxes,
    title: 'The tableFeatures object',
    summary:
      'One static object per table shape, declared at module scope. It carries three different kinds of thing: feature plugins, create*RowModel factories, and the function registries those features resolve names against. Its inferred type then gates every option, state slice and instance API downstream — a missing API is almost always a missing registration, not a typing problem.',
    codeFilename: 'tables/coreAdminTable.tsx',
    code: [
      'export const coreAdminFeatures = tableFeatures({',
      '  // 1. Feature plugins — the APIs and state slices.',
      '  columnFilteringFeature,',
      '  globalFilteringFeature,',
      '  rowSortingFeature,',
      '  rowPaginationFeature,',
      '',
      '  // 2. Row-model slots — the processing pipeline, in order.',
      '  filteredRowModel: createFilteredRowModel(),',
      '  sortedRowModel: createSortedRowModel(),',
      '  paginatedRowModel: createPaginatedRowModel(),',
      '',
      '  // 3. Function registries — the string names columns may use.',
      '  filterFns: { includesString: filterFn_includesString },',
      '  sortFns: { text: sortFn_text, datetime: sortFn_datetime },',
      '',
      '  // 4. Type-only slots — phantom values, stripped at runtime.',
      '  columnMeta: {} as DataTableColumnMeta,',
      '})',
    ].join('\n'),
    points: [
      { term: 'Prerequisites', description: 'Slots are validated: sortedRowModel needs rowSortingFeature, filteredRowModel needs columnFilteringFeature, columnResizingFeature needs columnSizingFeature. A missing prerequisite becomes a type error naming what to add.' },
      { term: 'Module scope', description: 'Calling tableFeatures() inside a component builds a new feature set every render and re-stitches the table with it.' },
      { term: 'Pipeline order', description: 'core → filtered → grouped → sorted → expanded → paginated. Each stage falls through to the previous one when its slot is missing or its manual* flag is set.' },
      { term: 'Avoid stockFeatures', description: 'It registers every stock plugin and every processing slot, which is exactly what v9 tree-shaking is designed to avoid. Two grids on this page register nine plugins between them.' },
      { term: 'Two grids, two objects', description: 'Grid 2 has no rowSortingFeature at all, so column.getCanSort does not exist and the shared header cell renders plain labels. Nothing had to be disabled.' },
    ],
  },
  {
    id: 'use-table',
    icon: Radio,
    title: 'useTable and reactive reads',
    summary:
      'useTable takes the options and an optional selector. The selector decides two things at once: which state changes re-render this component, and what table.state contains. Omit it and you subscribe to every registered slice — the right default, and the wrong one as soon as a row-level interaction like selection starts re-rendering the whole body.',
    codeFilename: 'components/AdvancedSaasGrid.tsx',
    code: [
      'const table = useTable(',
      '  { features, columns, data, atoms: { rowSelection } },',
      '  // rowSelection is deliberately absent: ticking a checkbox must not',
      '  // re-render every row. The parts that care subscribe themselves.',
      '  (state) => ({',
      '    columnVisibility: state.columnVisibility,',
      '    columnSizing: state.columnSizing,',
      '  }),',
      ')',
      '',
      '// Narrow boundary inside a cell, where table is the core instance:',
      '<Subscribe',
      '  source={row.table.atoms.rowSelection}',
      '  selector={(selection) => Boolean(selection[row.id])}',
      '>',
      '  {(selected) => <tr data-selected={selected}>...</tr>}',
      '</Subscribe>',
    ].join('\n'),
    points: [
      { term: 'table.state', description: 'The selected value, for reads during render. It contains only what the selector projected.' },
      { term: 'table.Subscribe', description: 'Component-level subscription from the instance you got back from useTable. Pass source to watch one atom instead of the whole store.' },
      { term: 'Subscribe (standalone)', description: 'Import it directly inside cell and header renderers — there, table is typed as the core Table and has no .Subscribe.' },
      { term: 'atoms.x.get()', description: 'A snapshot, never a subscription. Correct as a memo dependency, wrong as the value a component renders from.' },
      { term: 'table.store.state', description: 'Deprecated for render reads for the same reason. Prefer table.state.' },
    ],
  },
  {
    id: 'state-ownership',
    icon: Link2,
    title: 'State ownership — pick exactly one owner',
    summary:
      'Every registered slice has exactly one owner. Declaring a slice in two places and relying on precedence (atoms beat state beats internal) is how tables start fighting their own UI. Both grids on this page make the choice explicitly and differently.',
    codeFilename: 'hooks/useTableSearchParams.ts',
    code: [
      '// 1. Internal (default) — nothing outside needs it.',
      'const table = useTable({ features, columns, data })',
      'table.setSorting([{ id: "name", desc: false }])',
      '',
      '// 2. initialState — the starting and reset value only.',
      'initialState: { columnVisibility: { location: false } }',
      '',
      '// 3. External atom — another subsystem owns the slice.',
      'const rowSelection = useCreateAtom({})',
      'useTable({ features, columns, data, atoms: { rowSelection } })',
      '',
      '// 4. Controlled state + on<Slice>Change — here the URL is the store.',
      'useTable({',
      '  features, columns, data,',
      '  state: { pagination, sorting, globalFilter },',
      '  onPaginationChange: (updater) => {',
      '    // Table APIs pass updater functions, not values.',
      '    const next = functionalUpdate(updater, current)',
      '    setSearchParams(...)',
      '  },',
      '})',
    ].join('\n'),
    points: [
      { term: 'Grid 1', description: 'pagination, sorting and globalFilter are owned by the URL through option 4, so a refresh, a shared link and the back button all reproduce the view.' },
      { term: 'Grid 2', description: 'rowSelection uses option 3 because a bulk-action bar outside the table reads and clears it; columnVisibility and columnSizing stay internal.' },
      { term: 'Never both', description: 'An external atom plus on<Slice>Change for the same slice means two writers. Pick the atom and drop the callback.' },
      { term: 'No global onStateChange', description: 'v8’s single callback is gone. Control slices individually, or subscribe to table.store to observe everything.' },
      { term: 'Resets', description: 'Feature resets (table.resetSorting()) flow through the feature updater and reach the external owner. Core table.reset() only touches internal base atoms.' },
    ],
  },
  {
    id: 'performance',
    icon: Gauge,
    title: 'Performance tuning',
    summary:
      'Almost every "the table re-renders too much" report is one of four things: an unstable model input, a selector that is wider than the component, a per-cell read of derived geometry, or client processing over a dataset the server should have narrowed.',
    codeFilename: 'lib/columnSizeVars.ts',
    code: [
      '// Module constant — not `data={rows ?? []}`, which is a new array',
      '// identity every render and invalidates every row model.',
      'const NO_EMPLOYEES: Employee[] = []',
      '',
      '// Widths collected once per commit, written to the table element,',
      '// and read back by CSS — no cell ever calls column.getSize().',
      'const sizeVars = buildColumnSizeVars(table)',
      '',
      '<table style={sizeVars}>',
      '  <td style={{ width: "calc(var(--col-name-size) * 1px)" }} />',
    ].join('\n'),
    points: [
      { term: 'Stable inputs', description: 'features, columns and data must keep their identity between real changes. Subscriptions do not compensate for a new array every render.' },
      { term: 'Narrow selectors', description: 'Subscribe at the widest boundary that actually needs the slice, then use Subscribe lower down for the rest.' },
      { term: 'CSS variables', description: 'With columnResizeMode "onChange", widths commit on every animation frame. Reading getSize() in every cell multiplies that by the column count.' },
      { term: 'Measure first', description: 'Wrapping every cell in Subscribe is slower than the default. The per-row subscription here exists because selection is a row-level interaction over 184 rows.' },
      { term: 'Server processing', description: 'manual* flags move filtering, sorting and paging to the backend. The renderer and the columns do not change.' },
    ],
  },
  {
    id: 'pitfalls',
    icon: TriangleAlert,
    title: 'Pitfalls worth memorising',
    summary:
      'The failure modes that cost the most time when moving to v9, in the order they usually appear.',
    points: [
      { term: 'v8 constructor', description: 'useReactTable + getCoreRowModel() is the v8 shape. v9 is useTable, and optional row models are feature slots, not table options.' },
      { term: 'Detached methods', description: 'const { getValue } = row breaks: v9 row, cell, column and header methods use their instance as this.' },
      { term: 'Unregistered API', description: 'table.setSorting is undefined until rowSortingFeature is registered. Treat a missing API as a missing plugin.' },
      { term: 'Fallback arrays', description: 'data={query.data ?? []} re-invalidates the row models on every render — and in some adapters loops them.' },
      { term: 'manual* means "already processed"', description: 'It never issues a request. The state has to reach the fetch layer separately.' },
      { term: 'Absent vs false', description: 'In columnVisibility only an explicit false hides a column. A missing key is visible.' },
      { term: 'Snapshot as subscription', description: 'atoms.rowSelection.get() returns the current value and never re-renders anything.' },
    ],
  },
]

/** Which capability each grid on this page exercises, at a glance. */
export const CAPABILITY_MATRIX = [
  { capability: 'Global search', feature: 'columnFilteringFeature + globalFilteringFeature', model: 'filteredRowModel', grid: 1 },
  { capability: 'Column sorting', feature: 'rowSortingFeature', model: 'sortedRowModel', grid: 1 },
  { capability: 'Pagination', feature: 'rowPaginationFeature', model: 'paginatedRowModel', grid: 1 },
  { capability: 'Column visibility', feature: 'columnVisibilityFeature', model: '—', grid: 2 },
  { capability: 'Row selection', feature: 'rowSelectionFeature', model: '—', grid: 2 },
  { capability: 'Column resizing', feature: 'columnSizingFeature + columnResizingFeature', model: '—', grid: 2 },
] as const
