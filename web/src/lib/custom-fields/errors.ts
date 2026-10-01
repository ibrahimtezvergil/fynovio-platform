import type { TFunction } from 'i18next'
import type { ApiError } from '@/types'

const KNOWN_CODES = new Set(['required', 'unknown_field', 'field_deprecated', 'invalid_type', 'invalid_value', 'out_of_range', 'too_long', 'invalid_option', 'option_deprecated', 'payload_too_large', 'invalid_reference'])

/** A 422 `custom_field_invalid` as one message per field, worded in the user's language from the machine code. */
export function customFieldErrors(t: TFunction<'opportunities'>, error: ApiError | null | undefined): Record<string, string> {
  if (!error || error.code !== 'custom_field_invalid') return {}
  const codes = error.fieldCodes ?? {}
  return Object.fromEntries(Object.entries(codes).map(([field, fieldCodes]) => {
    const code = fieldCodes.find((candidate) => KNOWN_CODES.has(candidate))
    return [field, code ? t(`customFields.errors.${code}`) : (error.fields?.[field]?.[0] ?? t('customFields.errors.invalid_value'))]
  }))
}

export const requiredErrors = (t: TFunction<'opportunities'>, fieldNames: readonly string[]) =>
  Object.fromEntries(fieldNames.map((fieldName) => [fieldName, t('customFields.errors.required')]))
