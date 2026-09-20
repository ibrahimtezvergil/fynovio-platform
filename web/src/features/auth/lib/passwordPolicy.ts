import type { TFunction } from 'i18next'

export interface PasswordPolicyHints {
  minLength: number
  maxLength: number
}

/** Shown until (and unless) the server's own policy arrives from `/auth/config`. */
export const DEFAULT_POLICY: PasswordPolicyHints = { minLength: 12, maxLength: 128 }

const KNOWN_CODES = ['too_short', 'too_long', 'equals_email', 'equals_email_local_part', 'required'] as const
type KnownCode = (typeof KNOWN_CODES)[number]
const isKnown = (code: string): code is KnownCode => (KNOWN_CODES as readonly string[]).includes(code)

/** The server's machine codes, worded for the user. An unknown code still gets a sentence, never a raw code. */
export function violationMessages(t: TFunction<'auth'>, codes: readonly string[] | undefined, policy: PasswordPolicyHints): string {
  const values = { min: policy.minLength, max: policy.maxLength }
  const known = (codes ?? []).filter(isKnown)
  if (known.length === 0) return t('passwordPolicy.invalid')
  return known.map((code) => t(`passwordPolicy.${code}`, values)).join(' ')
}
