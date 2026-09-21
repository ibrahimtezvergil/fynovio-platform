import type { TFunction } from 'i18next'
import type { ApiError } from '@/types'

/**
 * The user-facing classes of failure the Calendar API can produce (api-contract.md, "Errors"). `ApiError` (from
 * `api/client.ts#toApiError`) already carries ProblemDetails `type` as `code`; this only decides what the person
 * should SEE. Server text is never shown: every sentence is a translated string, so a missing translation is a
 * test failure and an internal message can never leak into the UI.
 */
export type ProblemKind =
  | 'validation'
  | 'unauthorized'
  | 'forbidden'
  | 'notFound'
  | 'concurrency'
  | 'idempotencyConflict'
  | 'rangeTooLarge'
  | 'linkUnavailable'
  | 'rateLimited'
  | 'unavailable'

export const PROBLEM_KINDS: readonly ProblemKind[] = [
  'validation',
  'unauthorized',
  'forbidden',
  'notFound',
  'concurrency',
  'idempotencyConflict',
  'rangeTooLarge',
  'linkUnavailable',
  'rateLimited',
  'unavailable',
]

export interface Problem {
  kind: ProblemKind
}

/** Discriminates on the ProblemDetails `type` first (both 409s share a status), then falls back to the status. */
export function toProblem(error: ApiError): Problem {
  switch (error.code) {
    case 'validation_error':
      return { kind: 'validation' }
    case 'forbidden':
      return { kind: 'forbidden' }
    case 'not_found':
      return { kind: 'notFound' }
    case 'concurrency_conflict':
      return { kind: 'concurrency' }
    case 'idempotency_key_reused':
      return { kind: 'idempotencyConflict' }
    case 'range_too_large':
      return { kind: 'rangeTooLarge' }
    case 'link_target_unavailable':
      return { kind: 'linkUnavailable' }
  }
  if (error.status === 400) return { kind: 'validation' }
  if (error.status === 401) return { kind: 'unauthorized' }
  if (error.status === 403) return { kind: 'forbidden' }
  if (error.status === 404) return { kind: 'notFound' }
  if (error.status === 429) return { kind: 'rateLimited' }
  return { kind: 'unavailable' } // 5xx, network failure, timeout, anything unrecognised: generic and retry-safe
}

export function describeProblem(t: TFunction<'calendar'>, problem: Problem): { title: string; description: string } {
  return {
    title: t(`problem.${problem.kind}.title`),
    description: t(`problem.${problem.kind}.description`),
  }
}

/** A kind the person can only clear by re-reading the entry from the server. */
export const needsReload = (problem: Problem) => problem.kind === 'concurrency' || problem.kind === 'notFound'
