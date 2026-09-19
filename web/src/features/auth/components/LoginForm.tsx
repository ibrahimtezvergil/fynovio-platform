import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2 } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { useLocation, useNavigate } from 'react-router-dom'
import { Field } from '@/components/common/Field'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useLogin } from '@/features/auth/api'
import { loginSchema, type LoginValues } from '@/features/auth/schema'
import { paths } from '@/routes/paths'
import type { ApiError } from '@/types'

export function LoginForm() {
  const { t } = useTranslation('auth')
  const login = useLogin()
  const navigate = useNavigate()
  const location = useLocation()

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: 'deniz@fynovio.com', password: 'fynovio123' },
  })

  const onSubmit = async (values: LoginValues) => {
    try {
      await login.mutateAsync(values)
    } catch (error) {
      // The axios interceptor normalises every failure into `ApiError`, so the
      // form can surface the server's own message instead of a generic one.
      setError('root', { message: (error as ApiError).message ?? t('loginForm.genericError') })
      return
    }
    // Send people back where they were headed before the redirect.
    const from = (location.state as { from?: string } | null)?.from ?? paths.dashboard
    navigate(from, { replace: true })
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
      <Field label={t('loginForm.emailLabel')} error={errors.email?.message}>
        {(props) => <Input {...props} type="email" autoComplete="email" {...register('email')} />}
      </Field>

      <Field label={t('loginForm.passwordLabel')} error={errors.password?.message}>
        {(props) => (
          <Input {...props} type="password" autoComplete="current-password" {...register('password')} />
        )}
      </Field>

      {errors.root && (
        <p role="alert" className="text-destructive text-[12px]">
          {errors.root.message}
        </p>
      )}

      <Button type="submit" disabled={isSubmitting} size="lg" className="mt-1 w-full">
        {isSubmitting && <Loader2 aria-hidden className="size-4 animate-spin" />}
        {isSubmitting ? t('loginForm.submitting') : t('loginForm.submit')}
      </Button>
    </form>
  )
}
