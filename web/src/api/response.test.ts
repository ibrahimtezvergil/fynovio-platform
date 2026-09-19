import { describe, expect, it } from 'vitest'
import { isArrayOf, parseApiResponse, unwrapApiResponse } from '@/api/response'
import { dealSchema } from '@/types/schemas'

describe('unwrapApiResponse', () => {
  it('accepts a direct payload', () => {
    expect(unwrapApiResponse(['a'], isArrayOf<string>)).toEqual(['a'])
  })

  it('unwraps a data envelope', () => {
    expect(unwrapApiResponse({ data: ['a'] }, isArrayOf<string>)).toEqual(['a'])
  })

  it('rejects an unexpected payload shape', () => {
    expect(() => unwrapApiResponse({ results: [] }, isArrayOf<string>)).toThrow()
  })

  it('rejects a payload whose records do not match its schema', () => {
    expect(() => parseApiResponse([{ title: 'Missing id' }], dealSchema.array())).toThrow()
  })
})
