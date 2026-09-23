import { z } from 'zod'
import { i18n } from '@/lib/i18n'

export const COMPANY_TIMEZONE_VALUES = ['Europe/Istanbul', 'Europe/Berlin', 'Europe/London'] as const
export const COMPANY_CURRENCY_VALUES = ['TRY', 'EUR', 'USD'] as const

const t = (key: string, options?: Record<string, unknown>) => i18n.t(key, { ns: 'company-settings', ...options })
const isForbiddenSingleLineCharacter = (character: string) => {
  const code = character.charCodeAt(0)
  return character === '\r' || character === '\n' || character === '\u2028' || character === '\u2029' || code < 32 || code === 127
}
const isForbiddenAddressCharacter = (character: string) => {
  const code = character.charCodeAt(0)
  return (code < 32 && character !== '\r' && character !== '\n') || code === 127
}
const singleLine = (max: number) => z.string().trim().min(1, t('validation.required')).max(max, t('validation.maxChars', { max })).refine(
  (value) => !Array.from(value).some(isForbiddenSingleLineCharacter),
  t('validation.singleLine'),
)
const nullableSingleLine = (max: number) => z.string().trim().max(max, t('validation.maxChars', { max })).refine(
  (value) => !Array.from(value).some(isForbiddenSingleLineCharacter),
  t('validation.singleLine'),
).nullable()

/** Wire contract for the tenant profile. Nullable values are explicit so PUT remains a full replacement. */
export const companySettingsSchema = z.object({
  displayName: singleLine(120).min(2, t('validation.displayName')),
  legalName: nullableSingleLine(160),
  taxNumber: nullableSingleLine(32),
  taxOffice: nullableSingleLine(120),
  email: z.string().trim().email(t('validation.email')).max(254, t('validation.maxChars', { max: 254 })).transform((value) => value.toLowerCase()).nullable(),
  phone: nullableSingleLine(32),
  address: z.string().trim().max(500, t('validation.maxChars', { max: 500 })).refine(
    (value) => !Array.from(value).some(isForbiddenAddressCharacter),
    t('validation.address'),
  ).nullable(),
  timezone: z.enum(COMPANY_TIMEZONE_VALUES),
  currencyCode: z.enum(COMPANY_CURRENCY_VALUES),
  rowVersion: z.number().int().positive(),
})

export type CompanySettings = z.output<typeof companySettingsSchema>

/** Form-only shape: the server's row version is kept outside editable fields. */
export const companySettingsFormSchema = companySettingsSchema.omit({ rowVersion: true })
export type CompanySettingsFormValues = z.input<typeof companySettingsFormSchema>

export const updateCompanySettingsResultSchema = z.object({
  settings: companySettingsSchema,
  replayed: z.boolean(),
})

export function settingsToFormValues(settings: CompanySettings): CompanySettingsFormValues {
  const { rowVersion: _rowVersion, ...values } = settings
  return values
}

const roleAssignmentSchema = z.object({
  assignmentId: z.number().int().positive(),
  roleKey: z.string().min(1),
  roleName: z.string().min(1),
  canRevoke: z.boolean(),
})

export const companyAccessOverviewSchema = z.object({
  revision: z.number().int().nonnegative().default(0),
  canInvite: z.boolean(),
  canGrant: z.boolean(),
  canRevoke: z.boolean(),
  canManageRoles: z.boolean().default(false),
  availableActions: z.array(z.object({ key: z.string().min(1), ownerModule: z.string().min(1), resourceType: z.string().min(1) })).default([]),
  pendingInvitations: z.array(z.object({
    invitationId: z.string().uuid(),
    email: z.string().email(),
    displayName: z.string().nullable(),
    roleKey: z.string().nullable(),
    expiresAt: z.string().datetime({ offset: true }),
  })),
  members: z.array(z.object({
    principalIssuer: z.string().min(1),
    principalSubject: z.string().min(1),
    displayName: z.string().min(1),
    email: z.string().email(),
    status: z.enum(['invited', 'active', 'disabled']),
    assignments: z.array(roleAssignmentSchema),
  })),
  roles: z.array(z.object({
    key: z.string().min(1),
    name: z.string().min(1),
    origin: z.enum(['tenant', 'system_template']),
    canEdit: z.boolean().default(false),
    permissions: z.array(z.object({ actionKey: z.string().min(1), relation: z.string().nullable() })),
  })),
})

export type CompanyAccessOverview = z.output<typeof companyAccessOverviewSchema>
