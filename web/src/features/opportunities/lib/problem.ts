import type { TFunction } from 'i18next'
import type { ApiError } from '@/types'

/**
 * The user-facing classes of failure the Opportunity API can produce (spec §17). `ApiError` (from
 * `api/client.ts#toApiError`) is the single ProblemDetails parsing layer; this only decides what the
 * person should SEE for it. Nothing here re-parses a response body.
 */
export type ProblemKind =
  | 'validation'
  | 'unauthorized'
  | 'forbidden'
  | 'notFound'
  | 'concurrency'
  | 'idempotencyConflict'
  | 'lifecycle'
  | 'pipeline'
  | 'notAssignable'
  | 'rateLimited'
  | 'unavailable'

export interface Problem {
  kind: ProblemKind
  /** A backend-authored business-rule sentence that is safe to show; present only for the kinds that carry one. */
  detail?: string
}

/** ProblemDetails `type`s whose `title` is a deliberate domain sentence (an exception message the API owns), not an internal. */
const SAFE_DETAIL_CODES = new Set(['validation_error', 'illegal_lifecycle_transition', 'invalid_pipeline_transition'])

export function toProblem(error: ApiError): Problem {
  const detail = error.code && SAFE_DETAIL_CODES.has(error.code) ? error.message : undefined
  switch (error.code) {
    case 'concurrency_conflict':
      return { kind: 'concurrency' }
    case 'idempotency_key_reused':
      return { kind: 'idempotencyConflict' }
    case 'illegal_lifecycle_transition':
      return { kind: 'lifecycle', detail }
    case 'invalid_pipeline_transition':
    case 'invalid_pipeline_configuration':
      return { kind: 'pipeline', detail }
    case 'principal_not_assignable':
      return { kind: 'notAssignable' }
    case 'validation_error':
      return { kind: 'validation', detail }
    case 'not_found':
      return { kind: 'notFound' }
    case 'forbidden':
      return { kind: 'forbidden' }
  }
  if (error.status === 400) return { kind: 'validation' }
  if (error.status === 401) return { kind: 'unauthorized' }
  if (error.status === 403) return { kind: 'forbidden' }
  if (error.status === 404) return { kind: 'notFound' }
  if (error.status === 429) return { kind: 'rateLimited' }
  return { kind: 'unavailable' } // 5xx, network failure, timeout, anything unrecognised: generic and retry-safe
}

export function describeProblem(t: TFunction<'opportunities'>, problem: Problem): { title: string; description: string } {
  return {
    title: t(`problem.${problem.kind}.title`),
    description: problem.detail ?? t(`problem.${problem.kind}.description`),
  }
}

/** A kind the person can only clear by refetching the record. */
export const needsReload = (problem: Problem) => problem.kind === 'concurrency'
