import { describe, expect, it } from 'vitest'
import { i18n } from '@/lib/i18n'
import { DEFAULT_POLICY, violationMessages } from './passwordPolicy'

const t = i18n.getFixedT(null, 'auth')

describe('violationMessages', () => {
  it("words the server's codes with the server's limits", () => {
    const text = violationMessages(t, ['too_short', 'equals_email'], { minLength: 14, maxLength: 100 })
    expect(text).toContain('14')
    expect(text).toContain(t('passwordPolicy.equals_email'))
  })

  it('never shows a raw or unknown code — it falls back to a sentence', () => {
    expect(violationMessages(t, ['brand_new_rule'], DEFAULT_POLICY)).toBe(t('passwordPolicy.invalid'))
    expect(violationMessages(t, [], DEFAULT_POLICY)).toBe(t('passwordPolicy.invalid'))
    expect(violationMessages(t, undefined, DEFAULT_POLICY)).toBe(t('passwordPolicy.invalid'))
    expect(violationMessages(t, ['brand_new_rule'], DEFAULT_POLICY)).not.toContain('brand_new_rule')
  })
})
