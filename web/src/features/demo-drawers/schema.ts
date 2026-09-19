import { z } from 'zod'
import { i18n } from '@/lib/i18n'
import { CUSTOMER_STATUSES } from '@/features/demo-drawers/data/customers'

const t = (key: string) => i18n.t(key, { ns: 'demo-drawers' })

/**
 * What the edit drawer owns. It lives beside the feature, not inside the
 * component, so the drawer, its dirty guard and any future server call all
 * validate against one definition.
 */
export const customerEditSchema = z.object({
  name: z.string().trim().min(2, t('schema.nameMin')).max(80, t('schema.nameMax')),
  owner: z.string().trim().min(2, t('schema.ownerRequired')),
  status: z.enum(CUSTOMER_STATUSES),
  paymentTerm: z.string().trim().min(2, t('schema.paymentTermRequired')),
  note: z.string().trim().max(400, t('schema.noteMax')),
})

export type CustomerEditValues = z.infer<typeof customerEditSchema>
