import { describe, expect, it } from 'vitest'
import { i18n } from '@/lib/i18n'
import type { ApiError } from '@/types'
import { describeProblem, needsReload, PROBLEM_KINDS, toProblem, type ProblemKind } from './problem'

const error = (status: number, code?: string): ApiError => ({ message: 'server text that must never be shown', status, code })

describe('toProblem — every ProblemDetails `type` in the contract', () => {
  it.each<[number, string, ProblemKind]>([
    [400, 'validation_error', 'validation'],
    [403, 'forbidden', 'forbidden'],
    [404, 'not_found', 'notFound'],
    [409, 'concurrency_conflict', 'concurrency'],
    [409, 'idempotency_key_reused', 'idempotencyConflict'],
    [422, 'range_too_large', 'rangeTooLarge'],
    [422, 'link_target_unavailable', 'linkUnavailable'],
  ])('%i %s → %s', (status, code, kind) => {
    expect(toProblem(error(status, code)).kind).toBe(kind)
  })

  it('tells the two 409s apart by `type`, not by status', () => {
    expect(toProblem(error(409, 'concurrency_conflict')).kind).not.toBe(toProblem(error(409, 'idempotency_key_reused')).kind)
  })

  it('falls back on the status when there is no recognised type (framework 401, gateway errors)', () => {
    expect(toProblem(error(401)).kind).toBe('unauthorized')
    expect(toProblem(error(400)).kind).toBe('validation')
    expect(toProblem(error(403)).kind).toBe('forbidden')
    expect(toProblem(error(404)).kind).toBe('notFound')
    expect(toProblem(error(429)).kind).toBe('rateLimited')
    expect(toProblem(error(500)).kind).toBe('unavailable')
    expect(toProblem(error(0)).kind).toBe('unavailable')
    expect(toProblem(error(422, 'something_new')).kind).toBe('unavailable')
  })

  it('a stale write and a vanished entry are cleared by reloading; a validation error is not', () => {
    expect(needsReload({ kind: 'concurrency' })).toBe(true)
    expect(needsReload({ kind: 'notFound' })).toBe(true)
    expect(needsReload({ kind: 'validation' })).toBe(false)
  })
})

describe('describeProblem — translated, and never the server’s text', () => {
  it.each(['tr', 'en'])('every kind has a title and description in %s', async (language) => {
    const t = i18n.getFixedT(language, 'calendar')
    for (const kind of PROBLEM_KINDS) {
      const { title, description } = describeProblem(t, { kind })
      expect(title, `${language} ${kind} title`).not.toMatch(/^problem\./)
      expect(description, `${language} ${kind} description`).not.toMatch(/^problem\./)
      expect(title.length).toBeGreaterThan(0)
      expect(description).not.toContain('server text')
    }
  })

  it('the two languages actually differ', () => {
    const tr = describeProblem(i18n.getFixedT('tr', 'calendar'), { kind: 'concurrency' })
    const en = describeProblem(i18n.getFixedT('en', 'calendar'), { kind: 'concurrency' })
    expect(tr.title).not.toBe(en.title)
  })
})
