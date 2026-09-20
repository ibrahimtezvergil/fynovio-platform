import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2 } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { useAuthConfig, useChangePassword } from '@/features/auth/api'
import { violationMessages } from '@/features/auth/lib/passwordPolicy'
import { changePasswordSchema, type ChangePasswordValues } from '@/features/auth/schema'
import type { ApiError } from '@/types'
import { commonErrorMessage } from './errorMessages'
import { PasswordField } from './PasswordField'
import { useSingleFlight } from './useSingleFlight'

const EMPTY: ChangePasswordValues = { currentPassword: '', newPassword: '', confirmPassword: '' }

/** Signed-in password change. Stays on the page: the current session survives, every other one is ended by the server. */
export function ChangePasswordForm() {
  const { t } = useTranslation('auth')
  const change = useChangePassword()
  const singleFlight = useSingleFlight()
  const { policy } = useAuthConfig()
  const {
    control,
    handleSubmit,
    setError,
    reset,
    formState: { errors },
  } = useForm<ChangePasswordValues>({ resolver: zodResolver(changePasswordSchema), defaultValues: EMPTY })

  const onSubmit = (values: ChangePasswordValues) =>
    singleFlight(async () => {
      change.reset()
      try {
        await change.mutateAsync({ currentPassword: values.currentPassword, newPassword: values.newPassword })
        reset(EMPTY) // never leave passwords sitting in the form
      } catch (error) {
        const apiError = error as ApiError
        if (apiError.code === 'invalid_current_password') setError('currentPassword', { message: t('accountSecurity.currentWrong') })
        else if (apiError.code === 'password_policy_violation') setError('newPassword', { message: violationMessages(t, apiError.violations, policy) })
        else setError('root', { message: commonErrorMessage(t, apiError) })
      }
    })

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
      <PasswordField
        control={control}
        name="currentPassword"
        label={t('passwordFields.currentPassword')}
        error={errors.currentPassword?.message}
        autoComplete="current-password"
      />
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
      {change.isSuccess && (
        <output className="block text-[12.5px] text-[var(--nx-pos)]">{t('accountSecurity.changed')}</output>
      )}

      <Button type="submit" disabled={change.isPending} size="lg" className="w-full sm:w-auto sm:self-start">
        {change.isPending && <Loader2 aria-hidden className="size-4 animate-spin" />}
        {change.isPending ? t('accountSecurity.submitting') : t('accountSecurity.submit')}
      </Button>
    </form>
  )
}
