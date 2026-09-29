import type { AvailableActions, PipelineStageKind } from '../schema'

/** What a board column means for the card being moved. */
export type DropVerdict = 'origin' | 'unknown' | 'allowed' | 'denied'

/** The command a drop dispatches. Won and Lost never go through `changeStage` — the backend rejects it (architecture doc §4.2). */
export type MoveIntent = 'changeStage' | 'win' | 'lose'

export interface MoveTarget {
  stageId: number | null
  kind: PipelineStageKind
}

export function intentFor(kind: PipelineStageKind): MoveIntent {
  if (kind === 'Won') return 'win'
  if (kind === 'Lost') return 'lose'
  return 'changeStage'
}

/**
 * Whether the picked-up card may land in `target`. Only the server's actions projection decides: until it has arrived
 * every column is `unknown` (fail closed), and nothing here re-derives a rule.
 */
export function classifyTarget(target: MoveTarget, fromStageId: number | null, actions: AvailableActions | undefined): DropVerdict {
  if (target.stageId !== null && target.stageId === fromStageId) return 'origin'
  if (actions === undefined) return 'unknown'
  if (target.stageId === null) return 'denied'
  switch (intentFor(target.kind)) {
    case 'win':
      return actions.canWin ? 'allowed' : 'denied'
    case 'lose':
      return actions.canLose ? 'allowed' : 'denied'
    case 'changeStage':
      return actions.canChangeStage && actions.allowedTargetStageIds.includes(target.stageId) ? 'allowed' : 'denied'
  }
}

/** The next column in `direction` the card may land in, skipping columns it cannot; `null` at the edge. */
export function nextAllowedIndex(verdicts: readonly DropVerdict[], fromIndex: number, direction: 1 | -1): number | null {
  for (let index = fromIndex + direction; index >= 0 && index < verdicts.length; index += direction) {
    if (verdicts[index] === 'allowed') return index
  }
  return null
}

/** Only an open, versioned card can be moved: a draft needs the open command, a closed one is terminal. */
export function isMovable(row: { status: string; rowVersion: number | null; stageId: number | null }): boolean {
  return row.status === 'Open' && row.rowVersion !== null && row.stageId !== null
}
