import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2 } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { useAuthConfig, useResetPassword } from '@/features/auth/api'
import { violationMessages } from '@/features/auth/lib/passwordPolicy'
import { resetPasswordSchema, type ResetPasswordValues } from '@/features/auth/schema'
import { paths } from '@/routes/paths'
import type { ApiError } from '@/types'
import { commonErrorMessage } from './errorMessages'
import { PasswordField } from './PasswordField'
import { useSingleFlight } from './useSingleFlight'

/** Shown on the login screen after a successful reset. */
export const PASSWORD_UPDATED_NOTICE = 'passwordUpdated'

export function ResetPasswordForm({ token, onLinkInvalid }: { token: string; onLinkInvalid: () => void }) {
  const { t } = useTranslation('auth')
  const navigate = useNavigate()
  const reset = useResetPassword()
  const singleFlight = useSingleFlight()
  const { policy } = useAuthConfig()
  const {
    control,
    handleSubmit,
    setError,
    setValue,
    formState: { errors },
  } = useForm<ResetPasswordValues>({ resolver: zodResolver(resetPasswordSchema), defaultValues: { newPassword: '', confirmPassword: '' } })

  const onSubmit = (values: ResetPasswordValues) =>
    singleFlight(async () => {
      try {
        await reset.mutateAsync({ token, newPassword: values.newPassword })
        navigate(paths.login, { replace: true, state: { notice: PASSWORD_UPDATED_NOTICE } })
      } catch (error) {
        const apiError = error as ApiError
        if (apiError.code === 'invalid_or_expired_token') return onLinkInvalid()
        setValue('newPassword', '')
        setValue('confirmPassword', '')
        if (apiError.code === 'password_policy_violation') {
          setError('newPassword', { message: violationMessages(t, apiError.violations, policy) })
        } else {
          setError('root', { message: commonErrorMessage(t, apiError) })
        }
      }
    })

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
      <PasswordField
        control={control}
        name="newPassword"
        label={t('passwordFields.newPassword')}
        hint={t('passwordPolicy.hint', { min: policy.minLength })}
        error={errors.newPassword?.message}
        autoComplete="new-password"
      />
      <PasswordField
        control={control}
        name="confirmPassword"
        label={t('passwordFields.confirmPassword')}
        error={errors.confirmPassword?.message}
        autoComplete="new-password"
      />

      {errors.root && (
        <p role="alert" className="text-destructive text-[12px]">
          {errors.root.message}
        </p>
      )}

      <Button type="submit" disabled={reset.isPending} size="lg" className="w-full">
        {reset.isPending && <Loader2 aria-hidden className="size-4 animate-spin" />}
        {reset.isPending ? t('resetPassword.submitting') : t('resetPassword.submit')}
      </Button>
    </form>
  )
}
