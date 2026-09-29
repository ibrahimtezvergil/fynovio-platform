import { useQueryClient } from '@tanstack/react-query'
import { ChevronLeft, ChevronRight, Hand, Inbox, X } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { EmptyState } from '@/components/common/EmptyState'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { PAGE_SIZE, opportunityKeys, useChangeStage, useTenantId } from '../api'
import { intentFor } from '../lib/boardMove'
import type { OpportunityRow } from '../lib/rows'
import { useBoardMove, type BoardTarget } from '../lib/useBoardMove'
import { useKeyedCommand } from '../lib/useKeyedCommand'
import type { PipelineStage, PipelineStageKind } from '../schema'
import { BoardColumn } from './BoardColumn'
import { LoseDialog, WinDialog } from './LifecycleDialogs'
import { ProblemNotice } from './ProblemNotice'

interface OpportunitiesBoardProps {
  rows: readonly OpportunityRow[]
  configuredStages: readonly PipelineStage[]
  /** Dims the cards during a background refetch instead of blanking them. */
  refreshing: boolean
  /** Archived cards are shown but never moved. */
  readOnly: boolean
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
  kind: PipelineStageKind
  sortOrder: number | null
  rows: OpportunityRow[]
}

/** Always shows the active default pipeline, including empty stages, then any historical stages in the loaded page. */
function toColumns(rows: readonly OpportunityRow[], configuredStages: readonly PipelineStage[]): Column[] {
  const byStage = new Map<number | null, Column>(configuredStages.filter((stage) => !stage.isArchived)
    .map((stage) => [stage.id, { stageId: stage.id, label: stage.name, kind: stage.kind, sortOrder: stage.sortOrder, rows: [] }]))
  for (const row of rows) {
    const column = byStage.get(row.stageId) ?? { stageId: row.stageId, label: row.stage, kind: 'Open' as const, sortOrder: null, rows: [] }
    column.rows.push(row)
    byStage.set(row.stageId, column)
  }
  return [...byStage.values()].toSorted((a, b) => {
    if (a.stageId === null && b.stageId === null) return 0
    if (a.stageId === null) return -1
    if (b.stageId === null) return 1
    if (a.sortOrder !== null && b.sortOrder !== null) return a.sortOrder - b.sortOrder
    if (a.sortOrder !== null) return -1
    if (b.sortOrder !== null) return 1
    return a.stageId - b.stageId
  })
}

/** A closing move waits for its confirmation dialog; the card stays where it is until the command succeeds. */
type ClosingMove = { intent: 'win' | 'lose'; row: OpportunityRow }

/**
 * The board is another projection of the same filtered page the grid shows — same rows, same paging.
 * A drop is a request, not a move: the card changes column only when the server has confirmed and the list has refreshed
 * (the Opportunity commands are deliberately not optimistic). Won and Lost columns open the win/lose dialogs — the
 * backend rejects a plain stage change into them.
 */
export function OpportunitiesBoard({ rows, configuredStages, refreshing, readOnly, page, hasNext, loadedCount, onPageChange, returnTo }: OpportunitiesBoardProps) {
  const { t } = useTranslation('opportunities')
  const queryClient = useQueryClient()
  const tenantId = useTenantId()
  const columns = useMemo(() => toColumns(rows, configuredStages), [rows, configuredStages])
  const stageCommand = useKeyedCommand(useChangeStage())
  const [movingId, setMovingId] = useState<number | null>(null)
  const [now] = useState(() => Date.now())
  const [closing, setClosing] = useState<ClosingMove | null>(null)

  const columnLabel = useCallback((column: Column) => (column.stageId == null ? t('list.board.noStage') : (column.label ?? t('list.stageId', { id: column.stageId }))), [t])
  const targets = useMemo<BoardTarget[]>(() => columns.map((column) => ({ stageId: column.stageId, kind: column.kind, label: columnLabel(column) })), [columns, columnLabel])

  const reloadList = useCallback(() => {
    stageCommand.reset()
    return queryClient.invalidateQueries({ queryKey: opportunityKeys.lists(tenantId) })
  }, [queryClient, stageCommand, tenantId])

  const handleDrop = useCallback(
    async (row: OpportunityRow, target: BoardTarget) => {
      const intent = intentFor(target.kind)
      if (intent !== 'changeStage') return setClosing({ intent, row })
      if (target.stageId === null || row.rowVersion === null) return
      setMovingId(row.id)
      try {
        await stageCommand.run({ id: row.id, expectedVersion: row.rowVersion, targetStageId: target.stageId })
      } finally {
        setMovingId(null)
      }
    },
    [stageCommand],
  )

  const move = useBoardMove(targets, handleDrop)
  const carrying = move.carry?.mode === 'grab' ? move.carry.row : null

  return (
    <div className="flex flex-col gap-3">
      <output aria-live="assertive" className="sr-only">
        {move.announcement}
      </output>

      {stageCommand.problem && <ProblemNotice problem={stageCommand.problem} onReload={() => void reloadList()} />}

      {carrying && (
        // Floats over the page: an inline banner would push the columns down under the pointer mid-move.
        <div className="nx-overlay fixed bottom-6 left-1/2 z-40 flex w-[min(40rem,calc(100vw-2rem))] -translate-x-1/2 flex-wrap items-center gap-3 rounded-xl px-4 py-2.5 text-[12.5px]" data-testid="board-carry-banner">
          <Hand aria-hidden className="text-[var(--nx-tint)] size-4" strokeWidth={1.8} />
          <span className="flex-1">{t('list.board.carrying', { id: carrying.id })}</span>
          <Button type="button" variant="ghost" size="sm" onClick={move.cancel}>
            <X aria-hidden strokeWidth={1.7} />
            {t('list.board.cancelMove')}
          </Button>
        </div>
      )}

      {columns.length === 0 ? (
        <Card className="rounded-[var(--nx-r-panel)] p-0">
          <EmptyState icon={Inbox} title={t('list.grid.noMatchTitle')} description={t('list.grid.noMatchDescription')} />
        </Card>
      ) : (
        <div className={`flex items-stretch gap-3 overflow-x-auto pb-2 transition-opacity ${refreshing ? 'opacity-50' : ''}`} aria-busy={refreshing || undefined}>
          {columns.map((column, index) => (
            <BoardColumn
              key={column.stageId ?? 'none'}
              index={index}
              label={targets[index].label}
              kind={column.kind}
              rows={column.rows}
              move={move}
              readOnly={readOnly}
              movingId={movingId}
              now={now}
              returnTo={returnTo}
            />
          ))}
        </div>
      )}

      {closing?.intent === 'win' && (
        <WinDialog opportunity={{ id: closing.row.id, rowVersion: closing.row.rowVersion ?? 0 }} onClose={() => setClosing(null)} onReload={() => void reloadList()} />
      )}
      {closing?.intent === 'lose' && (
        <LoseDialog opportunity={{ id: closing.row.id, rowVersion: closing.row.rowVersion ?? 0 }} onClose={() => setClosing(null)} onReload={() => void reloadList()} />
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
