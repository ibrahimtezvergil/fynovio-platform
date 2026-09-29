import { CheckCircle2, ArrowDownToLine, XCircle } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import type { DropVerdict } from '../lib/boardMove'
import type { PipelineStageKind } from '../schema'
import type { OpportunityRow } from '../lib/rows'
import { totalAmountLabel } from '../lib/rows'
import type { BoardMove } from '../lib/useBoardMove'
import { BoardCard } from './BoardCard'

interface BoardColumnProps {
  index: number
  label: string
  kind: PipelineStageKind
  rows: readonly OpportunityRow[]
  move: BoardMove
  readOnly: boolean
  movingId: number | null
  now: number
  returnTo: string
}

/** The stage's kind is drawn twice — an icon and the accent — so no state reads by colour alone. */
const KIND_ACCENT: Record<PipelineStageKind, string> = {
  Open: 'var(--nx-tint)',
  Won: 'var(--nx-st-green-fg)',
  Lost: 'var(--nx-st-red-fg)',
}

export function BoardColumn({ index, label, kind, rows, move, readOnly, movingId, now, returnTo }: BoardColumnProps) {
  const { t } = useTranslation('opportunities')
  const verdict: DropVerdict | null = move.carry ? (move.verdicts[index] ?? null) : null
  const hovered = move.hoverIndex === index
  const total = totalAmountLabel(rows)
  const carryingByGrip = move.carry?.mode === 'grab'
  const Icon = kind === 'Won' ? CheckCircle2 : kind === 'Lost' ? XCircle : null

  return (
    <section
      aria-label={t('list.board.columnLabel', { stage: label, count: rows.length })}
      data-verdict={verdict ?? undefined}
      onDragOver={(event) => move.onColumnDragOver(event, index)}
      onDragLeave={(event) => move.onColumnDragLeave(event, index)}
      onDrop={(event) => move.onColumnDrop(event, index)}
      className={cn(
        'nx-card flex min-h-[26rem] min-w-64 flex-1 basis-64 flex-col gap-3 rounded-[var(--nx-r-panel)] border-t-[3px] p-3 transition-[opacity,box-shadow]',
        verdict === 'allowed' && 'ring-1 ring-[var(--nx-tint)]/50',
        hovered && 'ring-2 ring-[var(--nx-tint)] bg-[var(--nx-tint-fill)]',
        (verdict === 'denied' || verdict === 'unknown') && 'opacity-55',
      )}
      style={{ borderTopColor: KIND_ACCENT[kind] }}
    >
      <header className="flex flex-col gap-0.5 px-1">
        <div className="flex items-center justify-between gap-2">
          <h3 className="flex min-w-0 items-center gap-1.5 text-[13px] font-[600]">
            {Icon && <Icon aria-hidden className="size-4 shrink-0" style={{ color: KIND_ACCENT[kind] }} strokeWidth={1.9} />}
            <span className="truncate">{label}</span>
          </h3>
          <span className="tnum bg-[var(--nx-fill)] text-muted-foreground rounded-full px-2 py-0.5 text-[11.5px] font-[590]">{rows.length}</span>
        </div>
        <span className="tnum text-muted-foreground min-h-4 text-[11.5px]">{total ?? ''}</span>
      </header>

      {carryingByGrip && verdict === 'allowed' && (
        <Button type="button" variant="outline" size="sm" onClick={() => move.drop(index)} className="w-full">
          <ArrowDownToLine aria-hidden strokeWidth={1.7} />
          {t('list.board.moveHere')}
        </Button>
      )}
      {verdict === 'denied' && <p className="text-muted-foreground px-1 text-[11.5px]">{t('list.board.columnDenied')}</p>}
      {verdict === 'unknown' && <p className="text-muted-foreground px-1 text-[11.5px]">{t('list.board.checking')}</p>}

      <div className="flex flex-1 flex-col gap-2">
        {rows.map((row) => (
          <BoardCard key={row.id} row={row} move={move} readOnly={readOnly} moving={movingId === row.id} now={now} returnTo={returnTo} />
        ))}
        {rows.length === 0 && (
          <div className="text-muted-foreground flex flex-1 items-center justify-center rounded-lg border border-dashed border-[var(--nx-hairline)] p-4 text-center text-[12px]">
            {verdict === 'allowed' ? t('list.board.dropHere') : t('list.board.emptyColumn')}
          </div>
        )}
      </div>
    </section>
  )
}
