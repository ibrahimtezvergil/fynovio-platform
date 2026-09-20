import type { TFunction } from 'i18next'
import { z } from 'zod'

/**
 * Canonical lifecycle values (Phase 1 lifecycle, closed set). Pipeline stages are NOT here on purpose:
 * they are tenant-configurable data and must never become a TypeScript union.
 */
export const OPPORTUNITY_STATUSES = ['Draft', 'Open', 'Won', 'Lost'] as const
export type OpportunityStatus = (typeof OPPORTUNITY_STATUSES)[number]

/** The API serializes `OpportunityStatus` as its integer ordinal (no string-enum converter is configured); a name is accepted too. */
const statusSchema = z.union([z.number().int().min(0).max(3), z.enum(OPPORTUNITY_STATUSES)]).transform((wire): OpportunityStatus =>
  typeof wire === 'number' ? OPPORTUNITY_STATUSES[wire] : wire,
)

// Everything the backend could one day omit or mask (field-level security) is `nullish` and rendered as absent, never inferred.
export const opportunityLineSchema = z.object({
  id: z.number(),
  quantity: z.number(),
  unitPrice: z.number(),
  lineTotal: z.number().nullish(),
  isOptional: z.boolean(),
  isCanceled: z.boolean(),
})
export type OpportunityLine = z.infer<typeof opportunityLineSchema>

export const opportunitySchema = z.object({
  id: z.number(),
  status: statusSchema,
  partyId: z.number().nullish(),
  assignedPrincipalIssuer: z.string().nullish(),
  assignedPrincipalSubject: z.string().nullish(),
  currency: z.string().nullish(),
  estimatedAmount: z.number().nullish(),
  totalAmount: z.number().nullish(),
  pipelineDefinitionVersionId: z.number().nullish(),
  pipelineStageId: z.number().nullish(),
  lostReason: z.string().nullish(),
  expiryDate: z.string().nullish(),
  openedDate: z.string().nullish(),
  wonDate: z.string().nullish(),
  lostDate: z.string().nullish(),
  /** The concurrency token. Kept for mutation requests; never rendered. */
  rowVersion: z.number(),
  lines: z.array(opportunityLineSchema).default([]),
})
export type Opportunity = z.infer<typeof opportunitySchema>

export const opportunitySummarySchema = z.object({
  id: z.number(),
  status: statusSchema,
  estimatedAmount: z.number().nullish(),
  currency: z.string().nullish(),
  assignedPrincipalIssuer: z.string().nullish(),
  assignedPrincipalSubject: z.string().nullish(),
  pipelineStageId: z.number().nullish(),
  partyId: z.number().nullish(),
  /** With `pipelineStageId` this names the stage: stage ids are only meaningful inside their pipeline version. */
  pipelineDefinitionVersionId: z.number().nullish(),
  expiryDate: z.string().nullish(),
})
export type OpportunitySummary = z.infer<typeof opportunitySummarySchema>

/** Server-authoritative projection: the UI renders exactly these booleans and never re-derives them. */
export const availableActionsSchema = z.object({
  canOpen: z.boolean(),
  canChangeStage: z.boolean(),
  allowedTargetStageIds: z.array(z.number()),
  canWin: z.boolean(),
  canLose: z.boolean(),
  canReassign: z.boolean(),
})
export type AvailableActions = z.infer<typeof availableActionsSchema>

export const pipelineStageSchema = z.object({
  id: z.number(),
  name: z.string(),
  sortOrder: z.number(),
  isActive: z.boolean(),
  isEntry: z.boolean(),
})
export type PipelineStage = z.infer<typeof pipelineStageSchema>

/** A member the SERVER says may be assigned this opportunity (authorization-aware; the UI never filters or adds to the list). */
export const assignablePrincipalSchema = z.object({
  issuer: z.string(),
  subject: z.string(),
  displayName: z.string(),
  email: z.string().nullish(),
})
export type AssignablePrincipal = z.infer<typeof assignablePrincipalSchema>

/** Display fields of a Party from the CRM reference query — nothing beyond what a picker needs. */
export const partyReferenceSchema = z.object({
  id: z.number(),
  partyType: z.string(),
  displayName: z.string(),
  email: z.string().nullish(),
})
export type PartyReference = z.infer<typeof partyReferenceSchema>

// The command results carry only what the caller needs; the canonical state is refetched after every command.
export const createResultSchema = z.object({ opportunityId: z.number(), replayed: z.boolean() })
export const commandResultSchema = z.object({ opportunityId: z.number(), replayed: z.boolean().optional() }).loose()

// ---- form input schemas (immediate UX only; the backend stays authoritative) ----

const CURRENCY = /^[A-Z]{3}$/
const hasAtMostTwoDecimals = (value: number) => Math.abs(Math.round(value * 100) - value * 100) < 1e-6

export function createOpportunityFormSchema(t: TFunction<'opportunities'>) {
  return z.object({
    partyId: z.coerce.number({ error: t('form.partyId.invalid') }).int(t('form.partyId.invalid')).min(1, t('form.partyId.invalid')),
    currency: z.string().trim().toUpperCase().regex(CURRENCY, t('form.currency.invalid')),
    estimatedAmount: z.coerce
      .number({ error: t('form.estimatedAmount.invalid') })
      .min(0, t('form.estimatedAmount.invalid'))
      .refine(hasAtMostTwoDecimals, t('form.estimatedAmount.decimals')),
  })
}
export type CreateOpportunityValues = z.infer<ReturnType<typeof createOpportunityFormSchema>>
export type CreateOpportunityInput = z.input<ReturnType<typeof createOpportunityFormSchema>>

export function addLineFormSchema(t: TFunction<'opportunities'>) {
  return z.object({
    productId: z.coerce.number({ error: t('lines.add.productId.invalid') }).int(t('lines.add.productId.invalid')).min(1, t('lines.add.productId.invalid')),
    quantity: z.coerce.number({ error: t('lines.add.quantity.invalid') }).int(t('lines.add.quantity.invalid')).min(1, t('lines.add.quantity.invalid')),
    unitPrice: z.coerce
      .number({ error: t('lines.add.unitPrice.invalid') })
      .min(0, t('lines.add.unitPrice.invalid'))
      .refine(hasAtMostTwoDecimals, t('lines.add.unitPrice.decimals')),
    isOptional: z.boolean(),
  })
}
export type AddLineValues = z.infer<ReturnType<typeof addLineFormSchema>>
export type AddLineInput = z.input<ReturnType<typeof addLineFormSchema>>

export function reasonFormSchema(t: TFunction<'opportunities'>) {
  return z.object({ reason: z.string().trim().min(1, t('reason.required')) })
}
export type ReasonValues = z.infer<ReturnType<typeof reasonFormSchema>>

export function openFormSchema(t: TFunction<'opportunities'>) {
  return z.object({
    expiryDate: z
      .string()
      .min(1, t('open.expiry.required'))
      .refine((value) => endOfLocalDay(value).getTime() > Date.now(), t('open.expiry.future')),
  })
}
export type OpenValues = z.infer<ReturnType<typeof openFormSchema>>

/** `<input type="date">` yields `YYYY-MM-DD`; the expiry is the END of that local day, sent as an absolute instant. */
export function endOfLocalDay(isoDate: string): Date {
  const [year, month, day] = isoDate.split('-').map(Number)
  return new Date(year, month - 1, day, 23, 59, 59)
}
