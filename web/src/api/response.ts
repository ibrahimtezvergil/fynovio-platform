import { i18n } from '@/lib/i18n'
import type { z } from 'zod'

type ResponseGuard<T> = (value: unknown) => value is T

function isDataEnvelope(value: unknown): value is { data: unknown } {
  return typeof value === 'object' && value !== null && 'data' in value
}

/**
 * Accept direct API payloads and the `{ data: payload }` collection envelope
 * commonly returned by server APIs. The guard preserves the runtime contract
 * TypeScript types alone cannot guarantee.
 */
export function unwrapApiResponse<T>(payload: unknown, isExpected: ResponseGuard<T>): T {
  const value = isDataEnvelope(payload) ? payload.data : payload

  if (isExpected(value)) return value

  throw new Error(i18n.t('api.invalidResponse'))
}

/**
 * Validates direct payloads and `{ data: payload }` envelopes at the API boundary.
 * Keep `unwrapApiResponse` during the incremental migration of existing features.
 */
export function parseApiResponse<T>(payload: unknown, schema: z.ZodType<T>): T {
  const value = isDataEnvelope(payload) ? payload.data : payload
  const result = schema.safeParse(value)

  if (result.success) return result.data

  throw new Error(i18n.t('api.invalidResponse'))
}

export function isArrayOf<T>(value: unknown): value is T[] {
  return Array.isArray(value)
}
