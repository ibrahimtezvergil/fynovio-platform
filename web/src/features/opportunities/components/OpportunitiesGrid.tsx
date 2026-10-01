import { useCreateAtom, useSelector } from '@tanstack/react-store'
import { useTable, type RowSelectionState } from '@tanstack/react-table'
import { ChevronLeft, ChevronRight, Download, Inbox, X } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { EmptyState } from '@/components/common/EmptyState'
import { DataTable } from '@/components/data-table'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { useCustomFieldDefinitions } from '@/lib/custom-fields/api'
import { activeFields } from '@/lib/custom-fields/values'
import { exportRowsToCsv } from '@/lib/importExport/exportCsv'
import type { SharedView } from '@/lib/shared-views/schema'
import { paths } from '@/routes/paths'
import { PAGE_SIZE } from '../api'
import type { OpportunityRow } from '../lib/rows'
import { applySharedView } from '../lib/views'
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
  returnTo: string
  /** A shared table view: which columns, in what order. Null shows every column. */
  tableView?: SharedView | null
}

/**
 * Selection lives in an external atom so the selection bar — which is not part of the table — can read and clear it
 * without every row re-rendering. Row actions are local UI prototypes until their backend commands are available.
 */
export function OpportunitiesGrid({ rows, isLoading, refreshing, page, hasNext, loadedCount, onPageChange, returnTo, tableView }: OpportunitiesGridProps) {
  const { t, i18n } = useTranslation('opportunities')
  const definitions = useCustomFieldDefinitions()
  const customFields = useMemo(() => activeFields(definitions.data ?? []), [definitions.data])
  const navigate = useNavigate()
  const rowSelection = useCreateAtom<RowSelectionState>({})
  const [localEdits, setLocalEdits] = useState<Partial<Record<number, Partial<OpportunityRow>>>>({})
  const visibleRows = useMemo(() => rows.map((row) => ({ ...row, ...localEdits[row.id] })), [rows, localEdits])
  const applyLocalEdit = useCallback((id: number, changes: Partial<OpportunityRow>) => setLocalEdits((current) => ({ ...current, [id]: { ...current[id], ...changes } })), [])
  const columns = useMemo(() => {
    const all = createOpportunityColumns(t, returnTo, applyLocalEdit, customFields, i18n.language)
    // The view only selects and orders columns that already exist, so the result keeps the table's column type.
    return applySharedView(all, tableView, (column) => (column as { id?: string; accessorKey?: string }).id ?? (column as { accessorKey?: string }).accessorKey) as typeof all
  }, [t, returnTo, applyLocalEdit, customFields, i18n.language, tableView])

  const table = useTable(
    {
      features: opportunityFeatures,
      data: visibleRows as OpportunityRow[],
      columns,
      getRowId: getOpportunityRowId,
      atoms: { rowSelection },
      enableRowSelection: true,
      enableMultiRowSelection: true,
      enableRowRangeSelection: true,
    },
    // `rowSelection` is deliberately absent: ticking a checkbox must not re-render the rows.
    () => ({}),
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
          <span role="status" className="tnum text-[13px] font-[590] text-[var(--nx-tint)]">{t('list.grid.selectedCount', { count: selectedCount })}</span>
          <span className="flex-1" />
          <Button
            variant="secondary"
            size="sm"
            onClick={() =>
              exportRowsToCsv(
                table.getSelectedRowModel().rows.map((row) => row.original),
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
                t('list.grid.selectedExportFile'),
              )
            }
          >
            <Download aria-hidden strokeWidth={1.7} /> {t('list.grid.exportSelected')}
          </Button>
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
          <Download aria-hidden strokeWidth={1.7} /> {t('list.grid.exportPage')}
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
