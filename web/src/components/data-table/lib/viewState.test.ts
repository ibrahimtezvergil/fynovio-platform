import { renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { useSessionStore } from '@/lib/auth'
import {
  readPersistedViewState,
  useViewStateUserId,
  viewStateStorageKey,
  writePersistedViewState,
} from '@/components/data-table/lib/viewState'

describe('viewStateStorageKey', () => {
  it('namespaces by user and table', () => {
    expect(viewStateStorageKey('demo-tables.saas-grid', 'u-1')).toBe(
      'fynovio.viewState.u-1.demo-tables.saas-grid',
    )
  })

  it('falls back to "anon" with no user', () => {
    expect(viewStateStorageKey('demo-tables.saas-grid', null)).toBe(
      'fynovio.viewState.anon.demo-tables.saas-grid',
    )
  })

  it('appends an optional suffix', () => {
    expect(viewStateStorageKey('demo-filters.orders', 'u-1', 'personalViews')).toBe(
      'fynovio.viewState.u-1.demo-filters.orders.personalViews',
    )
  })
})

describe('readPersistedViewState / writePersistedViewState', () => {
  afterEach(() => localStorage.clear())

  it('round-trips a JSON value', () => {
    writePersistedViewState('test-key', { columnOrder: ['a', 'b'] })
    expect(readPersistedViewState('test-key')).toEqual({ columnOrder: ['a', 'b'] })
  })

  it('returns null for a key that was never written', () => {
    expect(readPersistedViewState('missing-key')).toBeNull()
  })

  it('returns null instead of throwing on corrupt JSON', () => {
    localStorage.setItem('bad-key', '{not json')
    expect(readPersistedViewState('bad-key')).toBeNull()
  })
})

describe('useViewStateUserId', () => {
  afterEach(() => {
    useSessionStore.getState().endSession('unauthenticated')
  })

  it('is null with no signed-in user', () => {
    const { result } = renderHook(() => useViewStateUserId())
    expect(result.current).toBeNull()
  })

  it('reflects the signed-in user id', () => {
    useSessionStore.getState().applyAuthResult({
      status: 'authenticated',
      accessToken: 't',
      expiresIn: 600,
      account: { id: 42, email: 'a@example.com', displayName: 'A', locale: null },
      activeTenant: { tenantId: 1 },
      memberships: [{ tenantId: 1, displayName: 'Acme Türkiye' }],
    })
    const { result } = renderHook(() => useViewStateUserId())
    expect(result.current).toBe('42')
  })
})
