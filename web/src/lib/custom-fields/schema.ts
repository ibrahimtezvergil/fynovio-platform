import { z } from 'zod'

/** The value types a tenant can define (adr-tier1-custom-fields.md decision 3); wire names match the database. */
export const CUSTOM_FIELD_TYPES = ['text', 'long_text', 'number', 'decimal', 'boolean', 'date', 'select', 'multi_select', 'email', 'phone', 'url'] as const
export type CustomFieldType = (typeof CUSTOM_FIELD_TYPES)[number]

export const customFieldOptionSchema = z.object({ key: z.string(), label: z.string(), isDeprecated: z.boolean() })
export type CustomFieldOption = z.infer<typeof customFieldOptionSchema>

export const customFieldDefinitionSchema = z.object({
  id: z.number().int().positive(),
  fieldName: z.string(),
  label: z.string(),
  fieldType: z.enum(CUSTOM_FIELD_TYPES),
  isRequired: z.boolean(),
  config: z.object({
    options: z.array(customFieldOptionSchema).nullish(),
    scale: z.number().int().nullish(),
    min: z.number().nullish(),
    max: z.number().nullish(),
    maxLength: z.number().int().nullish(),
  }),
  status: z.enum(['Active', 'Deprecated']),
  sortOrder: z.number().int(),
  rowVersion: z.number().int().positive(),
})
export type CustomFieldDefinition = z.infer<typeof customFieldDefinitionSchema>

export const customFieldDefinitionsSchema = z.array(customFieldDefinitionSchema)
export const customFieldImpactSchema = z.object({ definitionId: z.number(), fieldName: z.string(), opportunitiesWithValue: z.number().int().nonnegative() })
export const manageCustomFieldResultSchema = z.object({ definitionId: z.number(), rowVersion: z.number(), replayed: z.boolean() })

/** A record's stored custom field object: key → JSON value. Keys of deprecated or unknown fields may be present. */
export const customFieldValuesSchema = z.record(z.string(), z.unknown())
export type CustomFieldValues = z.infer<typeof customFieldValuesSchema>

export const isOptionType = (type: CustomFieldType) => type === 'select' || type === 'multi_select'
