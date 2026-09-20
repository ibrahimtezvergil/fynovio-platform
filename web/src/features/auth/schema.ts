import { z } from 'zod'
import { i18n } from '@/lib/i18n'

/**
 * Sign-in only checks that something was entered. The password policy is the
 * server's to enforce (and to change) — a client-side length rule here would
 * only ever drift from it.
 */
export const loginSchema = z.object({
  email: z.email(i18n.t('schema.emailInvalid', { ns: 'auth' })),
  password: z.string().min(1, i18n.t('schema.passwordRequired', { ns: 'auth' })),
})

export type LoginValues = z.infer<typeof loginSchema>
