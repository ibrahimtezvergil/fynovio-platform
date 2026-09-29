import { CalendarClock, GripVertical, Loader2 } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { paths } from '@/routes/paths'
import { cn } from '@/lib/utils'
import { formatDate, formatMoney } from '../lib/format'
import { isMovable } from '../lib/boardMove'
import type { OpportunityRow } from '../lib/rows'
import type { BoardMove } from '../lib/useBoardMove'
import { OpportunityStatusBadge } from './OpportunityStatusBadge'

interface BoardCardProps {
  row: OpportunityRow
  move: BoardMove
  /** Archived cards are read-only, whatever the server would say about them. */
  readOnly: boolean
  /** A move for this card is in flight (server-confirmed — the card stays put until the list refreshes). */
  moving: boolean
  /** Epoch ms the board was rendered at, so "overdue" is decided once per render, not per card. */
  now: number
  returnTo: string
}

const initials = (name: string) =>
  name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0].toLocaleUpperCase('tr'))
    .join('')

export function BoardCard({ row, move, readOnly, moving, now, returnTo }: BoardCardProps) {
  const { t } = useTranslation('opportunities')
  const movable = !readOnly && isMovable(row)
  const carried = move.carry?.row.id === row.id
  const expiry = formatDate(row.expiryDate)
  const overdue = row.status === 'Open' && row.expiryDate != null && new Date(row.expiryDate).getTime() < now
  const money = formatMoney(row.amount, row.currency)

  return (
    <article
      data-testid="opportunity-card"
      data-carried={carried || undefined}
      draggable={movable && !moving}
      onDragStart={movable ? (event) => move.onDragStart(event, row) : undefined}
      onDragEnd={movable ? move.onDragEnd : undefined}
      aria-busy={moving || undefined}
      className={cn(
        'group/board-card relative flex gap-1 rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-1 transition-[opacity,box-shadow,background-color]',
        'focus-within:bg-[var(--nx-fill-hover)] hover:bg-[var(--nx-fill-hover)]',
        movable && !moving && 'cursor-grab active:cursor-grabbing',
        (moving || carried) && 'opacity-60',
        carried && 'ring-2 ring-[var(--nx-tint)]',
      )}
    >
      {movable ? (
        <button
          type="button"
          aria-pressed={carried && move.carry?.mode === 'grab'}
          aria-label={t('list.board.grabLabel', { id: row.id })}
          disabled={moving}
          onClick={() => move.onGripClick(row)}
          onKeyDown={(event) => move.onGripKeyDown(event, row)}
          className="text-[var(--nx-label-3)] hover:text-foreground aria-pressed:text-[var(--nx-tint)] mt-1.5 flex h-6 w-5 shrink-0 cursor-pointer items-center justify-center rounded-sm opacity-40 transition-opacity group-focus-within/board-card:opacity-100 group-hover/board-card:opacity-100 focus-visible:opacity-100 aria-pressed:opacity-100 [@media(hover:none)]:opacity-100"
        >
          <GripVertical aria-hidden className="size-4" strokeWidth={1.7} />
        </button>
      ) : (
        <span aria-hidden className="w-2 shrink-0" />
      )}
      <Link
        to={paths.crmOpportunity(row.id, returnTo)}
        draggable={false}
        className="flex min-w-0 flex-1 flex-col gap-1.5 rounded-md p-2 pl-1"
      >
        <span className="flex items-center justify-between gap-2">
          <span className="text-primary tabular-nums text-[12.5px] font-[590]">#{row.id}</span>
          {moving ? (
            <Loader2 aria-label={t('list.board.moving')} className="text-muted-foreground size-3.5 animate-spin" />
          ) : (
            row.status !== 'Open' && <OpportunityStatusBadge status={row.status} size="sm" />
          )}
        </span>
        <span className="truncate text-[13px] font-[590]">{row.party ?? (row.partyId == null ? '—' : t('list.partyId', { id: row.partyId }))}</span>
        <span className="tnum text-[13px] font-[550]">{money ?? '—'}</span>
        {(row.owner || expiry) && (
          <span className="mt-0.5 flex items-center justify-between gap-2 border-t border-[var(--nx-hairline)] pt-2">
            {row.owner ? (
              <span className="flex min-w-0 items-center gap-1.5">
                <span aria-hidden className="nx-avatar size-5 text-[9.5px]">
                  {initials(row.owner)}
                </span>
                <span className="text-muted-foreground truncate text-[11.5px]">{row.owner}</span>
              </span>
            ) : (
              <span />
            )}
            {expiry && (
              <span
                title={t(overdue ? 'list.board.expiryOverdue' : 'list.board.expiry')}
                className={cn('tnum flex shrink-0 items-center gap-1 text-[11.5px]', overdue ? 'text-[var(--nx-st-red-fg)] font-[590]' : 'text-muted-foreground')}
              >
                <CalendarClock aria-hidden className="size-3" strokeWidth={1.8} />
                {expiry}
              </span>
            )}
          </span>
        )}
      </Link>
    </article>
  )
}
