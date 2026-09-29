import { describe, expect, it } from 'vitest'
import type { CrmSettings } from '@/features/crm-settings/schema'
import type { PipelineStage } from '../schema'
import { summarizeCreation } from './creationSummary'

const base = {
  defaultPipelineDefinitionId: 3, opportunityCreationMode: 'Form', opportunityCreationSteps: ['Customer'], defaultOpportunityTypeId: 7,
  requireLostReason: false, requireWonLine: false, defaultAssignmentMode: 'Manual', assignmentPolicy: 'AnyAssignablePrincipal',
  defaultPrincipal: null, defaultTeamId: null, defaultTerritoryId: null, rowVersion: 1,
  pipelines: [{ id: 3, name: 'Retail', rowVersion: 1, isActive: true, isArchived: false, versions: [] }],
  opportunityTypes: [{ id: 7, key: 'new', name: 'New customer', status: 'Active', rowVersion: 1 }],
  lostReasons: [], customerNeeds: [],
} as unknown as CrmSettings
const stages: PipelineStage[] = [
  { id: 1, name: 'Qualified', sortOrder: 1, isActive: true, isEntry: true, isArchived: false, kind: 'Open' },
  { id: 2, name: 'Won', sortOrder: 2, isActive: true, isEntry: false, isArchived: false, kind: 'Won' },
]

describe('summarizeCreation', () => {
  it('reads the owner, type, pipeline and entry stage the settings decide', () => {
    expect(summarizeCreation(base, stages)).toEqual({ owner: { kind: 'self' }, typeName: 'New customer', pipelineName: 'Retail', entryStageName: 'Qualified', blocker: null })
  })

  it('names a fixed owner only when a default principal is actually configured', () => {
    expect(summarizeCreation({ ...base, defaultAssignmentMode: 'DefaultPrincipal', defaultPrincipal: { issuer: 'i', subject: 's' } }, stages).owner).toEqual({ kind: 'fixed' })
    expect(summarizeCreation({ ...base, defaultAssignmentMode: 'DefaultPrincipal' }, stages).owner).toEqual({ kind: 'self' })
  })

  it.each(['Team', 'Territory'] as const)('blocks creation when %s assignment has no directory behind it', (mode) => {
    expect(summarizeCreation({ ...base, defaultAssignmentMode: mode }, stages).blocker).toEqual({ kind: 'unsupported', mode })
  })

  it('leaves pipeline, entry stage and type empty when nothing usable is configured', () => {
    const summary = summarizeCreation({ ...base, defaultPipelineDefinitionId: null, defaultOpportunityTypeId: 99 }, stages)
    expect(summary).toMatchObject({ pipelineName: null, entryStageName: null, typeName: null })
  })

  it('tolerates settings that have not loaded yet', () => {
    expect(summarizeCreation(undefined, undefined)).toMatchObject({ owner: { kind: 'self' }, pipelineName: null, blocker: null })
  })
})
