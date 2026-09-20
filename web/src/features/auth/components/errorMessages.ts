import type { TFunction } from 'i18next'
import type { ApiError } from '@/types'

/** The wording shared by every auth form for failures that are not about one specific field. */
export function commonErrorMessage(t: TFunction<'auth'>, error: ApiError): string {
  if (error.status === 429) {
    return error.retryAfterSeconds ? t('errors.rateLimited', { seconds: error.retryAfterSeconds }) : t('errors.rateLimitedNoWait')
  }
  return t('errors.generic')
}
