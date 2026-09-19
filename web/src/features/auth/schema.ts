import { z } from 'zod'
import { i18n } from '@/lib/i18n'

export const loginSchema = z.object({
  email: z.email(i18n.t('schema.emailInvalid', { ns: 'auth' })),
  password: z.string().min(8, i18n.t('schema.passwordMin', { ns: 'auth' })),
})

export type LoginValues = z.infer<typeof loginSchema>
