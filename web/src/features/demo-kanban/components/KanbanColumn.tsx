import { Plus } from 'lucide-react'
import { Fragment } from 'react'
import { useTranslation } from 'react-i18next'
import { StatusBadge } from '@/components/common/StatusBadge'
import { Button } from '@/components/ui/button'
import { KanbanCard } from '@/features/demo-kanban/components/KanbanCard'
import type { BoardApi } from '@/features/demo-kanban/hooks/useBoard'
import type { BoardColumn } from '@/features/demo-kanban/types'
import { cn } from '@/lib/utils'

interface KanbanColumnProps {
  column: BoardColumn
  columns: readonly BoardColumn[]
  api: BoardApi
}

/** A 2px accent rule where the card would land. Decoration only — the live region says it. */
function DropIndicator() {
  return (
    <li aria-hidden className="-my-1 h-[3px] rounded-full bg-[var(--nx-tint)]" />
  )
}

export function KanbanColumn({ column, columns, api }: KanbanColumnProps) {
  const { t } = useTranslation('demo-kanban')
  const cards = api.board[column.id] ?? []
  const overLimit = column.wipLimit != null && cards.length > column.wipLimit
  const isDropColumn = api.dropTarget?.columnId === column.id

  return (
    <section
      aria-label={t('column.ariaLabel', { label: column.label, count: cards.length })}
      className={cn(
        'flex w-[292px] shrink-0 flex-col gap-3 rounded-2xl border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-3 transition-colors',
        isDropColumn && 'border-[var(--nx-tint)] bg-[var(--nx-tint-fill)]',
      )}
    >
      <header className="flex items-center gap-2">
        <StatusBadge label={column.label} tone={column.tone} />
        <span className="text-muted-foreground tnum text-[12px] font-[590]">{cards.length}</span>
        <div className="flex-1" />
        {column.wipLimit != null && (
          <span
            className={cn(
              'tnum rounded-sm px-1.5 py-0.5 text-[10.5px] font-[650]',
              overLimit
                ? 'bg-[var(--nx-st-red-bg)] text-[var(--nx-st-red-fg)]'
                : 'text-[var(--nx-label-3)]',
            )}
          >
            {overLimit && <span className="sr-only">{t('column.limitExceeded')}</span>}
            WIP {cards.length}/{column.wipLimit}
          </span>
        )}
        <Button variant="ghost" size="icon-xs" aria-label={t('column.addCard', { label: column.label })}>
          <Plus strokeWidth={1.8} />
        </Button>
      </header>

      {/* The whole body is the drop zone; the tail append lives here, while
          each card refines the index as the pointer passes over it. */}
      <ul
        onDragOver={(event) => api.onColumnDragOver(event, column.id)}
        onDrop={(event) => api.onDrop(event, column.id)}
        className="flex max-h-[560px] flex-1 flex-col gap-2 overflow-y-auto"
      >
        {cards.map((card, index) => (
          <Fragment key={card.id}>
            {isDropColumn && api.dropTarget?.index === index && <DropIndicator />}
            <KanbanCard
              card={card}
              index={index}
              count={cards.length}
              column={column}
              columns={columns}
              api={api}
            />
          </Fragment>
        ))}

        {isDropColumn && api.dropTarget?.index === cards.length && <DropIndicator />}

        {cards.length === 0 && (
          <li className="text-[var(--nx-label-3)] flex min-h-[76px] items-center justify-center rounded-md border border-dashed border-[var(--nx-hairline-strong)] px-3 text-center text-[11.5px]">
            {t('column.empty')}
          </li>
        )}
      </ul>
    </section>
  )
}
