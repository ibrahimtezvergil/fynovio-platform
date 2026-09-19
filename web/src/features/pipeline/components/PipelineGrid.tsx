import { useCreateAtom, useSelector, type Atom } from '@tanstack/react-store'
import { useTable, type RowSelectionState } from '@tanstack/react-table'
import { ChevronLeft, ChevronRight, Download, Inbox, Trash2, UserRoundCog, Workflow, X } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { EmptyState } from '@/components/common/EmptyState'
import { StageBadge } from '@/components/common/StageBadge'
import { DataTable } from '@/components/data-table'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { OWNERS } from '@/features/pipeline/data/deals'
import { DealPeekDrawer } from '@/features/pipeline/components/DealPeekDrawer'
import { money, percent } from '@/features/pipeline/data/format'
import {
  createPipelineColumns,
  getDealRowId,
  pipelineFeatures,
  type PipelineRowActions,
} from '@/features/pipeline/table/pipelineTable'
import { initialsOf } from '@/lib/utils'
import { STAGES, type Deal } from '@/types'
import { exportRowsToCsv } from '@/lib/importExport/exportCsv'
import {
  emptySelection,
  resolveSelectionCount,
  resolveSelectionIds,
  toggleSelectAllResults,
  type SelectionState,
} from '@/components/data-table/lib/selection'

interface PipelineGridProps {
  deals: Deal[]
  isLoading?: boolean
  actions: PipelineRowActions
}

/**
 * Selection lives in an external atom so the bulk-action bar — which is not
 * part of the table — can read and clear it without the grid re-rendering
 * every row on a tick. Sorting and pagination stay internal to the instance.
 */
export function PipelineGrid({ deals, isLoading, actions }: PipelineGridProps) {
  const { t } = useTranslation('pipeline')
  const rowSelection = useCreateAtom<RowSelectionState>({})
  const [allResultsSelection, setAllResultsSelection] = useState<SelectionState | null>(null)
  const [activeDealId, setActiveDealId] = useState<string | null>(null)
  const columns = useMemo(() => createPipelineColumns(actions, t), [actions, t])

  const table = useTable(
    {
      features: pipelineFeatures,
      data: deals,
      columns,
      getRowId: getDealRowId,
      atoms: { rowSelection },
      enableRowSelection: true,
      enableMultiRowSelection: true,
      enableRowRangeSelection: true,
      initialState: {
        // The artboard opens on the biggest deals first.
        sorting: [{ id: 'value', desc: true }],
        pagination: { pageIndex: 0, pageSize: 10 },
      },
    },
    // `rowSelection` is deliberately absent: ticking a checkbox must not
    // re-render the page of rows.
    (state) => ({ sorting: state.sorting, pagination: state.pagination }),
  )

  const rows = table.getRowModel().rows
  const pageTotal = rows.reduce((sum, row) => sum + row.original.value, 0)
  const pageProbability =
    rows.length === 0
      ? 0
      : rows.reduce((sum, row) => sum + row.original.probability, 0) / rows.length

  const { pageIndex, pageSize } = table.state.pagination ?? { pageIndex: 0, pageSize: 10 }
  const rowCount = table.getRowCount()
  const pageCount = table.getPageCount()
  const firstRow = rowCount === 0 ? 0 : pageIndex * pageSize + 1
  const lastRow = Math.min(rowCount, (pageIndex + 1) * pageSize)

  return (
    <Card className="gap-0 rounded-[var(--nx-r-panel)] p-0">
      <BulkActionBar
        selection={rowSelection}
        actions={actions}
        deals={deals}
        allResultsSelection={allResultsSelection}
        onSelectAllResults={() => setAllResultsSelection((current) => toggleSelectAllResults(current ?? emptySelection()))}
        onClear={() => {
          table.resetRowSelection()
          setAllResultsSelection(null)
        }}
      />

      {/* No `density` prop: the grid inherits the app-wide preference. */}
      <DataTable
        table={table}
        minWidth={1060}
        isLoading={isLoading}
        caption={t('grid.caption')}
        onRowClick={(deal) => setActiveDealId(deal.id)}
        empty={
          <EmptyState icon={Inbox} title={t('grid.emptyTitle')} description={t('grid.emptyDescription')} />
        }
      />
      <DealPeekDrawer
        deals={rows.map((row) => row.original)}
        activeId={activeDealId}
        onActiveIdChange={setActiveDealId}
        onComplete={(id) => actions.onChangeStage([id], 'ready')}
      />

      {/* The artboard's tfoot totals ride in the footer instead: the kit's
          renderer owns thead/tbody only, and a page total is not a column.
          It reads the density gutter so it tightens with the rows above it. */}
      <div className="flex flex-wrap items-center gap-3 border-t border-[var(--nx-hairline)] px-[var(--nx-d-cell-x-edge)] py-[var(--nx-d-gap)]">
        <span className="tnum text-muted-foreground text-[12.5px]">
          {t('grid.rangeSummary', { count: rowCount, first: firstRow, last: lastRow })}
        </span>
        {rows.length > 0 && (
          <span className="tnum text-muted-foreground text-[12.5px]">
            · {t('grid.pageTotal')}{' '}
            <span className="text-foreground font-[650]">{money.format(pageTotal)}</span>{' '}
            · {t('grid.pageAverage', { value: percent.format(pageProbability) })}
          </span>
        )}

        <span className="flex-1" />
        <Button variant="secondary" size="sm" onClick={() => exportRowsToCsv(deals, [
          { key: 'title', header: 'Fırsat' }, { key: 'account', header: 'Müşteri' }, { key: 'owner', header: 'Sorumlu' }, { key: 'stage', header: 'Aşama' }, { key: 'value', header: 'Tutar' },
        ], 'pipeline.csv')}>
          <Download aria-hidden strokeWidth={1.7} /> Dışa aktar
        </Button>

        <Button
          variant="secondary"
          size="icon-sm"
          aria-label={t('grid.prevPage')}
          disabled={!table.getCanPreviousPage()}
          onClick={() => table.previousPage()}
        >
          <ChevronLeft aria-hidden strokeWidth={1.7} />
        </Button>
        <span className="tnum text-muted-foreground min-w-[52px] text-center text-[12.5px]">
          {pageCount === 0 ? 0 : pageIndex + 1} / {pageCount}
        </span>
        <Button
          variant="secondary"
          size="icon-sm"
          aria-label={t('grid.nextPage')}
          disabled={!table.getCanNextPage()}
          onClick={() => table.nextPage()}
        >
          <ChevronRight aria-hidden strokeWidth={1.7} />
        </Button>
      </div>
    </Card>
  )
}

