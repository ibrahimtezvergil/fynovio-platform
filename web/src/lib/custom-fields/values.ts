import type { CustomFieldDefinition, CustomFieldValues } from './schema'

/** What an input holds while being edited: text for scalar types, a flag for boolean, option keys for multi-select. */
export type CustomFieldDraft = string | boolean | string[]
export type CustomFieldDrafts = Record<string, CustomFieldDraft>

/** Active fields in display order — what forms and list columns render. */
export const activeFields = (definitions: readonly CustomFieldDefinition[]) =>
  definitions.filter((definition) => definition.status === 'Active').toSorted((a, b) => a.sortOrder - b.sortOrder || a.fieldName.localeCompare(b.fieldName))

export function toDraft(definition: CustomFieldDefinition, value: unknown): CustomFieldDraft {
  switch (definition.fieldType) {
    case 'boolean':
      return value === true
    case 'multi_select':
      return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : []
    case 'number':
    case 'decimal':
      return typeof value === 'number' ? String(value) : ''
    default:
      return typeof value === 'string' ? value : ''
  }
}

export function toDrafts(definitions: readonly CustomFieldDefinition[], values: CustomFieldValues | null | undefined): CustomFieldDrafts {
  return Object.fromEntries(definitions.map((definition) => [definition.fieldName, toDraft(definition, values?.[definition.fieldName])]))
}

/**
 * The JSON value to send, or undefined for "no value" (the key is then omitted). A number the user typed with a
 * decimal comma is accepted; anything not a finite number is sent as the raw text so the server reports it.
 */
export function fromDraft(definition: CustomFieldDefinition, draft: CustomFieldDraft | undefined): unknown {
  if (draft === undefined) return undefined
  switch (definition.fieldType) {
    case 'boolean':
      return draft === true ? true : undefined
    case 'multi_select':
      return Array.isArray(draft) && draft.length > 0 ? draft : undefined
    case 'number':
    case 'decimal': {
      const text = typeof draft === 'string' ? draft.trim() : ''
      if (text === '') return undefined
      const parsed = Number(text.replace(',', '.'))
      return Number.isFinite(parsed) ? parsed : text
    }
    default: {
      const text = typeof draft === 'string' ? draft.trim() : ''
      return text === '' ? undefined : text
    }
  }
}

/** The full-replacement object for the active fields (deprecated values are carried forward by the server). */
export function toPayload(definitions: readonly CustomFieldDefinition[], drafts: CustomFieldDrafts): CustomFieldValues {
  const payload: CustomFieldValues = {}
  for (const definition of activeFields(definitions)) {
    const value = fromDraft(definition, drafts[definition.fieldName])
    if (value !== undefined) payload[definition.fieldName] = value
  }
  return payload
}

/** Required active fields left empty — checked before sending so the person sees it without a round trip. */
export const missingRequired = (definitions: readonly CustomFieldDefinition[], drafts: CustomFieldDrafts) =>
  activeFields(definitions).filter((definition) => definition.isRequired && fromDraft(definition, drafts[definition.fieldName]) === undefined).map((definition) => definition.fieldName)

export const hasValue = (value: unknown) => value !== undefined && value !== null && value !== '' && !(Array.isArray(value) && value.length === 0)

/** Human-readable value: option labels instead of keys, a localized date and number; '' when absent. */
export function formatCustomFieldValue(definition: CustomFieldDefinition, value: unknown, yesNo: { yes: string; no: string }, locale?: string): string {
  if (!hasValue(value)) return ''
  const optionLabel = (key: unknown) => definition.config.options?.find((option) => option.key === key)?.label ?? String(key)
  switch (definition.fieldType) {
    case 'boolean':
      return value === true ? yesNo.yes : yesNo.no
    case 'select':
      return optionLabel(value)
    case 'multi_select':
      return Array.isArray(value) ? value.map(optionLabel).join(', ') : String(value)
    case 'number':
    case 'decimal':
      return typeof value === 'number'
        ? new Intl.NumberFormat(locale, { minimumFractionDigits: definition.config.scale ?? 0, maximumFractionDigits: definition.config.scale ?? 0 }).format(value)
        : String(value)
    case 'date': {
      if (typeof value !== 'string') return String(value)
      const [year, month, day] = value.split('-').map(Number)
      return year && month && day ? new Intl.DateTimeFormat(locale).format(new Date(year, month - 1, day)) : value
    }
    default:
      return String(value)
  }
}

/** Turkish letters and anything else fold into the key alphabet (`^[a-z][a-z0-9_]{1,62}$`). */
export const keyFromLabel = (label: string) => label.trim().toLocaleLowerCase('tr').replace(/ı/g, 'i').normalize('NFKD').replace(/[̀-ͯ]/g, '')
  .replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '').replace(/^(\d)/, 'f_$1').slice(0, 63)
