import { describe, expect, it } from 'vitest'
import { AttemptKeys, isDefinitiveOutcome } from './attemptKey'

const counter = () => {
  let n = 0
  return () => `key-${++n}`
}

describe('isDefinitiveOutcome', () => {
  it.each([
    [null, true],
    [{ message: '', status: 400 }, true],
    [{ message: '', status: 403 }, true],
    [{ message: '', status: 409 }, true],
    [{ message: '', status: 0 }, false],
    [{ message: '', status: 500 }, false],
    [{ message: '', status: 503 }, false],
    [{ message: '', status: 429 }, false],
    [{ message: '', status: 408 }, false],
  ])('%j → definitive: %s', (error, expected) => {
    expect(isDefinitiveOutcome(error)).toBe(expected)
  })
})

describe('AttemptKeys', () => {
  it('reuses the key for the same payload while the outcome is unknown (a network retry)', () => {
    const keys = new AttemptKeys(counter())
    const first = keys.begin({ id: 1, expectedVersion: 5 })
    keys.settle({ message: 'network', status: 0 })
    expect(keys.begin({ id: 1, expectedVersion: 5 })).toBe(first)
  })

  it('keeps the key across several unknown outcomes in a row', () => {
    const keys = new AttemptKeys(counter())
    const first = keys.begin({ a: 1 })
    keys.settle({ message: '', status: 503 })
    keys.begin({ a: 1 })
    keys.settle({ message: '', status: 0 })
    expect(keys.begin({ a: 1 })).toBe(first)
  })

  it('releases the key once the server has answered definitively, so the next action gets a new one', () => {
    const keys = new AttemptKeys(counter())
    const first = keys.begin({ a: 1 })
    keys.settle(null)
    expect(keys.begin({ a: 1 })).not.toBe(first)
  })

  it('a 409 is definitive too (the reload that follows is a new logical action)', () => {
    const keys = new AttemptKeys(counter())
    const first = keys.begin({ a: 1 })
    keys.settle({ message: '', status: 409, code: 'concurrency_conflict' })
    expect(keys.begin({ a: 1 })).not.toBe(first)
  })

  it('a different payload never reuses a held key (same key + other body would be a 409 idempotency conflict)', () => {
    const keys = new AttemptKeys(counter())
    const first = keys.begin({ reason: 'a' })
    keys.settle({ message: '', status: 0 })
    expect(keys.begin({ reason: 'b' })).not.toBe(first)
  })
})
