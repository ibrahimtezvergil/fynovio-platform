import { useCallback, useEffect, useMemo, useState, type DragEvent, type KeyboardEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useFetchAvailableActions } from '../api'
import type { AvailableActions } from '../schema'
import { classifyTarget, nextAllowedIndex, type DropVerdict, type MoveTarget } from './boardMove'
import type { OpportunityRow } from './rows'

export interface BoardTarget extends MoveTarget {
  label: string
}

interface Carry {
  row: OpportunityRow
  /** `pointer` = native drag; `grab` = picked up with the grip (keyboard or tap), dropped on a column. */
  mode: 'pointer' | 'grab'
  /** `undefined` until the server's projection arrives; every target stays `unknown` (closed) meanwhile. */
  actions: AvailableActions | undefined
}

const MIME = 'application/x-fynovio-opportunity'

/**
 * Picking a card up and choosing where it lands — three ways in (native drag, keyboard, tap), one way out (`onDrop`).
 * The hook never sends a command: it decides where a drop may land, from the server's actions projection,
 * fetched once when the card is picked up.
 */
export function useBoardMove(columns: readonly BoardTarget[], onDrop: (row: OpportunityRow, target: BoardTarget) => void) {
  const { t } = useTranslation('opportunities')
  const fetchActions = useFetchAvailableActions()
  const [carry, setCarry] = useState<Carry | null>(null)
  const [hoverIndex, setHoverIndex] = useState<number | null>(null)
  const [announcement, setAnnouncement] = useState('')

  const verdicts = useMemo<readonly DropVerdict[]>(
    () => (carry ? columns.map((column) => classifyTarget(column, carry.row.stageId, carry.actions)) : []),
    [carry, columns],
  )
  const originIndex = carry ? columns.findIndex((column) => column.stageId === carry.row.stageId) : -1

  const release = useCallback(() => {
    setCarry(null)
    setHoverIndex(null)
  }, [])

  const cancel = useCallback(() => {
    if (!carry) return
    setAnnouncement(t('list.board.a11y.cancelled', { id: carry.row.id }))
    release()
  }, [carry, release, t])

  const pickUp = useCallback(
    (row: OpportunityRow, mode: Carry['mode']) => {
      setCarry({ row, mode, actions: undefined })
      setHoverIndex(null)
      if (mode === 'grab') setAnnouncement(t('list.board.a11y.grabbed', { id: row.id }))
      fetchActions(row.id).then(
        (actions) => setCarry((current) => (current?.row.id === row.id ? { ...current, actions } : current)),
        () => setAnnouncement(t('list.board.a11y.actionsFailed')),
      )
    },
    [fetchActions, t],
  )

  const drop = useCallback(
    (index: number) => {
      if (!carry || verdicts[index] !== 'allowed') return
      const target = columns[index]
      release()
      setAnnouncement(t('list.board.a11y.dropped', { id: carry.row.id, stage: target.label }))
      onDrop(carry.row, target)
    },
    [carry, columns, onDrop, release, t, verdicts],
  )

  // Escape puts a carried card back, wherever focus is (the grip, a column button, or nowhere).
  useEffect(() => {
    if (!carry) return
    const onKeyDown = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape') cancel()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [carry, cancel])

  /* ---- native drag ----------------------------------------------------- */

  const onDragStart = (event: DragEvent, row: OpportunityRow) => {
    event.dataTransfer.effectAllowed = 'move'
    event.dataTransfer.setData(MIME, String(row.id)) // some browsers refuse to start a drag with an empty payload
    pickUp(row, 'pointer')
  }

  const onColumnDragOver = (event: DragEvent, index: number) => {
    if (!carry || verdicts[index] !== 'allowed') return // no preventDefault: the browser shows "not allowed"
    event.preventDefault()
    event.dataTransfer.dropEffect = 'move'
    setHoverIndex(index)
  }

  const onColumnDragLeave = (event: DragEvent, index: number) => {
    if (event.currentTarget.contains(event.relatedTarget as Node | null)) return
    setHoverIndex((current) => (current === index ? null : current))
  }

  const onColumnDrop = (event: DragEvent, index: number) => {
    event.preventDefault()
    drop(index)
  }

  /* ---- grip (keyboard / tap) ------------------------------------------- */

  const onGripClick = (row: OpportunityRow) => {
    if (carry?.row.id !== row.id) return pickUp(row, 'grab')
    if (hoverIndex !== null && hoverIndex !== originIndex) return drop(hoverIndex)
    cancel()
  }

  const onGripKeyDown = (event: KeyboardEvent, row: OpportunityRow) => {
    if (carry?.row.id !== row.id || carry.mode !== 'grab') return
    const direction = event.key === 'ArrowRight' ? 1 : event.key === 'ArrowLeft' ? -1 : null
    if (direction === null) return
    event.preventDefault()
    const next = nextAllowedIndex(verdicts, hoverIndex ?? originIndex, direction)
    if (next === null) return setAnnouncement(carry.actions ? t('list.board.a11y.noFurther') : t('list.board.a11y.loadingActions'))
    setHoverIndex(next)
    setAnnouncement(t('list.board.a11y.target', { stage: columns[next].label }))
  }

  return {
    carry,
    verdicts,
    hoverIndex,
    announcement,
    drop,
    cancel,
    onDragStart,
    onDragEnd: release,
    onColumnDragOver,
    onColumnDragLeave,
    onColumnDrop,
    onGripClick,
    onGripKeyDown,
  }
}

export type BoardMove = ReturnType<typeof useBoardMove>