interface BulkActionBarProps {
  selection: Atom<RowSelectionState>
  actions: PipelineRowActions
  deals: Deal[]
  allResultsSelection: SelectionState | null
  onSelectAllResults: () => void
  onClear: () => void
}

/**
 * The reason selection is externally owned: this bar consumes the slice
 * directly, so it is the only thing that re-renders when a checkbox is ticked.
 */
function BulkActionBar({ selection, actions, deals, allResultsSelection, onSelectAllResults, onClear }: BulkActionBarProps) {
  const { t } = useTranslation('pipeline')
  // Joined rather than an array: a fresh array every read would defeat the
  // selector's equality check and re-render this bar on unrelated commits.
  const ids = useSelector(selection, (state) => Object.keys(state).join(' '))
  const selected = ids === '' ? [] : ids.split(' ')
  const selectionState = allResultsSelection ?? { mode: 'include' as const, ids: new Set(selected) }
  const selectedCount = resolveSelectionCount(selectionState, deals.length)
  if (selectedCount === 0) return null
  const resolvedIds = resolveSelectionIds(selectionState, deals)

  const run = (mutate: () => void) => {
    mutate()
    onClear()
  }

  return (
    <div className="flex flex-wrap items-center gap-3 border-b border-[var(--nx-hairline)] bg-[var(--nx-tint-fill)] px-6 py-3">
      <span className="tnum text-[13px] font-[590] text-[var(--nx-tint)]">
        {allResultsSelection?.mode === 'exclude'
          ? t('bulkBar.allResultsSelected', { count: selectedCount, excluded: allResultsSelection.ids.size })
          : t('bulkBar.selectedCount', { count: selectedCount })}
      </span>
      {allResultsSelection?.mode !== 'exclude' && selected.length > 0 && (
        <Button variant="ghost" size="sm" onClick={onSelectAllResults}>
          {t('bulkBar.selectAllResults', { count: deals.length })}
        </Button>
      )}
      <span className="flex-1" />

      <DropdownMenu>
        <DropdownMenuTrigger
          render={
            <Button variant="secondary" size="sm">
              <UserRoundCog aria-hidden strokeWidth={1.7} />
              {t('bulkBar.assign')}
            </Button>
          }
        />
        <DropdownMenuContent align="end" className="w-52">
          {OWNERS.map((owner) => (
            <DropdownMenuItem
              key={owner}
              onClick={() => run(() => actions.onAssign(resolvedIds, owner))}
            >
              <span aria-hidden className="nx-avatar size-5 text-[9.5px]">
                {initialsOf(owner)}
              </span>
              {owner}
            </DropdownMenuItem>
          ))}
        </DropdownMenuContent>
      </DropdownMenu>

      <DropdownMenu>
        <DropdownMenuTrigger
          render={
            <Button variant="secondary" size="sm">
              <Workflow aria-hidden strokeWidth={1.7} />
              {t('bulkBar.changeStage')}
            </Button>
          }
        />
        <DropdownMenuContent align="end" className="w-52">
          {STAGES.map((stage) => (
            <DropdownMenuItem
              key={stage}
              onClick={() => run(() => actions.onChangeStage(resolvedIds, stage))}
            >
              <StageBadge stage={stage} />
            </DropdownMenuItem>
          ))}
        </DropdownMenuContent>
      </DropdownMenu>

      <Button variant="destructive" size="sm" onClick={() => run(() => actions.onRemove(resolvedIds))}>
        <Trash2 aria-hidden strokeWidth={1.7} />
        {t('bulkBar.remove')}
      </Button>

      <Button variant="ghost" size="icon-sm" aria-label={t('bulkBar.clearSelection')} onClick={onClear}>
        <X aria-hidden strokeWidth={1.7} />
      </Button>
    </div>
  )
}
