import { useCreateAtom, useSelector, type Atom } from '@tanstack/react-store'
import { useTable, type RowSelectionState } from '@tanstack/react-table'
import { Mail, RotateCcw, Trash2, Users } from 'lucide-react'
import { useEffect, useState } from 'react'
import { EmptyState } from '@/components/common/EmptyState'
import {
  DataTable,
  DataTableViewOptions,
  readPersistedViewState,
  useDebouncedValue,
  useViewStateUserId,
  viewStateStorageKey,
  writePersistedViewState,
  type ViewState,
} from '@/components/data-table'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { useAllEmployees } from '@/features/demo-tables/data/api'
import { NO_EMPLOYEES } from '@/features/demo-tables/data/employees'
import {
  SAAS_GRID_TABLE_ID,
  getEmployeeRowId,
  saasGridColumns,
  saasGridDefaultColumn,
  saasGridFeatures,
  saasGridInitialVisibility,
} from '@/features/demo-tables/tables/saasGridTable'

/** The column-layout slice of `ViewState` this grid persists. */
type SaasGridLayout = Pick<ViewState, 'columnVisibility' | 'columnSizing' | 'columnOrder'>

/**
 * Grid 2 — column visibility, multi-row selection, resizable and reorderable
 * columns.
 *
 * Three ownership decisions worth reading before the code:
 *
 * - `rowSelection` lives in an **external atom**, because the bulk-action bar
 *   below the toolbar is not part of the table and still has to read and clear
 *   it. Table APIs (`row.toggleSelected`, `table.resetRowSelection`) write that
 *   atom directly, so there is no `onRowSelectionChange` glue.
 * - `columnVisibility`, `columnSizing` and `columnOrder` stay **internal** to
 *   the table — nothing outside the grid needs them as live state.
 * - That same layout slice is still mirrored out to `localStorage`, keyed per
 *   user and table (`ViewState` from `data-table/lib/viewState.ts`), so a
 *   reload restores it. `columnResizing` is deliberately excluded — it is
 *   transient drag state, and persisting it would rehydrate a phantom drag.
 */
export function AdvancedSaasGrid() {
  const { data = NO_EMPLOYEES, isLoading } = useAllEmployees(true)
  const userId = useViewStateUserId()
  const storageKey = viewStateStorageKey(SAAS_GRID_TABLE_ID, userId)

  // Read once, at mount — `initialState` is only ever consulted then.
  const [persistedLayout] = useState(() => readPersistedViewState<SaasGridLayout>(storageKey))

  // Stable for the component's lifetime — a new atom per render would reset
  // the selection on every keystroke elsewhere on the page.
  const rowSelection = useCreateAtom<RowSelectionState>({})

  const table = useTable(
    {
      features: saasGridFeatures,
      data,
      columns: saasGridColumns,

      // Selection is stored as a set of row ids. Index-based ids would point at
      // whatever row later lands in that position.
      getRowId: getEmployeeRowId,

      defaultColumn: saasGridDefaultColumn,
      initialState: {
        columnVisibility: persistedLayout?.columnVisibility ?? saasGridInitialVisibility,
        columnSizing: persistedLayout?.columnSizing ?? {},
        columnOrder: persistedLayout?.columnOrder ?? [],
      },

      // External owner for one slice; everything else stays internal.
      atoms: { rowSelection },

      enableRowSelection: true,
      enableMultiRowSelection: true,
      // Shift-click ranges are on by default and work through
      // `row.getToggleSelectedHandler()`.
      enableRowRangeSelection: true,

      // 'onChange' commits widths on every animation frame of a drag;
      // 'onEnd' would only write them when the pointer is released.
      columnResizeMode: 'onChange',
      columnResizeDirection: 'ltr',
    },
    // Narrow selector: this component re-renders for layout state only.
    // `rowSelection` is deliberately absent — ticking a checkbox must not
    // re-render 184 rows, and the pieces that care subscribe themselves.
    (state) => ({
      columnVisibility: state.columnVisibility,
      columnSizing: state.columnSizing,
      columnResizing: state.columnResizing,
      columnOrder: state.columnOrder,
    }),
  )

  // Debounced so a resize drag (which commits on every frame) writes to
  // storage once it settles, not sixty times a second.
  const layoutSnapshot = JSON.stringify({
    columnVisibility: table.state.columnVisibility,
    columnSizing: table.state.columnSizing,
    columnOrder: table.state.columnOrder,
  } satisfies SaasGridLayout)
  const debouncedLayoutSnapshot = useDebouncedValue(layoutSnapshot, 400)

  useEffect(() => {
    writePersistedViewState(storageKey, JSON.parse(debouncedLayoutSnapshot) as SaasGridLayout)
  }, [storageKey, debouncedLayoutSnapshot])

  return (
    <Card className="gap-0 overflow-hidden p-0">
      <div className="border-border flex flex-wrap items-center justify-between gap-3 border-b px-4 py-3">
        <div className="min-w-0">
          <h3 className="text-[15px] leading-5 font-semibold">Workspace directory</h3>
          <p className="text-muted-foreground mt-0.5 text-xs">
            Drag a column edge to resize · double-click an edge to reset · Shift-click a checkbox
            to select a range
          </p>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <DataTableViewOptions table={table} />
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              table.resetColumnSizing()
              table.resetColumnOrder(true)
            }}
          >
            <RotateCcw aria-hidden strokeWidth={1.75} />
            Reset layout
          </Button>
        </div>
      </div>

      <SelectionActionBar
        selection={rowSelection}
        onClear={() => table.resetRowSelection()}
      />

      <DataTable
        table={table}
        resizable
        caption="Workspace directory with resizable columns and multi-row selection"
        isLoading={isLoading}
        empty={
          <EmptyState
            icon={Users}
            title="No members to show"
            description="Once members join this workspace they appear here."
          />
        }
      />
    </Card>
  )
}

interface SelectionActionBarProps {
  selection: Atom<RowSelectionState>
  onClear: () => void
}

/**
 * The reason `rowSelection` is externally owned.
 *
 * This bar subscribes to the atom directly through `useSelector` — it is a
 * consumer of the slice, not of the table — while still using table APIs to act
 * on it. Because the grid's own selector excludes `rowSelection`, this is the
 * only thing that re-renders when a checkbox is ticked.
 */
function SelectionActionBar({ selection, onClear }: SelectionActionBarProps) {
  const count = useSelector(selection, (state) => Object.keys(state).length)
  if (count === 0) return null

  return (
    <div className="bg-accent border-border flex flex-wrap items-center justify-between gap-3 border-b px-4 py-2">
      <p className="text-[13px] font-medium">
        {count} {count === 1 ? 'member' : 'members'} selected
      </p>
      <div className="flex items-center gap-2">
        <Button variant="outline" size="sm">
          <Mail aria-hidden strokeWidth={1.75} />
          Email
        </Button>
        <Button variant="destructive" size="sm">
          <Trash2 aria-hidden strokeWidth={1.75} />
          Remove
        </Button>
        <Button variant="ghost" size="sm" onClick={onClear}>
          Clear
        </Button>
      </div>
    </div>
  )
}
