import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { usePersonalViews } from '@/features/demo-filters/lib/personalViews'
import { EMPTY_FILTER } from '@/features/demo-filters/types'

afterEach(() => {
  localStorage.clear()
})

describe('usePersonalViews', () => {
  it('starts empty and saves a new view, returning it for immediate selection', () => {
    const { result } = renderHook(() => usePersonalViews())
    expect(result.current.views).toEqual([])

    let saved: ReturnType<typeof result.current.save> | undefined
    act(() => {
      saved = result.current.save('My view', { ...EMPTY_FILTER, onlyTagged: true }, [
        { field: 'amount', direction: 'desc' },
      ])
    })

    expect(result.current.views).toHaveLength(1)
    expect(result.current.views[0]).toMatchObject({
      label: 'My view',
      filter: { onlyTagged: true },
      sort: [{ field: 'amount', direction: 'desc' }],
      shared: false,
    })
    expect(saved).toEqual(result.current.views[0])
  })

  it('persists across remounts', () => {
    const first = renderHook(() => usePersonalViews())
    act(() => {
      first.result.current.save('Kept view', EMPTY_FILTER, [])
    })
    first.unmount()

    const second = renderHook(() => usePersonalViews())
    expect(second.result.current.views).toHaveLength(1)
    expect(second.result.current.views[0]?.label).toBe('Kept view')
  })

  it('removes a view by id', () => {
    const { result } = renderHook(() => usePersonalViews())
    act(() => {
      result.current.save('To remove', EMPTY_FILTER, [])
    })
    const id = result.current.views[0]!.id

    act(() => {
      result.current.remove(id)
    })
    expect(result.current.views).toEqual([])
  })
})
