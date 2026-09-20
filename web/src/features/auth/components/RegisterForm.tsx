import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2 } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Field } from '@/components/common/Field'
import { Button, buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { useAuthConfig, useRegister } from '@/features/auth/api'
import { violationMessages } from '@/features/auth/lib/passwordPolicy'
import { registerSchema, type RegisterValues } from '@/features/auth/schema'
import { paths } from '@/routes/paths'
import type { ApiError } from '@/types'
import { commonErrorMessage } from './errorMessages'
import { PasswordField } from './PasswordField'
import { useSingleFlight } from './useSingleFlight'

/** Only rendered when the server says self-registration is on. A new account has no tenant access until it is invited. */
export function RegisterForm() {
  const { t } = useTranslation('auth')
  const registration = useRegister()
  const singleFlight = useSingleFlight()
  const { policy } = useAuthConfig()
  const {
    control,
    register,
    handleSubmit,
    setError,
    setValue,
    formState: { errors },
  } = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { displayName: '', email: '', password: '', confirmPassword: '' },
  })

  if (registration.isSuccess) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>{t('register.sentTitle')}</CardTitle>
          <CardDescription>
            <output>{t('register.sentDescription')}</output>
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Link to={paths.login} className={buttonVariants({ size: 'lg', className: 'w-full' })}>
            {t('register.backToLogin')}
          </Link>
        </CardContent>
      </Card>
    )
  }

  const onSubmit = (values: RegisterValues) =>
    singleFlight(async () => {
      try {
        await registration.mutateAsync({ email: values.email, displayName: values.displayName.trim(), password: values.password })
      } catch (error) {
        const apiError = error as ApiError
        setValue('password', '')
        setValue('confirmPassword', '')
        if (apiError.code === 'password_policy_violation') setError('password', { message: violationMessages(t, apiError.violations, policy) })
        else setError('root', { message: commonErrorMessage(t, apiError) })
      }
    })

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('register.title')}</CardTitle>
        <CardDescription>{t('register.description')}</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
          <Field label={t('register.nameLabel')} error={errors.displayName?.message}>
            {(props) => <Input {...props} type="text" autoComplete="name" {...register('displayName')} />}
          </Field>
          <Field label={t('register.emailLabel')} error={errors.email?.message}>
            {(props) => (
              <Input {...props} type="email" autoComplete="username" autoCapitalize="none" spellCheck={false} {...register('email')} />
            )}
          </Field>
          <PasswordField
            control={control}
            name="password"
            label={t('passwordFields.newPassword')}
            hint={t('passwordPolicy.hint', { min: policy.minLength })}
            error={errors.password?.message}
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

          <Button type="submit" disabled={registration.isPending} size="lg" className="w-full">
            {registration.isPending && <Loader2 aria-hidden className="size-4 animate-spin" />}
            {registration.isPending ? t('register.submitting') : t('register.submit')}
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}
