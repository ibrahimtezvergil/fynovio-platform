import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2 } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Field } from '@/components/common/Field'
import { Button, buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { useForgotPassword } from '@/features/auth/api'
import { forgotPasswordSchema, type ForgotPasswordValues } from '@/features/auth/schema'
import { paths } from '@/routes/paths'
import type { ApiError } from '@/types'
import { commonErrorMessage } from './errorMessages'
import { useSingleFlight } from './useSingleFlight'

/**
 * Whatever the address, the answer is the same: the server replies 202 for known, unknown and throttled
 * addresses alike, and this screen must not add a difference of its own.
 */
export function ForgotPasswordForm() {
  const { t } = useTranslation('auth')
  const forgot = useForgotPassword()
  const singleFlight = useSingleFlight()
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<ForgotPasswordValues>({ resolver: zodResolver(forgotPasswordSchema), defaultValues: { email: '' } })

  if (forgot.isSuccess) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>{t('forgotPassword.sentTitle')}</CardTitle>
          <CardDescription>
            <output>{t('forgotPassword.sentDescription')}</output>
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Link to={paths.login} className={buttonVariants({ variant: 'outline', size: 'lg', className: 'w-full' })}>
            {t('forgotPassword.backToLogin')}
          </Link>
        </CardContent>
      </Card>
    )
  }

  const onSubmit = (values: ForgotPasswordValues) =>
    singleFlight(async () => {
      try {
        await forgot.mutateAsync(values)
      } catch (error) {
        setError('root', { message: commonErrorMessage(t, error as ApiError) })
      }
    })

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('forgotPassword.title')}</CardTitle>
        <CardDescription>{t('forgotPassword.description')}</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
          <Field label={t('forgotPassword.emailLabel')} error={errors.email?.message}>
            {(props) => (
              <Input {...props} type="email" autoComplete="username" autoCapitalize="none" spellCheck={false} {...register('email')} />
            )}
          </Field>

          {errors.root && (
            <p role="alert" className="text-destructive text-[12px]">
              {errors.root.message}
            </p>
          )}

          <Button type="submit" disabled={forgot.isPending} size="lg" className="w-full">
            {forgot.isPending && <Loader2 aria-hidden className="size-4 animate-spin" />}
            {forgot.isPending ? t('forgotPassword.submitting') : t('forgotPassword.submit')}
          </Button>
          <Link to={paths.login} className="text-muted-foreground hover:text-foreground text-center text-[12.5px]">
            {t('forgotPassword.backToLogin')}
          </Link>
        </form>
      </CardContent>
    </Card>
  )
}
