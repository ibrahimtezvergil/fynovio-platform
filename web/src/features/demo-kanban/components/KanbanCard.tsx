import { CalendarClock, GripVertical, MessageSquare, MoreHorizontal, TriangleAlert } from 'lucide-react'
import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { StatusBadge } from '@/components/common/StatusBadge'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import type { BoardApi } from '@/features/demo-kanban/hooks/useBoard'
import { PRIORITY_TONE, type BoardCard, type BoardColumn } from '@/features/demo-kanban/types'
import { initialsOf } from '@/lib/utils'
import { cn } from '@/lib/utils'

interface KanbanCardProps {
  card: BoardCard
  index: number
  count: number
  column: BoardColumn
  columns: readonly BoardColumn[]
  api: BoardApi
}

/**
 * One card, draggable by pointer and movable by keyboard.
 *
 * The grip is the keyboard's entry point, so it — not the card — is the
 * focusable control: `aria-pressed` says whether the card is currently picked
 * up, and the label reads the position out loud before any key is pressed.
 * The overflow menu repeats the move as a plain list of columns, because a
 * grab-and-arrow interaction is still a lot to ask of a first-time user.
 */
export function KanbanCard({ card, index, count, column, columns, api }: KanbanCardProps) {
  const { t } = useTranslation('demo-kanban')
  const grabbed = api.grabbed === card.id
  const gripRef = useRef<HTMLButtonElement>(null)

  // Moving across columns remounts the card under a new parent, which drops
  // focus mid-interaction. While the card is held, focus follows it.
  useEffect(() => {
    if (grabbed) gripRef.current?.focus()
  }, [grabbed, column.id, index])

  const priority = card.priority
    ? { label: t(`priority.${card.priority}`), tone: PRIORITY_TONE[card.priority] }
    : null

  return (
    <li
      draggable
      data-dragging={api.dragging === card.id ? '' : undefined}
      onDragStart={(event) => api.onDragStart(event, card.id)}
      onDragEnd={api.onDragEnd}
      onDragOver={(event) => api.onCardDragOver(event, column.id, index)}
      className={cn(
        'group/card flex cursor-grab flex-col gap-2 rounded-md border border-[var(--nx-hairline)] bg-[var(--nx-surface)] px-3 py-2.5 shadow-[inset_0_1px_0_var(--nx-specular)] transition-[border-color,box-shadow,opacity] duration-[250ms] ease-fluid',
        'hover:border-[var(--nx-hairline-strong)] active:cursor-grabbing',
        'data-dragging:opacity-40',
        grabbed && 'border-[var(--nx-tint)] shadow-[0_0_0_3px_var(--nx-tint-fill)]',
      )}
    >
      <div className="flex items-start gap-1.5">
        <button
          ref={gripRef}
          type="button"
          aria-pressed={grabbed}
          aria-label={t('card.grabLabel', {
            title: card.title,
            column: column.label,
            index: index + 1,
            count,
          })}
          onKeyDown={(event) => api.onCardKeyDown(event, card.id)}
          className="text-[var(--nx-label-3)] -ml-1 mt-px rounded-sm p-0.5 transition-colors hover:text-foreground aria-pressed:text-[var(--nx-tint)]"
        >
          <GripVertical aria-hidden className="size-4" strokeWidth={1.7} />
        </button>

        <p className="min-w-0 flex-1 text-[13px] leading-[1.35] font-[590] tracking-[-0.012em]">
          {card.title}
        </p>

        <DropdownMenu>
          <DropdownMenuTrigger
            render={
              <Button
                variant="ghost"
                size="icon-xs"
                aria-label={t('card.menuLabel', { title: card.title })}
                className="-mr-1 shrink-0"
              />
            }
          >
            <MoreHorizontal strokeWidth={1.7} />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-52">
            <DropdownMenuLabel>{t('card.moveToColumn')}</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuRadioGroup
              value={column.id}
              onValueChange={(next) => api.move(card.id, { columnId: next, index: 0 })}
            >
              {columns.map((option) => (
                <DropdownMenuRadioItem key={option.id} value={option.id}>
                  {option.label}
                </DropdownMenuRadioItem>
              ))}
            </DropdownMenuRadioGroup>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>

      <p className="text-muted-foreground pl-[22px] text-[11.5px] leading-4">{card.context}</p>

      {card.tags.length > 0 && (
        <ul className="flex flex-wrap gap-1.5 pl-[22px]">
          {card.tags.map((tag) => (
            <li
              key={tag}
              className="text-muted-foreground rounded-sm bg-[var(--nx-fill)] px-1.5 py-0.5 text-[10.5px] font-[550]"
            >
              {tag}
            </li>
          ))}
        </ul>
      )}

      {card.checklist && (
        <div className="flex items-center gap-2 pl-[22px]">
          <div className="nx-meter flex-1">
            <div
              className="nx-meter__bar"
              style={{ width: `${(card.checklist.done / card.checklist.total) * 100}%` }}
            />
          </div>
          <span className="text-muted-foreground tnum text-[10.5px]">
            {card.checklist.done}/{card.checklist.total}
          </span>
        </div>
      )}

      <div className="flex items-center gap-2 border-t border-[var(--nx-hairline-soft)] pt-2 pl-[22px]">
        <span aria-hidden className="nx-avatar size-6">
          {initialsOf(card.owner)}
        </span>
        <span className="text-muted-foreground min-w-0 flex-1 truncate text-[11.5px]">
          {card.owner}
        </span>
        <span className="tnum text-[12px] font-[590]">{card.metric}</span>
      </div>

      <div className="flex flex-wrap items-center gap-x-2.5 gap-y-1 pl-[22px]">
        {priority && <StatusBadge {...priority} size="sm" />}
        {card.due && (
          <span
            className={cn(
              'inline-flex items-center gap-1 text-[11px]',
              card.overdue ? 'font-[590] text-[var(--nx-neg)]' : 'text-muted-foreground',
            )}
          >
            {card.overdue ? (
              <TriangleAlert aria-hidden className="size-3" strokeWidth={2} />
            ) : (
              <CalendarClock aria-hidden className="size-3" strokeWidth={1.8} />
            )}
            {card.overdue && <span className="sr-only">{t('card.overdue')}</span>}
            {card.due}
          </span>
        )}
        {card.comments != null && (
          <span className="text-muted-foreground inline-flex items-center gap-1 text-[11px]">
            <MessageSquare aria-hidden className="size-3" strokeWidth={1.8} />
            <span className="tnum">{card.comments}</span>
            <span className="sr-only">{t('card.comments')}</span>
          </span>
        )}
      </div>
    </li>
  )
}
