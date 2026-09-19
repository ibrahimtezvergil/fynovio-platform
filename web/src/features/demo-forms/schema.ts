import { z } from 'zod'
import { isPhoneComplete, isValidTaxId } from '@/components/common/inputs'
import { i18n } from '@/lib/i18n'

const t = (key: string) => i18n.t(key, { ns: 'demo-forms' })

/**
 * The validated half of the page: the same controls, wired through
 * `react-hook-form` with a zod schema beside the feature.
 *
 * Every field is nullable on the way in, because that is what an empty control
 * emits — the schema is where "empty" becomes "invalid", not the control.
 *
 * Validation messages resolve from the `demo-forms` catalog at module-load
 * time via the shared `i18n` instance (schemas are plain objects, not
 * components — no hook to re-run on a language change). See `stageMeta()` in
 * `StageBadge.tsx` for the same accepted tradeoff elsewhere.
 */
export const quoteSchema = z.object({
  customer: z
    .string()
    .nullable()
    .refine((value) => value !== null, t('schema.customerRequired')),
  taxId: z.string().refine(isValidTaxId, t('schema.taxId')),
  phone: z.string().refine(isPhoneComplete, t('schema.phone')),
  amount: z
    .number()
    .nullable()
    .refine((value) => value !== null && value > 0, t('schema.amount')),
  currency: z.enum(['TRY', 'USD', 'EUR', 'GBP']),
  discount: z.object({
    mode: z.enum(['percent', 'amount']),
    value: z.number().nullable(),
  }),
  closeDate: z
    .date()
    .nullable()
    .refine((value) => value !== null, t('schema.closeDate')),
  tags: z.array(z.string()).max(5, t('schema.tagsMax')),
  terms: z.boolean().refine((value) => value, t('schema.terms')),
})

/** What the controls hold while the form is being filled in. */
export type QuoteDraft = z.input<typeof quoteSchema>
