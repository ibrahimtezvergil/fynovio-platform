import { z } from 'zod'
import { STAGES, type Activity, type Deal, type StageBucket, type User } from '@/types'

const activityKinds = ['task', 'message', 'email', 'review'] as const
const userRoles = ['admin', 'manager', 'viewer'] as const

export const dealSchema: z.ZodType<Deal> = z.object({
  id: z.string(),
  title: z.string(),
  account: z.string(),
  stage: z.enum(STAGES),
  owner: z.string(),
  value: z.number(),
  closeDate: z.string().nullable(),
  probability: z.number(),
})

export const stageBucketSchema: z.ZodType<StageBucket> = z.object({
  stage: z.enum(STAGES),
  count: z.number(),
  value: z.number(),
})

export const activitySchema: z.ZodType<Activity> = z.object({
  id: z.string(),
  kind: z.enum(activityKinds),
  title: z.string(),
  when: z.string(),
})

export const userSchema: z.ZodType<User> = z.object({
  id: z.string(),
  name: z.string(),
  email: z.string(),
  role: z.enum(userRoles),
  initials: z.string(),
})

/**
 * A pipeline stage's kind. Won and Lost are the system's closing stages (reached only through the win/lose commands);
 * every other stage is Open. The API sends the integer ordinal — a name is accepted too — and an absent kind reads as Open.
 */
export const PIPELINE_STAGE_KINDS = ['Open', 'Won', 'Lost'] as const
export type PipelineStageKind = (typeof PIPELINE_STAGE_KINDS)[number]
export const pipelineStageKindSchema = z
  .union([z.number().int().min(0).max(2), z.enum(PIPELINE_STAGE_KINDS)])
  .default('Open')
  .transform((wire): PipelineStageKind => (typeof wire === 'number' ? PIPELINE_STAGE_KINDS[wire] : wire))
/** The ordinal a request body carries for a kind (the API binds enums as integers). */
export const pipelineStageKindWire = (kind: PipelineStageKind): number => PIPELINE_STAGE_KINDS.indexOf(kind)
