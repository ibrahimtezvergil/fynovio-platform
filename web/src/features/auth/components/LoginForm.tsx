import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2 } from 'lucide-react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import { PasswordInput } from '@/components/common/inputs/PasswordInput'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useLogin } from '@/features/auth/api'
import { loginSchema, type LoginValues } from '@/features/auth/schema'
import type { ApiError } from '@/types'
import { useSingleFlight } from './useSingleFlight'

/**
 * Only updates the session. Where the person goes next (dashboard, tenant
 * selection, no-access, the deep link they came from) is decided by
 * `PublicOnlyRoute` reacting to the new session state.
 */
export function LoginForm() {
  const { t } = useTranslation('auth')
  const login = useLogin()
  const singleFlight = useSingleFlight()

  const {
    control,
    register,
    handleSubmit,
    setError,
    setValue,
    formState: { errors },
  } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    // No defaults: a real sign-in never pre-fills credentials.
    defaultValues: { email: '', password: '' },
  })

  const messageFor = (error: ApiError): string => {
    if (error.status === 401) return t('loginForm.invalidCredentials') // never says which part was wrong
    if (error.status === 429) {
      return error.retryAfterSeconds
        ? t('loginForm.rateLimited', { seconds: error.retryAfterSeconds })
        : t('loginForm.rateLimitedNoWait')
    }
    return t('loginForm.genericError')
  }

  const onSubmit = (values: LoginValues) =>
    singleFlight(async () => {
      try {
        await login.mutateAsync(values)
      } catch (error) {
        setValue('password', '') // the email is kept; the password is never
        setError('root', { message: messageFor(error as ApiError) })
      }
    })

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
      <Field label={t('loginForm.emailLabel')} error={errors.email?.message}>
        {(props) => (
          <Input {...props} type="email" autoComplete="username" autoCapitalize="none" spellCheck={false} {...register('email')} />
        )}
      </Field>

      <Field label={t('loginForm.passwordLabel')} error={errors.password?.message}>
        {(props) => (
          <Controller
            control={control}
            name="password"
            render={({ field }) => (
              <PasswordInput {...props} value={field.value} onValueChange={field.onChange} autoComplete="current-password" />
            )}
          />
        )}
      </Field>

      {errors.root && (
        <p role="alert" className="text-destructive text-[12px]">
          {errors.root.message}
        </p>
      )}

      <Button type="submit" disabled={login.isPending} size="lg" className="mt-1 w-full">
        {login.isPending && <Loader2 aria-hidden className="size-4 animate-spin" />}
        {login.isPending ? t('loginForm.submitting') : t('loginForm.submit')}
      </Button>
    </form>
  )
}
