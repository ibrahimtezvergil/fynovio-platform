import { describe, expect, it } from 'vitest'
import {
  DEFAULT_PARAM_KEYS,
  parseGlobalFilter,
  parsePagination,
  parseSorting,
  prefixParamKeys,
  serializeSorting,
  setOrDelete,
} from '@/components/data-table/lib/urlState'

describe('parsePagination', () => {
  it('defaults to page 1 / the given page size when the URL has nothing', () => {
    const params = new URLSearchParams()
    expect(parsePagination(params, DEFAULT_PARAM_KEYS, 25)).toEqual({ pageIndex: 0, pageSize: 25 })
  })

  it('converts the 1-based wire page to a 0-based pageIndex', () => {
    const params = new URLSearchParams('page=3&size=10')
    expect(parsePagination(params, DEFAULT_PARAM_KEYS, 25)).toEqual({ pageIndex: 2, pageSize: 10 })
  })

  it('falls back for a non-positive or non-numeric value', () => {
    const params = new URLSearchParams('page=0&size=abc')
    expect(parsePagination(params, DEFAULT_PARAM_KEYS, 25)).toEqual({ pageIndex: 0, pageSize: 25 })
  })
})

describe('parseSorting / serializeSorting', () => {
  it('round-trips ascending and descending columns', () => {
    const sorting = [{ id: 'name', desc: false }, { id: 'monthlySpend', desc: true }]
    const wire = serializeSorting(sorting)
    expect(wire).toBe('name,-monthlySpend')

    const params = new URLSearchParams(`sort=${wire}`)
    expect(parseSorting(params, DEFAULT_PARAM_KEYS)).toEqual(sorting)
  })

  it('returns an empty array when the sort param is absent', () => {
    expect(parseSorting(new URLSearchParams(), DEFAULT_PARAM_KEYS)).toEqual([])
  })

  it('ignores blank tokens from stray commas', () => {
    const params = new URLSearchParams('sort=name,,-id')
    expect(parseSorting(params, DEFAULT_PARAM_KEYS)).toEqual([
      { id: 'name', desc: false },
      { id: 'id', desc: true },
    ])
  })
})

describe('parseGlobalFilter', () => {
  it('reads the query key, defaulting to an empty string', () => {
    expect(parseGlobalFilter(new URLSearchParams(), DEFAULT_PARAM_KEYS)).toBe('')
    expect(parseGlobalFilter(new URLSearchParams('q=nordwind'), DEFAULT_PARAM_KEYS)).toBe('nordwind')
  })
})

describe('prefixParamKeys', () => {
  it('returns the default keys when no prefix is given', () => {
    expect(prefixParamKeys(undefined)).toEqual(DEFAULT_PARAM_KEYS)
  })

  it('namespaces every key so two grids can share one URL', () => {
    expect(prefixParamKeys('grid')).toEqual({
      page: 'grid.page',
      size: 'grid.size',
      sort: 'grid.sort',
      query: 'grid.q',
    })
  })
})

describe('setOrDelete', () => {
  it('deletes the key when the value equals the fallback', () => {
    const params = new URLSearchParams('page=3')
    setOrDelete(params, 'page', '1', '1')
    expect(params.has('page')).toBe(false)
  })

  it('deletes the key when the value is empty', () => {
    const params = new URLSearchParams('q=nordwind')
    setOrDelete(params, 'q', '', '')
    expect(params.has('q')).toBe(false)
  })

  it('sets the key otherwise, so ?page=1 never appears as noise', () => {
    const params = new URLSearchParams()
    setOrDelete(params, 'page', '3', '1')
    expect(params.get('page')).toBe('3')
  })
})
