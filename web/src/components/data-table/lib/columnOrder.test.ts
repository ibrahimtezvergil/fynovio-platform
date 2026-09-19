import { describe, expect, it } from 'vitest'
import { moveColumnOrder, normalizeColumnOrder } from '@/components/data-table/lib/columnOrder'

describe('normalizeColumnOrder', () => {
  it('appends ids missing from a partial order', () => {
    expect(normalizeColumnOrder(['b'], ['a', 'b', 'c'])).toEqual(['b', 'a', 'c'])
  })

  it('drops ids that no longer exist', () => {
    expect(normalizeColumnOrder(['x', 'b', 'a'], ['a', 'b'])).toEqual(['b', 'a'])
  })

  it('falls back to definition order when empty', () => {
    expect(normalizeColumnOrder([], ['a', 'b', 'c'])).toEqual(['a', 'b', 'c'])
  })
})

describe('moveColumnOrder', () => {
  const allIds = ['select', 'name', 'email', 'department']
  const movableIds = ['name', 'email', 'department']

  it('swaps with the previous movable neighbour', () => {
    expect(moveColumnOrder(allIds, allIds, movableIds, 'email', -1)).toEqual([
      'select',
      'email',
      'name',
      'department',
    ])
  })

  it('swaps with the next movable neighbour', () => {
    expect(moveColumnOrder(allIds, allIds, movableIds, 'name', 1)).toEqual([
      'select',
      'email',
      'name',
      'department',
    ])
  })

  it('never swaps into a structural column', () => {
    expect(moveColumnOrder(allIds, allIds, movableIds, 'name', -1)).toEqual(allIds)
  })

  it('no-ops at the last movable position', () => {
    expect(moveColumnOrder(allIds, allIds, movableIds, 'department', 1)).toEqual(allIds)
  })

  it('returns the normalized order unchanged for an unknown column id', () => {
    expect(moveColumnOrder(allIds, allIds, movableIds, 'missing', 1)).toEqual(allIds)
  })
})
