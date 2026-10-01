import type { TFunction } from 'i18next'
import { z } from 'zod'
import { customFieldValuesSchema } from '@/lib/custom-fields/schema'
import { pipelineStageKindSchema, type PipelineStageKind } from '@/types/schemas'

export type { PipelineStageKind }

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
  assignedPrincipalDisplayName: z.string().nullish(),
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
  isArchived: z.boolean().optional(),
  archivedAt: z.string().nullish(),
  createdAt: z.string().nullish(),
  updatedAt: z.string().nullish(),
  /** The concurrency token. Kept for mutation requests; never rendered. */
  rowVersion: z.number(),
  lines: z.array(opportunityLineSchema).default([]),
  /** Stored custom field object, including values of deprecated fields (shown read-only). */
  customFields: customFieldValuesSchema.nullish(),
})
export type Opportunity = z.infer<typeof opportunitySchema>

export const opportunitySummarySchema = z.object({
  id: z.number(),
  status: statusSchema,
  estimatedAmount: z.number().nullish(),
  currency: z.string().nullish(),
  assignedPrincipalIssuer: z.string().nullish(),
  assignedPrincipalSubject: z.string().nullish(),
  assignedPrincipalDisplayName: z.string().nullish(),
  pipelineStageId: z.number().nullish(),
  partyId: z.number().nullish(),
  /** With `pipelineStageId` this names the stage: stage ids are only meaningful inside their pipeline version. */
  pipelineDefinitionVersionId: z.number().nullish(),
  expiryDate: z.string().nullish(),
  createdAt: z.string().nullish(),
  updatedAt: z.string().nullish(),
  totalAmount: z.number().nullish(),
  needs: z.array(z.string()).nullish(),
  isArchived: z.boolean().optional(),
  archivedAt: z.string().nullish(),
  rowVersion: z.number().optional(),
  customFields: customFieldValuesSchema.nullish(),
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
  canArchive: z.boolean().optional(),
  canRestore: z.boolean().optional(),
})
export type AvailableActions = z.infer<typeof availableActionsSchema>

export const pipelineStageSchema = z.object({
  id: z.number(),
  name: z.string(),
  sortOrder: z.number(),
  isActive: z.boolean(),
  isEntry: z.boolean(),
  isArchived: z.boolean().default(false),
  /** Won/Lost stages are reached only through the win/lose commands, never through a stage change. */
  kind: pipelineStageKindSchema,
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
  phone: z.string().nullish(),
})
export type PartyReference = z.infer<typeof partyReferenceSchema>

// The command results carry only what the caller needs; the canonical state is refetched after every command.
export const createPartyResultSchema = z.object({ id: z.number(), replayed: z.boolean() })
export const createResultSchema = z.object({ opportunityId: z.number(), replayed: z.boolean() })
/**
 * One row of the activity timeline (`GET /opportunities/{id}/activity`): a published fact, labelled by the server at
 * read time. `kind` stays an open string — a fact kind this build does not know is shown generically, never dropped.
 */
export const opportunityActivitySchema = z.object({
  eventId: z.string(),
  kind: z.string(),
  occurredAt: z.string(),
  version: z.number(),
  stageName: z.string().nullish(),
  fromStageName: z.string().nullish(),
  principalName: z.string().nullish(),
  amount: z.number().nullish(),
  currency: z.string().nullish(),
  lostReason: z.string().nullish(),
  changedFields: z.array(z.string()).nullish(),
})
export type OpportunityActivity = z.infer<typeof opportunityActivitySchema>

export const commandResultSchema = z.object({ opportunityId: z.number(), replayed: z.boolean().optional() }).loose()

// ---- form input schemas (immediate UX only; the backend stays authoritative) ----

const hasAtMostTwoDecimals = (value: number) => Math.abs(Math.round(value * 100) - value * 100) < 1e-6

/**
 * Money fields accept the two decimal forms Turkish keyboards commonly produce: `1250,50` and `1.250,50`.
 * A bare dot remains accepted for pasted/API-style values; mixed thousands/decimal conventions are rejected
 * instead of silently changing the amount.
 */
export function parseLocalizedAmount(value: unknown): unknown {
  if (typeof value !== 'string') return value
  const input = value.trim()
  if (input === '') return Number.NaN

  if (input.includes(',')) {
    if (!/^-?(?:\d{1,3}(?:\.\d{3})*|\d+)(?:,\d+)?$/.test(input)) return Number.NaN
    return Number(input.replaceAll('.', '').replace(',', '.'))
  }

  return /^-?\d+(?:\.\d+)?$/.test(input) ? Number(input) : Number.NaN
}

const localizedMoney = (invalid: string, decimals: string) =>
  z.preprocess(
    parseLocalizedAmount,
    z.number({ error: invalid }).min(0, invalid).refine(hasAtMostTwoDecimals, decimals),
  )

export function createOpportunityFormSchema(t: TFunction<'opportunities'>) {
  return z.object({
    partyId: z.coerce.number({ error: t('form.partyId.invalid') }).int(t('form.partyId.invalid')).min(1, t('form.partyId.invalid')),
    currency: z.string().trim().toUpperCase().pipe(z.enum(['TRY', 'USD', 'EUR'], { error: t('form.currency.invalid') })),
    estimatedAmount: localizedMoney(t('form.estimatedAmount.invalid'), t('form.estimatedAmount.decimals')),
  })
}
export type CreateOpportunityValues = z.infer<ReturnType<typeof createOpportunityFormSchema>>
export type CreateOpportunityInput = z.input<ReturnType<typeof createOpportunityFormSchema>>

export function newPartyFormSchema(t: TFunction<'opportunities'>) {
  return z.object({
    partyType: z.enum(['Person', 'Organization']),
    name: z.string().trim().min(1, t('newParty.name.required')).max(200, t('newParty.name.tooLong')),
    surname: z.string().trim().max(200, t('newParty.name.tooLong')),
    phone: z.string().trim().max(50, t('newParty.phone.tooLong')),
    email: z.string().trim().max(320, t('newParty.email.tooLong')).refine((value) => value === '' || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value), t('newParty.email.invalid')),
  })
}
export type NewPartyValues = z.infer<ReturnType<typeof newPartyFormSchema>>

export function addLineFormSchema(t: TFunction<'opportunities'>) {
  return z.object({
    productId: z.coerce.number({ error: t('lines.add.productId.invalid') }).int(t('lines.add.productId.invalid')).min(1, t('lines.add.productId.invalid')),
    quantity: z.coerce.number({ error: t('lines.add.quantity.invalid') }).int(t('lines.add.quantity.invalid')).min(1, t('lines.add.quantity.invalid')),
    unitPrice: localizedMoney(t('lines.add.unitPrice.invalid'), t('lines.add.unitPrice.decimals')),
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
