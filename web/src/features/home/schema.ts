import { z } from 'zod'

export const attentionSeverities = ['critical', 'warning'] as const
export type AttentionSeverity = (typeof attentionSeverities)[number]

export interface AttentionAction {
  label: string
  url: string
}

/**
 * A single focus-queue item. Business rules (what counts as critical, what
 * gets surfaced) live upstream of this shape — the UI only ever renders the
 * item it is given. See `docs` in the master prompt: Domain Events → Attention
 * Engine → Dashboard API → this shape.
 */
export interface AttentionItem {
  id: string
  type: string
  severity: AttentionSeverity
  title: string
  description?: string
  entityType?: string
  entityId?: string
  entityLabel?: string
  documentNumber?: string
  amount?: number
  priority: number
  /** Pre-formatted for now, like `Activity.when` — becomes an ISO timestamp once a backend exists. */
  timestamp: string
  owner?: string
  action?: AttentionAction
}

export const attentionItemSchema: z.ZodType<AttentionItem> = z.object({
  id: z.string(),
  type: z.string(),
  severity: z.enum(attentionSeverities),
  title: z.string(),
  description: z.string().optional(),
  entityType: z.string().optional(),
  entityId: z.string().optional(),
  entityLabel: z.string().optional(),
  documentNumber: z.string().optional(),
  amount: z.number().optional(),
  priority: z.number(),
  timestamp: z.string(),
  owner: z.string().optional(),
  action: z.object({ label: z.string(), url: z.string() }).optional(),
})

export const recentWorkEntityTypes = ['quote', 'order', 'customer'] as const
export type RecentWorkEntityType = (typeof recentWorkEntityTypes)[number]

export interface RecentWorkItem {
  id: string
  entityType: RecentWorkEntityType
  entityId: string
  title: string
  subtitle?: string
  reference?: string
  route: string
  /** Pre-formatted, like `Activity.when` (e.g. "12 dk önce", "Dün"). */
  lastAccessedAt: string
}

export const recentWorkItemSchema: z.ZodType<RecentWorkItem> = z.object({
  id: z.string(),
  entityType: z.enum(recentWorkEntityTypes),
  entityId: z.string(),
  title: z.string(),
  subtitle: z.string().optional(),
  reference: z.string().optional(),
  route: z.string(),
  lastAccessedAt: z.string(),
})

export interface TeamActivityItem {
  id: string
  user: string
  avatarInitials: string
  status?: string
  action: string
  entityType?: string
  entityId?: string
  entityLabel?: string
  timestamp: string
  route?: string
}

export const teamActivityItemSchema: z.ZodType<TeamActivityItem> = z.object({
  id: z.string(),
  user: z.string(),
  avatarInitials: z.string(),
  status: z.string().optional(),
  action: z.string(),
  entityType: z.string().optional(),
  entityId: z.string().optional(),
  entityLabel: z.string().optional(),
  timestamp: z.string(),
  route: z.string().optional(),
})
