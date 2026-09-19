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
