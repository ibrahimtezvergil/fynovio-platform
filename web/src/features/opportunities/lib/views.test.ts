import { describe, expect, it } from 'vitest'
import type { SharedView } from '@/lib/shared-views/schema'
import { applySharedView, columnIdOf } from './views'

const column = (id: string) => ({ id })
const idOf = (c: { id: string }) => c.id
const all = ['select', 'id', 'createdAt', 'party', 'status', 'amount', 'cf:region', 'cf:tier', 'rowActions'].map(column)

const view = (columns: SharedView['columns']): SharedView => ({ id: 1, key: 'review', name: 'Review', kind: 'table', columns, status: 'Active', sortOrder: 0, rowVersion: 1 })

describe('applying a shared table view', () => {
  it('shows every column when there is no view', () => {
    expect(applySharedView(all, null, idOf)).toEqual(all)
    expect(applySharedView(all, undefined, idOf)).toEqual(all)
  })

  it('shows exactly the view\'s columns in the view\'s order, between the table\'s own selection and action columns', () => {
    const shown = applySharedView(all, view([{ kind: 'field', key: 'region' }, { kind: 'builtin', key: 'party' }, { kind: 'builtin', key: 'id' }]), idOf)

    expect(shown.map(idOf)).toEqual(['select', 'cf:region', 'party', 'id', 'rowActions'])
  })

  it('skips a column the table no longer has — a field deprecated since the view was saved — and keeps the rest working', () => {
    const shown = applySharedView(all, view([{ kind: 'builtin', key: 'id' }, { kind: 'field', key: 'retired' }, { kind: 'builtin', key: 'status' }]), idOf)

    expect(shown.map(idOf)).toEqual(['select', 'id', 'status', 'rowActions'])
  })

  it('names a field column the way the table does', () => {
    expect(columnIdOf({ kind: 'field', key: 'region' })).toBe('cf:region')
    expect(columnIdOf({ kind: 'builtin', key: 'region' })).toBe('region')
  })

  it('never invents a column: a view of unknown columns leaves only the table\'s own', () => {
    expect(applySharedView(all, view([{ kind: 'builtin', key: 'serviceDuration' }]), idOf).map(idOf)).toEqual(['select', 'rowActions'])
  })
})
