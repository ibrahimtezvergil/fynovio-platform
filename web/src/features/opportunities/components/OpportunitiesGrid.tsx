import { useCreateAtom, useSelector } from '@tanstack/react-store'
import { useTable, type RowSelectionState } from '@tanstack/react-table'
import { ChevronLeft, ChevronRight, Download, Inbox, X } from 'lucide-react'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { EmptyState } from '@/components/common/EmptyState'
import { DataTable } from '@/components/data-table'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { exportRowsToCsv } from '@/lib/importExport/exportCsv'
import { paths } from '@/routes/paths'
import { PAGE_SIZE } from '../api'
import type { OpportunityRow } from '../lib/rows'
import { createOpportunityColumns, getOpportunityRowId, opportunityFeatures } from './opportunitiesTable'

interface OpportunitiesGridProps {
  rows: readonly OpportunityRow[]
  isLoading: boolean
  /** Dims the rows during a background refetch instead of blanking them. */
  refreshing: boolean
  /** Zero-based server page. */
  page: number
  hasNext: boolean
  /** Rows the server returned for this page, before the client-side filters. */
  loadedCount: number
  onPageChange: (page: number) => void
}

/**
 * Selection lives in an external atom so the selection bar — which is not part of the table — can read and clear it
 * without every row re-rendering. Rows are read-only: there is no row menu or bulk edit here until the edit work starts.
 */
export function OpportunitiesGrid({ rows, isLoading, refreshing, page, hasNext, loadedCount, onPageChange }: OpportunitiesGridProps) {
  const { t } = useTranslation('opportunities')
  const navigate = useNavigate()
  const rowSelection = useCreateAtom<RowSelectionState>({})
  const columns = useMemo(() => createOpportunityColumns(t), [t])

  const table = useTable(
    {
      features: opportunityFeatures,
      data: rows as OpportunityRow[],
      columns,
      getRowId: getOpportunityRowId,
      atoms: { rowSelection },
      enableRowSelection: true,
      enableMultiRowSelection: true,
      enableRowRangeSelection: true,
    },
    // `rowSelection` is deliberately absent: ticking a checkbox must not re-render the rows.
    (state) => ({ sorting: state.sorting }),
  )

  const selectedCount = useSelector(rowSelection, (state) => Object.keys(state).length)
  const goTo = (next: number) => {
    table.resetRowSelection()
    onPageChange(next)
  }

  return (
    <Card className="gap-0 rounded-[var(--nx-r-panel)] p-0">
      {selectedCount > 0 && (
        <div className="flex flex-wrap items-center gap-3 border-b border-[var(--nx-hairline)] bg-[var(--nx-tint-fill)] px-6 py-3">
          <span className="tnum text-[13px] font-[590] text-[var(--nx-tint)]">{t('list.grid.selectedCount', { count: selectedCount })}</span>
          <span className="flex-1" />
          <Button variant="ghost" size="icon-sm" aria-label={t('list.grid.clearSelection')} onClick={() => table.resetRowSelection()}>
            <X aria-hidden strokeWidth={1.7} />
          </Button>
        </div>
      )}

      {/* No `density` prop: the grid inherits the app-wide preference. */}
      <DataTable
        table={table}
        minWidth={980}
        isLoading={isLoading || refreshing}
        caption={t('list.grid.caption')}
        rowTestId="opportunity-row"
        onRowClick={(row) => navigate(paths.crmOpportunity(row.id))}
        empty={<EmptyState icon={Inbox} title={t('list.grid.noMatchTitle')} description={t('list.grid.noMatchDescription')} />}
      />

      <div className="flex flex-wrap items-center gap-3 border-t border-[var(--nx-hairline)] px-[var(--nx-d-cell-x-edge)] py-[var(--nx-d-gap)]">
        <span className="tnum text-muted-foreground text-[12.5px]" aria-live="polite">
          {refreshing ? t('list.refreshing') : t('list.page', { page: page + 1, size: PAGE_SIZE })}
          {rows.length !== loadedCount && ` · ${t('list.filters.showing', { visible: rows.length, total: loadedCount })}`}
        </span>

        <span className="flex-1" />
        <Button
          variant="secondary"
          size="sm"
          disabled={rows.length === 0}
          onClick={() =>
            exportRowsToCsv(
              rows,
              [
                { key: 'id', header: t('list.columns.id') },
                { key: 'party', header: t('list.columns.party') },
                { key: 'owner', header: t('list.columns.owner') },
                { key: 'status', header: t('list.columns.status') },
                { key: 'stage', header: t('list.columns.stage') },
                { key: 'amount', header: t('list.columns.amount') },
                { key: 'currency', header: t('list.columns.currency') },
                { key: 'expiryDate', header: t('list.columns.expiry') },
              ],
              t('list.grid.exportFile'),
            )
          }
        >
          <Download aria-hidden strokeWidth={1.7} /> {t('list.grid.export')}
        </Button>

        <Button variant="secondary" size="sm" disabled={page === 0} onClick={() => goTo(page - 1)}>
          <ChevronLeft aria-hidden strokeWidth={1.7} /> {t('list.previous')}
        </Button>
        <Button variant="secondary" size="sm" disabled={!hasNext} onClick={() => goTo(page + 1)}>
          {t('list.next')} <ChevronRight aria-hidden strokeWidth={1.7} />
        </Button>
      </div>
    </Card>
  )
}
