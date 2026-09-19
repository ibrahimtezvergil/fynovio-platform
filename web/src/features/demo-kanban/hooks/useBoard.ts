import { useCallback, useMemo, useState, type DragEvent, type KeyboardEvent } from 'react'
import { useTranslation } from 'react-i18next'
import {
  groupIntoBoard,
  type Board,
  type BoardCard,
  type BoardColumn,
  type DropTarget,
} from '@/features/demo-kanban/types'

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max)
}

function locate(board: Board, cardId: string): DropTarget | null {
  for (const columnId of Object.keys(board)) {
    const index = board[columnId]!.findIndex((card) => card.id === cardId)
    if (index !== -1) return { columnId, index }
  }
  return null
}

/**
 * Pure move. The one subtlety is a same-column move downwards: the index came
 * from the list *before* the card was pulled out of it, so every position past
 * the card's own has shifted up by one.
 */
function withMove(board: Board, cardId: string, to: DropTarget): Board {
  const from = locate(board, cardId)
  if (!from) return board

  const card = board[from.columnId]![from.index]!
  const source = board[from.columnId]!.filter((entry) => entry.id !== cardId)
  const next: Board = { ...board, [from.columnId]: source }

  const target = from.columnId === to.columnId ? source : [...(board[to.columnId] ?? [])]
  const shifted = from.columnId === to.columnId && from.index < to.index ? to.index - 1 : to.index
  const index = clamp(shifted, 0, target.length)

  next[to.columnId] = [
    ...target.slice(0, index),
    { ...card, columnId: to.columnId },
    ...target.slice(index),
  ]
  return next
}

const MIME = 'application/x-fynovio-card'

/**
 * Board state plus both ways to move a card.
 *
 * Pointer dragging is the HTML5 drag-and-drop API — no dependency, and it
 * gives the platform's own drag image for free. It is also completely
 * unreachable from a keyboard, which is why every card also carries a grab
 * mode: space picks the card up, the arrows move it, enter drops it, escape
 * puts it back. Both paths end in the same `withMove`, so the two can't
 * disagree about what a move means.
 */
