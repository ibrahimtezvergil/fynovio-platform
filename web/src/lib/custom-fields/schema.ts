import { z } from 'zod'

/** The value types a tenant can define (adr-tier1-custom-fields.md decision 3); wire names match the database. */
export const CUSTOM_FIELD_TYPES = ['text', 'long_text', 'number', 'decimal', 'boolean', 'date', 'select', 'multi_select', 'email', 'phone', 'url', 'reference'] as const
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
    /** A reference field's one fixed target (adr-semantic-catalog-changeset.md S-6). */
    target: z.object({ boundedContext: z.string(), entityType: z.string() }).nullish(),
  }),
  status: z.enum(['Active', 'Deprecated']),
  sortOrder: z.number().int(),
  rowVersion: z.number().int().positive(),
})
export type CustomFieldDefinition = z.infer<typeof customFieldDefinitionSchema>

export const customFieldDefinitionsSchema = z.array(customFieldDefinitionSchema)
/** A shared view that places the field as a column — listed before the field is deprecated. */
export const dependentViewSchema = z.object({ id: z.number(), key: z.string(), name: z.string() })
export const customFieldImpactSchema = z.object({
  definitionId: z.number(), fieldName: z.string(), opportunitiesWithValue: z.number().int().nonnegative(),
  dependentViews: z.array(dependentViewSchema).default([]),
})
export const manageCustomFieldResultSchema = z.object({ definitionId: z.number(), rowVersion: z.number(), replayed: z.boolean(), changeSetId: z.number().nullish() })

/** A reference value as the *reader* may see it: the target's label, or only the id when it is not available to them. */
export const customFieldReferenceSchema = z.object({ id: z.number().int().positive(), accessible: z.boolean(), label: z.string().nullish() })
export type CustomFieldReference = z.infer<typeof customFieldReferenceSchema>
export const customFieldReferencesSchema = z.record(z.string(), customFieldReferenceSchema)
export type CustomFieldReferences = z.infer<typeof customFieldReferencesSchema>

/** A record's stored custom field object: key → JSON value. Keys of deprecated or unknown fields may be present. */
export const customFieldValuesSchema = z.record(z.string(), z.unknown())
export type CustomFieldValues = z.infer<typeof customFieldValuesSchema>

export const isReferenceType = (type: CustomFieldType) => type === 'reference'
export const isOptionType = (type: CustomFieldType) => type === 'select' || type === 'multi_select'
