import type { CrmSettings } from '@/features/crm-settings/schema'
import type { PipelineStage } from '../schema'

export type OwnerRule =
  /** The person creating the opportunity owns it. */
  | { kind: 'self' }
  /** The workspace assigns every new opportunity to one named person. */
  | { kind: 'fixed' }
  /** Team/territory assignment has no directory behind it yet, so the server refuses to create the record. */
  | { kind: 'unsupported'; mode: 'Team' | 'Territory' }

export interface CreationSummary {
  owner: OwnerRule
  typeName: string | null
  pipelineName: string | null
  entryStageName: string | null
  /** A reason the opportunity cannot be created at all; when set, the form must not be submittable. */
  blocker: OwnerRule | null
}

/** What the workspace settings decide for a new opportunity, so the person knows the outcome before pressing the button. */
export function summarizeCreation(settings: CrmSettings | undefined, defaultStages: readonly PipelineStage[] | undefined): CreationSummary {
  const mode = settings?.defaultAssignmentMode ?? 'Manual'
  const owner: OwnerRule = mode === 'Team' || mode === 'Territory' ? { kind: 'unsupported', mode } : mode === 'DefaultPrincipal' && settings?.defaultPrincipal ? { kind: 'fixed' } : { kind: 'self' }
  const pipeline = settings?.pipelines.find((item) => item.id === settings.defaultPipelineDefinitionId && item.isActive && !item.isArchived)
  const type = settings?.opportunityTypes.find((item) => item.id === settings.defaultOpportunityTypeId && item.status === 'Active')
  const entry = defaultStages?.find((stage) => stage.isEntry && stage.kind === 'Open' && stage.isActive && !stage.isArchived)
  return {
    owner,
    typeName: type?.name ?? null,
    pipelineName: pipeline?.name ?? null,
    entryStageName: pipeline ? (entry?.name ?? null) : null,
    blocker: owner.kind === 'unsupported' ? owner : null,
  }
}