export function useBoard(columns: readonly BoardColumn[], cards: readonly BoardCard[]) {
  const { t } = useTranslation('demo-kanban')
  const [board, setBoard] = useState<Board>(() => groupIntoBoard(columns, cards))
  const [dragging, setDragging] = useState<string | null>(null)
  const [dropTarget, setDropTarget] = useState<DropTarget | null>(null)
  const [grabbed, setGrabbed] = useState<string | null>(null)
  const [origin, setOrigin] = useState<DropTarget | null>(null)
  const [announcement, setAnnouncement] = useState('')

  const labelOf = useCallback(
    (columnId: string) => columns.find((column) => column.id === columnId)?.label ?? columnId,
    [columns],
  )

  const announcePosition = useCallback(
    (prefix: string, cardId: string, next: Board) => {
      const at = locate(next, cardId)
      if (!at) return
      const size = next[at.columnId]!.length
      setAnnouncement(
        t('announce.position', { prefix, column: labelOf(at.columnId), position: at.index + 1, total: size }),
      )
    },
    [labelOf, t],
  )

  const move = useCallback(
    (cardId: string, to: DropTarget, announce = t('announce.moved')) => {
      setBoard((current) => {
        const next = withMove(current, cardId, to)
        announcePosition(announce, cardId, next)
        return next
      })
    },
    [announcePosition, t],
  )

  const reset = useCallback(() => {
    setBoard(groupIntoBoard(columns, cards))
    setAnnouncement(t('announce.reset'))
  }, [columns, cards, t])

  /* ---- pointer drag ---------------------------------------------------- */

  const onDragStart = useCallback((event: DragEvent, cardId: string) => {
    event.dataTransfer.effectAllowed = 'move'
    // Some browsers refuse to start a drag with an empty payload.
    event.dataTransfer.setData(MIME, cardId)
    setDragging(cardId)
  }, [])

  const onDragEnd = useCallback(() => {
    setDragging(null)
    setDropTarget(null)
  }, [])

  /** Over a card: the pointer's half decides whether it lands before or after. */
  const onCardDragOver = useCallback(
    (event: DragEvent, columnId: string, index: number) => {
      if (!dragging) return
      event.preventDefault()
      event.stopPropagation()
      const rect = event.currentTarget.getBoundingClientRect()
      const after = event.clientY > rect.top + rect.height / 2
      setDropTarget({ columnId, index: after ? index + 1 : index })
    },
    [dragging],
  )

  /** Over the column's empty tail: append. */
  const onColumnDragOver = useCallback(
    (event: DragEvent, columnId: string) => {
      if (!dragging) return
      event.preventDefault()
      setDropTarget({ columnId, index: board[columnId]?.length ?? 0 })
    },
    [dragging, board],
  )

  const onDrop = useCallback(
    (event: DragEvent, columnId: string) => {
      event.preventDefault()
      const cardId = dragging ?? event.dataTransfer.getData(MIME)
      if (!cardId) return
      const target = dropTarget ?? { columnId, index: board[columnId]?.length ?? 0 }
      move(cardId, target)
      setDragging(null)
      setDropTarget(null)
    },
    [dragging, dropTarget, board, move],
  )

  /* ---- keyboard grab --------------------------------------------------- */

  const release = useCallback(() => {
    setGrabbed(null)
    setOrigin(null)
  }, [])

  const onCardKeyDown = useCallback(
    (event: KeyboardEvent, cardId: string) => {
      const at = locate(board, cardId)
      if (!at) return

      if (event.key === ' ' || event.key === 'Enter') {
        event.preventDefault()
        if (grabbed === cardId) {
          announcePosition(t('announce.dropped'), cardId, board)
          release()
        } else {
          setGrabbed(cardId)
          setOrigin(at)
          setAnnouncement(
            t('announce.grabbed', {
              column: labelOf(at.columnId),
              position: at.index + 1,
              total: board[at.columnId]!.length,
            }),
          )
        }
        return
      }

      if (grabbed !== cardId) return

      if (event.key === 'Escape') {
        event.preventDefault()
        if (origin) move(cardId, origin, t('announce.cancelled'))
        release()
        return
      }

      const columnIndex = columns.findIndex((column) => column.id === at.columnId)
      switch (event.key) {
        case 'ArrowUp':
          event.preventDefault()
          move(cardId, { columnId: at.columnId, index: at.index - 1 })
          break
        case 'ArrowDown':
          event.preventDefault()
          // +2, not +1: `withMove` subtracts one for a downward same-column
          // move, because the index it is given predates the card's removal.
          move(cardId, { columnId: at.columnId, index: at.index + 2 })
          break
        case 'ArrowLeft': {
          event.preventDefault()
          const previous = columns[columnIndex - 1]
          if (previous) move(cardId, { columnId: previous.id, index: at.index })
          break
        }
        case 'ArrowRight': {
          event.preventDefault()
          const next = columns[columnIndex + 1]
          if (next) move(cardId, { columnId: next.id, index: at.index })
          break
        }
        default:
          break
      }
    },
    [board, columns, grabbed, origin, labelOf, move, release, announcePosition, t],
  )

  const totals = useMemo(
    () =>
      Object.fromEntries(
        columns.map((column) => [column.id, board[column.id]?.length ?? 0]),
      ) as Record<string, number>,
    [board, columns],
  )

  return {
    board,
    totals,
    dragging,
    dropTarget,
    grabbed,
    announcement,
    move,
    reset,
    onDragStart,
    onDragEnd,
    onCardDragOver,
    onColumnDragOver,
    onDrop,
    onCardKeyDown,
  }
}

export type BoardApi = ReturnType<typeof useBoard>
