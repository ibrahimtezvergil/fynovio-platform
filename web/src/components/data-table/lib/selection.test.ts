import { describe, expect, it } from 'vitest'
import { emptySelection, isRowSelected, resolveSelectionCount, toggleSelectAllResults } from './selection'

describe('selection exclusion model', () => {
  it('represents every result without enumerating every result ID', () => {
    const selection = toggleSelectAllResults(emptySelection())
    expect(selection.mode).toBe('exclude')
    expect(resolveSelectionCount(selection, 37_412)).toBe(37_412)
    expect(isRowSelected(selection, 'deal-1')).toBe(true)
  })

  it('subtracts exclusions from the result count', () => {
    const selection = { mode: 'exclude' as const, ids: new Set(['deal-1', 'deal-2', 'deal-3']) }
    expect(resolveSelectionCount(selection, 37_412)).toBe(37_409)
    expect(isRowSelected(selection, 'deal-2')).toBe(false)
  })
})
