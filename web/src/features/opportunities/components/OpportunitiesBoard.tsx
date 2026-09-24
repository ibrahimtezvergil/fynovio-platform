import { ChevronLeft, ChevronRight, Inbox } from 'lucide-react'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { EmptyState } from '@/components/common/EmptyState'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { paths } from '@/routes/paths'
import { PAGE_SIZE } from '../api'
import { formatMoney } from '../lib/format'
import type { OpportunityRow } from '../lib/rows'
import { OpportunityStatusBadge } from './OpportunityStatusBadge'

interface OpportunitiesBoardProps {
  rows: readonly OpportunityRow[]
  /** Dims the cards during a background refetch instead of blanking them. */
  refreshing: boolean
  /** Zero-based server page. */
  page: number
  hasNext: boolean
  /** Rows the server returned for this page, before the client-side filters. */
  loadedCount: number
  onPageChange: (page: number) => void
  returnTo: string
}

interface Column {
  stageId: number | null
  label: string | null
  rows: OpportunityRow[]
}

/** One column per pipeline stage present on the loaded page; opportunities not yet in a pipeline (drafts) come first. */
function toColumns(rows: readonly OpportunityRow[]): Column[] {
  const byStage = new Map<number | null, Column>()
  for (const row of rows) {
    const column = byStage.get(row.stageId) ?? { stageId: row.stageId, label: row.stage, rows: [] }
    column.rows.push(row)
    byStage.set(row.stageId, column)
  }
  return [...byStage.values()].toSorted((a, b) => (a.stageId ?? -1) - (b.stageId ?? -1))
}

/** The board is another projection of the same filtered page the grid shows — same rows, same paging. */
export function OpportunitiesBoard({ rows, refreshing, page, hasNext, loadedCount, onPageChange, returnTo }: OpportunitiesBoardProps) {
  const { t } = useTranslation('opportunities')
  const columns = useMemo(() => toColumns(rows), [rows])

  return (
    <div className="flex flex-col gap-3">
      {columns.length === 0 ? (
        <Card className="rounded-[var(--nx-r-panel)] p-0">
          <EmptyState icon={Inbox} title={t('list.grid.noMatchTitle')} description={t('list.grid.noMatchDescription')} />
        </Card>
      ) : (
        <div className={`flex gap-3 overflow-x-auto pb-1 transition-opacity ${refreshing ? 'opacity-50' : ''}`} aria-busy={refreshing || undefined}>
          {columns.map((column) => (
            <Card key={column.stageId ?? 'none'} className="min-w-56 flex-1 gap-3 rounded-[var(--nx-r-panel)] p-3.5">
              <div className="flex items-center justify-between gap-2">
                <h3 className="truncate text-[12.5px] font-[590]">
                  {column.stageId == null ? t('list.board.noStage') : (column.label ?? t('list.stageId', { id: column.stageId }))}
                </h3>
                <span className="text-muted-foreground tnum text-[11.5px]">{column.rows.length}</span>
              </div>
              <div className="flex flex-col gap-2">
                {column.rows.map((row) => (
                  <Link
                    key={row.id}
                    to={paths.crmOpportunity(row.id, returnTo)}
                    data-testid="opportunity-card"
                    className="flex flex-col gap-1 rounded-md border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-3 transition-colors hover:bg-[var(--nx-fill-hover)]"
                  >
                    <span className="flex items-center justify-between gap-2">
                      <span className="text-primary text-[12.5px] font-[590] tabular-nums">#{row.id}</span>
                      <OpportunityStatusBadge status={row.status} size="sm" />
                    </span>
                    <span className="truncate text-[12.5px] font-[550]">
                      {row.party ?? (row.partyId == null ? '—' : t('list.partyId', { id: row.partyId }))}
                    </span>
                    <span className="tnum text-[12px] font-[550]">{formatMoney(row.amount, row.currency) ?? '—'}</span>
                  </Link>
                ))}
              </div>
            </Card>
          ))}
        </div>
      )}

      <div className="flex flex-wrap items-center gap-3">
        <span className="tnum text-muted-foreground text-[12.5px]" aria-live="polite">
          {refreshing ? t('list.refreshing') : t('list.page', { page: page + 1, size: PAGE_SIZE })}
          {rows.length !== loadedCount && ` · ${t('list.filters.showing', { visible: rows.length, total: loadedCount })}`}
        </span>
        <span className="flex-1" />
        <Button variant="secondary" size="sm" disabled={page === 0} onClick={() => onPageChange(page - 1)}>
          <ChevronLeft aria-hidden strokeWidth={1.7} /> {t('list.previous')}
        </Button>
        <Button variant="secondary" size="sm" disabled={!hasNext} onClick={() => onPageChange(page + 1)}>
          {t('list.next')} <ChevronRight aria-hidden strokeWidth={1.7} />
        </Button>
      </div>
    </div>
  )
}
