import {
  ArrowUpDown,
  CheckSquare,
  Eye,
  MoveHorizontal,
  Rows3,
  Search,
  type LucideIcon,
} from 'lucide-react'
import type { DocPill } from '@/features/demo-tables/components/DocCard'

export interface FeatureDoc {
  id: string
  icon: LucideIcon
  title: string
  /** Which demo grid on this page shows the capability. */
  grid: 'Grid 1 — Core admin list' | 'Grid 2 — Advanced SaaS grid'
  summary: string
  pills: DocPill[]
  code: string
  codeFilename: string
  /** Knobs a developer reaches for first. */
  options: { term: string; description: string }[]
  /** The mistake this feature is most often paired with. */
  pitfall: string
}

export const FEATURE_DOCS: FeatureDoc[] = [
  {
    id: 'sorting',
    icon: ArrowUpDown,
    title: 'Column sorting',
    grid: 'Grid 1 — Core admin list',
    summary:
      'rowSortingFeature adds the sorting state and the column APIs; sortedRowModel is what actually reorders rows. Register the plugin without the model and the state changes while the rows sit still.',
    pills: [
      { label: 'rowSortingFeature', tone: 'feature' },
      { label: 'createSortedRowModel()', tone: 'model' },
      { label: 'sortFns', tone: 'option' },
      { label: 'state.sorting', tone: 'state' },
    ],
    codeFilename: 'tables/coreAdminTable.tsx',
    code: [
      'export const features = tableFeatures({',
      '  rowSortingFeature,',
      '  sortedRowModel: createSortedRowModel(),',
      '  // Only the comparators the columns name by string.',
      '  sortFns: { text: sortFn_text, datetime: sortFn_datetime },',
      '})',
      '',
      '// In the header cell — the whole sorting UI:',
      'const canSort = column.getCanSort()',
      'const sorted = column.getIsSorted() // false | "asc" | "desc"',
      'onClick={column.getToggleSortingHandler()}',
    ].join('\n'),
    options: [
      { term: 'enableSorting', description: 'Table- or column-level off switch, without removing the plugin.' },
      { term: 'enableMultiSort', description: 'Shift-click adds a second sort key. On by default; column.getSortIndex() gives the badge number.' },
      { term: 'enableSortingRemoval', description: 'Whether the third click clears the sort or cycles back to ascending.' },
      { term: 'sortDescFirst', description: 'Money and counts usually want descending first. Set per column.' },
      { term: 'sortUndefined', description: "'first' | 'last' | -1 | 1 | false — where empty values land." },
      { term: 'manualSorting', description: 'Skip sortedRowModel entirely; the incoming data is already ordered.' },
    ],
    pitfall:
      'Custom comparators must return the ascending comparison only. Table reverses the result for descending — a comparator that flips its own operands sorts the same way in both directions.',
  },
  {
    id: 'global-filtering',
    icon: Search,
    title: 'Global search',
    grid: 'Grid 1 — Core admin list',
    summary:
      'Global filtering rides on the column-filtering pipeline, so it needs columnFilteringFeature next to it and filteredRowModel to do the work. tableFeatures() rejects the pair at compile time if either is missing.',
    pills: [
      { label: 'columnFilteringFeature', tone: 'feature' },
      { label: 'globalFilteringFeature', tone: 'feature' },
      { label: 'createFilteredRowModel()', tone: 'model' },
      { label: 'state.globalFilter', tone: 'state' },
    ],
    codeFilename: 'tables/coreAdminTable.tsx',
    code: [
      'export const features = tableFeatures({',
      '  columnFilteringFeature,   // prerequisite',
      '  globalFilteringFeature,',
      '  filteredRowModel: createFilteredRowModel(),',
      "  filterFns: { includesString: filterFn_includesString },",
      '})',
      '',
      '// Default eligibility inspects the first core row and accepts any',
      '// string or number — say what should really be searchable.',
      'const options = {',
      "  globalFilterFn: 'includesString',",
      '  getColumnCanGlobalFilter: (column) => SEARCHABLE.has(column.id),',
      '}',
    ].join('\n'),
    options: [
      { term: 'globalFilterFn', description: "Any registered filterFns key, or a function. 'includesString' is the usual default." },
      { term: 'getColumnCanGlobalFilter', description: 'Per-column eligibility. Without it, numeric and ISO-date columns join the text search.' },
      { term: 'enableGlobalFilter', description: 'Column-level opt-out on the column definition.' },
      { term: 'manualFiltering', description: 'Server owns matching; the value must be forwarded into the request.' },
    ],
    pitfall:
      'manualFiltering does not send anything anywhere. If the filter value is not also in the query key, the state changes and the rows never do.',
  },
  {
    id: 'pagination',
    icon: Rows3,
    title: 'Pagination — client and server',
    grid: 'Grid 1 — Core admin list',
    summary:
      'One feature covers both modes. In client mode paginatedRowModel slices the processed rows; in manual mode the table trusts data as the current page and needs rowCount to know where the end is.',
    pills: [
      { label: 'rowPaginationFeature', tone: 'feature' },
      { label: 'createPaginatedRowModel()', tone: 'model' },
      { label: 'manualPagination', tone: 'option' },
      { label: 'state.pagination', tone: 'state' },
    ],
    codeFilename: 'components/CoreAdminGrid.tsx',
    code: [
      'const table = useTable({',
      '  features,',
      '  columns,',
      '  data: isServer ? page.rows : allRows,',
      '',
      '  manualPagination: isServer,',
      '  manualSorting: isServer,',
      '  manualFiltering: isServer,',
      '  rowCount: isServer ? page.rowCount : undefined,',
      '',
      '  // The URL owns the page index; do not let a data change rewrite it.',
      '  autoResetPageIndex: false,',
      '})',
    ].join('\n'),
    options: [
      { term: 'rowCount', description: 'Total rows on the server. pageCount is derived from it.' },
      { term: 'pageCount', description: 'Use instead of rowCount when the total is unknown; -1 means "there may be more".' },
      { term: 'autoResetPageIndex', description: 'Default behaviour resets to page 1 when the data changes. Turn it off when something else owns the page.' },
      { term: 'initialState.pagination', description: 'Starting and reset value — { pageIndex: 0, pageSize: 25 }.' },
    ],
    pitfall:
      'Passing the whole dataset together with manualPagination shows every row on every page. Manual mode never slices.',
  },
  {
    id: 'column-visibility',
    icon: Eye,
    title: 'Column visibility',
    grid: 'Grid 2 — Advanced SaaS grid',
    summary:
      'No row model — visibility does not reshape the pipeline, it changes which columns the visibility-aware APIs return. Render from getVisibleCells(); build the menu from getAllLeafColumns().',
    pills: [
      { label: 'columnVisibilityFeature', tone: 'feature' },
      { label: 'no row model', tone: 'model' },
      { label: 'state.columnVisibility', tone: 'state' },
    ],
    codeFilename: 'tables/saasGridTable.tsx',
    code: [
      'const features = tableFeatures({ columnVisibilityFeature })',
      '',
      '// Hiding a column at startup is state, not a column flag.',
      'const initialState = { columnVisibility: { location: false, seats: false } }',
      '',
      '// enableHiding: false only removes it from the menu.',
      'helper.display({ id: "select", enableHiding: false })',
      '',
      '// Menu: every leaf column. Body: only the visible cells.',
      'table.getAllLeafColumns().filter((column) => column.getCanHide())',
      'row.getVisibleCells()',
    ].join('\n'),
    options: [
      { term: 'enableHiding', description: 'Whether the column may be hidden at all. Not the same as being hidden.' },
      { term: 'initialState.columnVisibility', description: 'Only an explicit false hides a column; a missing key means visible.' },
      { term: 'table.resetColumnVisibility()', description: 'Back to initialState — what the "Reset to defaults" item calls.' },
    ],
    pitfall:
      'row.getAllCells() ignores visibility on purpose. Rendering with it leaves hidden columns in the DOM and misaligns every header.',
  },
  {
    id: 'row-selection',
    icon: CheckSquare,
    title: 'Multi-row selection',
    grid: 'Grid 2 — Advanced SaaS grid',
    summary:
      'Selection is a set of row ids, independent of the data. It needs no row model and no sorting — but it does need a stable getRowId, or a selection survives a re-sort by pointing at the wrong rows.',
    pills: [
      { label: 'rowSelectionFeature', tone: 'feature' },
      { label: 'no row model', tone: 'model' },
      { label: 'getRowId', tone: 'option' },
      { label: 'state.rowSelection', tone: 'state' },
    ],
    codeFilename: 'components/AdvancedSaasGrid.tsx',
    code: [
      'const features = tableFeatures({ rowSelectionFeature })',
      '',
      '// A real checkbox: Shift-range detection reads the click event, and',
      "// React's onChange carries it through event.nativeEvent.",
      '<input',
      '  type="checkbox"',
      '  checked={selected}',
      '  onChange={row.getToggleSelectedHandler()}',
      '/>',
      '',
      '// Ids for intent, row models for what is loaded.',
      'table.getSelectedRowIds()          // every selected id',
      'table.getSelectedRowModel().rows   // only rows currently in data',
    ].join('\n'),
    options: [
      { term: 'getRowId', description: 'Stable application id. The single most important option for selection.' },
      { term: 'enableRowSelection', description: 'Boolean or a per-row predicate — locks specific rows out.' },
      { term: 'enableMultiRowSelection', description: 'Set false for radio-style single selection.' },
      { term: 'enableRowRangeSelection', description: 'Shift-click ranges. On by default; only the handler moves the anchor.' },
      { term: 'enableSubRowSelection', description: 'Whether selecting a parent cascades to its children.' },
    ],
    pitfall:
      'Deleting a row does not clear its id from the selection. State that outlives the data is a feature under server pagination and a bug everywhere else — prune it when rows disappear.',
  },
  {
    id: 'column-resizing',
    icon: MoveHorizontal,
    title: 'Resizable columns',
    grid: 'Grid 2 — Advanced SaaS grid',
    summary:
      'columnResizingFeature contributes the drag gesture and writes into the sizing state, so columnSizingFeature has to be registered with it. Both produce numbers only — turning them into widths is the renderer’s job.',
    pills: [
      { label: 'columnSizingFeature', tone: 'feature' },
      { label: 'columnResizingFeature', tone: 'feature' },
      { label: 'columnResizeMode', tone: 'option' },
      { label: 'state.columnSizing', tone: 'state' },
    ],
    codeFilename: 'DataTableHeaderCell.tsx',
    code: [
      'const features = tableFeatures({',
      '  columnSizingFeature,     // prerequisite',
      '  columnResizingFeature,',
      '})',
      '',
      '// Mouse and touch separately: the shipped handler branches on',
      '// touchstart, and a lone pointerdown leaves touch resizing dead.',
      '<div',
      '  onMouseDown={header.getResizeHandler()}',
      '  onTouchStart={header.getResizeHandler()}',
      '  onDoubleClick={() => column.resetSize()}',
      '/>',
      '',
      '// Sizes written once per frame as CSS variables, not per cell.',
      'style={{ "--col-name-size": header.getSize() }}',
    ].join('\n'),
    options: [
      { term: 'columnResizeMode', description: "'onChange' commits widths during the drag; 'onEnd' only on release." },
      { term: 'columnResizeDirection', description: "'ltr' or 'rtl' — flips the sign of the drag delta." },
      { term: 'enableColumnResizing', description: 'Table-level switch; enableResizing does the same per column.' },
      { term: 'defaultColumn', description: '{ size, minSize, maxSize } floor and ceiling for every column.' },
      { term: 'column.resetSize()', description: 'Back to the column definition size — bound to double-click here.' },
    ],
    pitfall:
      'Sizing state is numeric. A column with size: "25%" is not a percentage, it is a type error waiting to become a layout that no longer matches getTotalSize().',
  },
]
