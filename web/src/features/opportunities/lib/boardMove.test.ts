import { describe, expect, it } from 'vitest'
import type { AvailableActions } from '../schema'
import { classifyTarget, intentFor, isMovable, nextAllowedIndex } from './boardMove'

const actions = (overrides: Partial<AvailableActions> = {}): AvailableActions => ({
  canOpen: false,
  canChangeStage: true,
  allowedTargetStageIds: [31],
  canWin: true,
  canLose: false,
  canReassign: false,
  ...overrides,
})

describe('classifyTarget', () => {
  it('is unknown everywhere except the origin until the server projection arrives', () => {
    expect(classifyTarget({ stageId: 31, kind: 'Open' }, 30, undefined)).toBe('unknown')
    expect(classifyTarget({ stageId: 30, kind: 'Open' }, 30, undefined)).toBe('origin')
  })

  it('allows an open stage only when the server lists it as a target', () => {
    expect(classifyTarget({ stageId: 31, kind: 'Open' }, 30, actions())).toBe('allowed')
    expect(classifyTarget({ stageId: 32, kind: 'Open' }, 30, actions())).toBe('denied')
    expect(classifyTarget({ stageId: 31, kind: 'Open' }, 30, actions({ canChangeStage: false }))).toBe('denied')
  })

  it('gates Won and Lost on canWin / canLose, never on the stage list', () => {
    expect(classifyTarget({ stageId: 34, kind: 'Won' }, 30, actions({ allowedTargetStageIds: [] }))).toBe('allowed')
    expect(classifyTarget({ stageId: 35, kind: 'Lost' }, 30, actions({ allowedTargetStageIds: [35] }))).toBe('denied')
  })

  it('never lets a card land in the no-stage column', () => {
    expect(classifyTarget({ stageId: null, kind: 'Open' }, 30, actions())).toBe('denied')
  })
})

describe('intentFor', () => {
  it('routes Won and Lost to their own commands', () => {
    expect([intentFor('Open'), intentFor('Won'), intentFor('Lost')]).toEqual(['changeStage', 'win', 'lose'])
  })
})

describe('nextAllowedIndex', () => {
  const verdicts = ['origin', 'denied', 'allowed', 'unknown', 'allowed'] as const
  it('skips columns the card cannot enter, in both directions, and stops at the edge', () => {
    expect(nextAllowedIndex(verdicts, 0, 1)).toBe(2)
    expect(nextAllowedIndex(verdicts, 2, 1)).toBe(4)
    expect(nextAllowedIndex(verdicts, 4, 1)).toBeNull()
    expect(nextAllowedIndex(verdicts, 4, -1)).toBe(2)
    expect(nextAllowedIndex(verdicts, 2, -1)).toBeNull()
  })
})

describe('isMovable', () => {
  it('moves only open, versioned, staged cards', () => {
    expect(isMovable({ status: 'Open', rowVersion: 3, stageId: 30 })).toBe(true)
    expect(isMovable({ status: 'Draft', rowVersion: 3, stageId: null })).toBe(false)
    expect(isMovable({ status: 'Won', rowVersion: 3, stageId: 34 })).toBe(false)
    expect(isMovable({ status: 'Open', rowVersion: null, stageId: 30 })).toBe(false)
  })
})
