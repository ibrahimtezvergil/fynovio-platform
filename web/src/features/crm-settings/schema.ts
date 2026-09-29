import { z } from 'zod'
import { pipelineStageKindSchema } from '@/types/schemas'

const item = z.object({ id: z.number().int().positive(), key: z.string(), name: z.string(), status: z.enum(['Active', 'Inactive', 'Archived']) })
const pipelineStage = z.object({ id: z.number().int().positive(), name: z.string(), sortOrder: z.number().int(), isActive: z.boolean(), isEntry: z.boolean(), isArchived: z.boolean(), kind: pipelineStageKindSchema })
const pipelineVersion = z.object({ id: z.number().int().positive(), versionNumber: z.number().int().positive(), status: z.enum(['Draft', 'Published', 'Superseded', 'Archived']), enforceAllowedTransitions: z.boolean(), publishedAt: z.string().datetime({ offset: true }).nullable(), stages: z.array(pipelineStage), allowedTransitions: z.array(z.object({ fromStageId: z.number().int().positive(), toStageId: z.number().int().positive() })) })
export const crmSettingsSchema = z.object({
  defaultPipelineDefinitionId: z.number().int().positive().nullable(),
  opportunityCreationMode: z.enum(['Form', 'Wizard']),
  opportunityCreationSteps: z.array(z.enum(['Customer', 'Needs', 'Products'])).min(1).max(3).default(['Customer', 'Needs', 'Products']),
  defaultOpportunityTypeId: z.number().int().positive().nullable(),
  requireLostReason: z.boolean(), requireWonLine: z.boolean(),
  defaultAssignmentMode: z.enum(['Manual', 'DefaultPrincipal', 'Team', 'Territory']),
  assignmentPolicy: z.enum(['AnyAssignablePrincipal', 'ManagerOnly', 'ManualOnly']),
  defaultPrincipal: z.object({ issuer: z.string(), subject: z.string() }).nullable(),
  defaultTeamId: z.number().int().positive().nullable(), defaultTerritoryId: z.number().int().positive().nullable(),
  rowVersion: z.number().int().nonnegative(), pipelines: z.array(z.object({ id: z.number().int().positive(), name: z.string(), rowVersion: z.number().int().nonnegative(), isActive: z.boolean(), isArchived: z.boolean(), versions: z.array(pipelineVersion) })), opportunityTypes: z.array(item.extend({ rowVersion: z.number().int().nonnegative() })), lostReasons: z.array(item.extend({ rowVersion: z.number().int().nonnegative() })),
  customerNeeds: z.array(z.object({ id: z.number().int().positive(), name: z.string(), category: z.string().nullable(), averagePrice: z.number(), status: z.enum(['Active', 'Inactive', 'Archived']), rowVersion: z.number().int().nonnegative() })),
})
export type CrmSettings = z.infer<typeof crmSettingsSchema>
