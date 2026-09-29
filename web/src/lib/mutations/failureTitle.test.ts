import { describe, expect, it } from 'vitest'
import { i18n } from '@/lib/i18n'
import type { ApiError } from '@/types'
import { failureTitle } from './failureTitle'

const error = (status: number, message: string) => ({ status, message }) as ApiError

describe('failureTitle', () => {
  it('replaces the transport layer\'s English default with a localized sentence for the status', () => {
    expect(failureTitle(error(404, 'Request failed with status code 404'))).toBe(i18n.t('api.requestFailed.notFound'))
    expect(failureTitle(error(0, 'Network Error'))).toBe(i18n.t('api.requestFailed.network'))
    expect(failureTitle(error(503, 'Request failed with status code 503'))).toBe(i18n.t('api.requestFailed.server'))
  })

  it('keeps a sentence the server wrote for people', () => {
    expect(failureTitle(error(422, 'Stage names must be unique'))).toBe('Stage names must be unique')
  })
})
