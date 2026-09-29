import { i18n } from '@/lib/i18n'
import type { ApiError } from '@/types'

/** What the transport layer says when the server said nothing of its own (axios's own sentences, in English). */
const TRANSPORT_MESSAGE = /^(Request failed with status code \d+|Network Error|timeout of \d+ms exceeded)$/i

/**
 * The title of a failed request's toast. A sentence the server wrote for people is kept; the transport layer's
 * default text ("Request failed with status code 404") is replaced by a plain, localized one for the status.
 */
export function failureTitle(error: ApiError): string {
  if (error.message && !TRANSPORT_MESSAGE.test(error.message)) return error.message
  if (error.status === 0) return i18n.t('api.requestFailed.network')
  if (error.status === 401) return i18n.t('api.requestFailed.unauthorized')
  if (error.status === 403) return i18n.t('api.requestFailed.forbidden')
  if (error.status === 404) return i18n.t('api.requestFailed.notFound')
  if (error.status === 409) return i18n.t('api.requestFailed.conflict')
  if (error.status >= 500) return i18n.t('api.requestFailed.server')
  return i18n.t('api.requestFailed.generic')
}
