import { describe, expect, it } from 'vitest'
import { needsReload, toProblem } from './problem'

const error = (status: number, code?: string, message = 'server text') => ({ status, code, message })

describe('toProblem — the backend ProblemDetails types map to the right UX', () => {
  it.each([
    [error(409, 'concurrency_conflict'), 'concurrency'],
    [error(409, 'idempotency_key_reused'), 'idempotencyConflict'],
    [error(409, 'illegal_lifecycle_transition'), 'lifecycle'],
    [error(409, 'invalid_pipeline_transition'), 'pipeline'],
    [error(422, 'principal_not_assignable'), 'notAssignable'],
    [error(409, 'invalid_pipeline_configuration'), 'pipeline'],
    [error(400, 'validation_error'), 'validation'],
    [error(404, 'not_found'), 'notFound'],
    [error(403, 'forbidden'), 'forbidden'],
    [error(400), 'validation'], // e.g. a framework binding failure, no `type`
    [error(401), 'unauthorized'],
    [error(429), 'rateLimited'],
    [error(500), 'unavailable'],
    [error(502), 'unavailable'],
    [error(0), 'unavailable'],
  ])('%j → %s', (input, kind) => {
    expect(toProblem(input).kind).toBe(kind)
  })

  it('shows the backend sentence only for the codes whose title is a deliberate domain message', () => {
    expect(toProblem(error(409, 'illegal_lifecycle_transition', 'Cannot win an opportunity in status Draft.')).detail).toBe('Cannot win an opportunity in status Draft.')
    expect(toProblem(error(400, 'validation_error', 'Currency must be a 3-letter ISO code.')).detail).toBe('Currency must be a 3-letter ISO code.')
  })

  it('never carries server text for 404, 403, 5xx or network failures', () => {
    for (const input of [error(404, 'not_found', 'Opportunity 9 was not found.'), error(403, 'forbidden', 'denied'), error(500, undefined, 'NullReferenceException at Foo.Bar'), error(0, undefined, 'Network Error')]) {
      expect(toProblem(input).detail).toBeUndefined()
    }
  })

  it('a concurrency conflict is the only kind that offers a reload', () => {
    expect(needsReload(toProblem(error(409, 'concurrency_conflict')))).toBe(true)
    expect(needsReload(toProblem(error(409, 'illegal_lifecycle_transition')))).toBe(false)
  })
})
