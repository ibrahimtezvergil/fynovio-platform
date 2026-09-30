import { describe, expect, it } from 'vitest'
import type { CustomFieldDefinition } from '@/lib/custom-fields/schema'
import { createOpportunityColumns } from './opportunitiesTable'

const t = ((key: string) => key) as never
const region: CustomFieldDefinition = {
  id: 7, fieldName: 'region', label: 'Region', fieldType: 'select', isRequired: false, status: 'Active', sortOrder: 1, rowVersion: 1,
  config: { options: [{ key: 'north', label: 'North', isDeprecated: false }] },
}

describe('opportunity columns', () => {
  it('adds one column per active custom field, before the row actions, labelled by the definition', () => {
    const columns = createOpportunityColumns(t, '/crm/opportunities', () => {}, [region])
    const ids = columns.map((column) => column.id)

    expect(ids).toContain('cf:region')
    expect(ids.indexOf('cf:region')).toBe(ids.indexOf('rowActions') - 1)
    expect(columns.find((column) => column.id === 'cf:region')?.header).toBe('Region')
  })

  it('adds nothing when the tenant has no custom fields', () => {
    expect(createOpportunityColumns(t, '/crm/opportunities', () => {}).some((column) => column.id?.startsWith('cf:'))).toBe(false)
  })
})
