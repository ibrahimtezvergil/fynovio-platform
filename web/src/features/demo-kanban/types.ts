import type { StatusTone } from '@/components/common/StatusBadge'
import { i18n } from '@/lib/i18n'

/** How urgent a card is. Drawn as a labelled flag, never as a bare colour. */
export type Priority = 'low' | 'normal' | 'high' | 'urgent'

export const PRIORITY_TONE: Record<Priority, StatusTone> = {
  low: 'gray',
  normal: 'blue',
  high: 'amber',
  urgent: 'red',
}

/**
 * Resolved from the `demo-kanban` catalog at call time. Module-scope fixture
 * data calls this directly; it is evaluated once, not live-reactive to a
 * language switch — the same accepted tradeoff as `stageMeta()`.
 */
export function priorityMeta(): Record<Priority, { label: string; tone: StatusTone }> {
  const priorities = Object.keys(PRIORITY_TONE) as Priority[]
  return priorities.reduce(
    (meta, priority) => {
      meta[priority] = {
        label: i18n.t(`priority.${priority}`, { ns: 'demo-kanban' }),
        tone: PRIORITY_TONE[priority],
      }
      return meta
    },
    {} as Record<Priority, { label: string; tone: StatusTone }>,
  )
}

export interface BoardColumn {
  id: string
  label: string
  tone: StatusTone
  /**
   * Work-in-progress cap. A column over its cap turns negative in the header —
   * the whole point of a board is to make a queue that stopped moving visible.
   */
  wipLimit?: number
}

export interface BoardCard {
  id: string
  columnId: string
  title: string
  /** The account (sales board) or the project (task board) it belongs to. */
  context: string
  owner: string
  /** Pre-formatted: money on the sales board, effort on the task board. */
  metric: string
  /** Forecast or due date, already formatted for display. */
  due?: string
  /** Past its date — the card carries a negative marker with a label. */
  overdue?: boolean
  priority?: Priority
  tags: string[]
  checklist?: { done: number; total: number }
  comments?: number
}

/** Cards keyed by column, in the order they are stacked. */
export type Board = Record<string, BoardCard[]>

/** Where a drag or a keyboard move would land. */
export interface DropTarget {
  columnId: string
  index: number
}

/** Flat fixture list into the per-column model the board actually renders. */
export function groupIntoBoard(columns: readonly BoardColumn[], cards: readonly BoardCard[]): Board {
  const board: Board = {}
  for (const column of columns) board[column.id] = []
  for (const card of cards) board[card.columnId]?.push(card)
  return board
}
