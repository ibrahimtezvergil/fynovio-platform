import { Keyboard, RotateCcw } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { KanbanColumn } from '@/features/demo-kanban/components/KanbanColumn'
import { useBoard } from '@/features/demo-kanban/hooks/useBoard'
import type { BoardCard, BoardColumn } from '@/features/demo-kanban/types'

interface KanbanBoardProps {
  columns: readonly BoardColumn[]
  cards: readonly BoardCard[]
  /** What the metric on each card means — the board says so once, not per card. */
  metricLabel: string
}

/**
 * The board: columns side by side, one horizontal scroller, and a live region
 * that narrates every move.
 *
 * A drag is a purely visual event — the drop indicator means nothing to a
 * screen reader, and neither does a card silently changing parents. The
 * `role="status"` line below is what makes the same move legible without
 * sight, and it is fed by both the pointer path and the keyboard path.
 */
export function KanbanBoard({ columns, cards, metricLabel }: KanbanBoardProps) {
  const { t } = useTranslation('demo-kanban')
  const api = useBoard(columns, cards)

  return (
    <div className="flex min-w-0 flex-col gap-3">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
        <p className="text-muted-foreground inline-flex items-center gap-1.5 text-[11.5px]">
          <Keyboard aria-hidden className="size-3.5" strokeWidth={1.7} />
          {t('board.keyboardHintBefore')} <kbd className="nx-kbd">{t('board.spaceKey')}</kbd>{' '}
          {t('board.keyboardHintMiddle')} <kbd className="nx-kbd">{t('board.enterKey')}</kbd>{' '}
          {t('board.keyboardHintAfter')}
        </p>
        <div className="flex-1" />
        <span className="text-[var(--nx-label-3)] text-[11.5px]">{metricLabel}</span>
        <Button variant="ghost" size="sm" onClick={api.reset}>
          <RotateCcw strokeWidth={1.75} />
          {t('board.reset')}
        </Button>
      </div>

      {/* -mx-* + px-*: the scroller runs edge to edge inside the card, but the
          first and last column still clear the card's padding. */}
      <div className="-mx-6 flex gap-3 overflow-x-auto px-6 pb-1">
        {columns.map((column) => (
          <KanbanColumn key={column.id} column={column} columns={columns} api={api} />
        ))}
      </div>

      <p role="status" aria-live="polite" className="sr-only">
        {api.announcement}
      </p>
    </div>
  )
}
