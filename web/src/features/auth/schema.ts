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

const message = (key: string) => i18n.t(key, { ns: 'auth' })

/** Only "was something entered" and "do the two entries match": the length/content rules are the server's (shown via `/auth/config`). */
const requiredPassword = (key: string) => z.string().min(1, message(key))

export const forgotPasswordSchema = z.object({
  email: z.email(message('schema.emailInvalid')),
})
export type ForgotPasswordValues = z.infer<typeof forgotPasswordSchema>

export const resetPasswordSchema = z
  .object({
    newPassword: requiredPassword('schema.newPasswordRequired'),
    confirmPassword: z.string(),
  })
  .refine((v) => v.newPassword === v.confirmPassword, { path: ['confirmPassword'], message: message('schema.confirmMismatch') })
export type ResetPasswordValues = z.infer<typeof resetPasswordSchema>

export const changePasswordSchema = z
  .object({
    currentPassword: requiredPassword('schema.passwordRequired'),
    newPassword: requiredPassword('schema.newPasswordRequired'),
    confirmPassword: z.string(),
  })
  .refine((v) => v.newPassword === v.confirmPassword, { path: ['confirmPassword'], message: message('schema.confirmMismatch') })
export type ChangePasswordValues = z.infer<typeof changePasswordSchema>

export const registerSchema = z
  .object({
    displayName: z.string().trim().min(1, message('schema.nameRequired')).max(200),
    email: z.email(message('schema.emailInvalid')),
    password: requiredPassword('schema.newPasswordRequired'),
    confirmPassword: z.string(),
  })
  .refine((v) => v.password === v.confirmPassword, { path: ['confirmPassword'], message: message('schema.confirmMismatch') })
export type RegisterValues = z.infer<typeof registerSchema>

/**
 * One form, two situations: a new address chooses a name and a password (entered twice); an address that already
 * has an account only proves its existing password.
 */
export function acceptInviteSchema(accountHasCredential: boolean) {
  return z
    .object({
      displayName: z.string().max(200),
      password: requiredPassword(accountHasCredential ? 'schema.passwordRequired' : 'schema.newPasswordRequired'),
      confirmPassword: z.string(),
    })
    .superRefine((v, ctx) => {
      if (accountHasCredential) return
      if (!v.displayName.trim()) ctx.addIssue({ code: 'custom', path: ['displayName'], message: message('schema.nameRequired') })
      if (v.password !== v.confirmPassword) ctx.addIssue({ code: 'custom', path: ['confirmPassword'], message: message('schema.confirmMismatch') })
    })
}
export type AcceptInviteValues = z.infer<ReturnType<typeof acceptInviteSchema>>
