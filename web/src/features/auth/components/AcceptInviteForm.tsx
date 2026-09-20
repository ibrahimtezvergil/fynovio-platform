import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2 } from 'lucide-react'
import { useMemo } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Field } from '@/components/common/Field'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useAcceptInvitation, useAuthConfig, type InvitationPreview } from '@/features/auth/api'
import { violationMessages } from '@/features/auth/lib/passwordPolicy'
import { acceptInviteSchema, type AcceptInviteValues } from '@/features/auth/schema'
import { paths } from '@/routes/paths'
import type { ApiError } from '@/types'
import { commonErrorMessage } from './errorMessages'
import { PasswordField } from './PasswordField'
import { useSingleFlight } from './useSingleFlight'

/**
 * Accepting ends with a new session (the same as signing in). Where that session goes next — dashboard, tenant
 * selection — is `PublicOnlyRoute`'s decision, so this only lands on /login and lets the guard route it.
 */
export function AcceptInviteForm({
  token,
  preview,
  onLinkInvalid,
}: {
  token: string
  preview: InvitationPreview
  onLinkInvalid: () => void
}) {
  const { t } = useTranslation('auth')
  const navigate = useNavigate()
  const accept = useAcceptInvitation()
  const singleFlight = useSingleFlight()
  const { policy } = useAuthConfig()
  const existing = preview.accountHasCredential
  const schema = useMemo(() => acceptInviteSchema(existing), [existing])
  const {
    control,
    register,
    handleSubmit,
    setError,
    setValue,
    formState: { errors },
  } = useForm<AcceptInviteValues>({
    resolver: zodResolver(schema),
    defaultValues: { displayName: preview.displayName ?? '', password: '', confirmPassword: '' },
  })

  const onSubmit = (values: AcceptInviteValues) =>
    singleFlight(async () => {
      try {
        await accept.mutateAsync({ token, password: values.password, ...(existing ? {} : { displayName: values.displayName.trim() }) })
        navigate(paths.login, { replace: true })
      } catch (error) {
        const apiError = error as ApiError
        if (apiError.code === 'invalid_or_expired_token') return onLinkInvalid()
        setValue('password', '')
        setValue('confirmPassword', '')
        if (apiError.code === 'invalid_credentials') setError('password', { message: t('acceptInvite.wrongPassword') })
        else if (apiError.code === 'password_policy_violation') setError('password', { message: violationMessages(t, apiError.violations, policy) })
        else setError('root', { message: commonErrorMessage(t, apiError) })
      }
    })

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
      <p className="text-muted-foreground text-[12.5px]">{t('acceptInvite.forAddress', { email: preview.email })}</p>

      {existing ? (
        <PasswordField
          control={control}
          name="password"
          label={t('acceptInvite.existingPasswordLabel')}
          error={errors.password?.message}
          autoComplete="current-password"
        />
      ) : (
        <>
          <Field label={t('acceptInvite.nameLabel')} error={errors.displayName?.message}>
            {(props) => <Input {...props} type="text" autoComplete="name" {...register('displayName')} />}
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
        </>
      )}

      {errors.root && (
        <p role="alert" className="text-destructive text-[12px]">
          {errors.root.message}
        </p>
      )}

      <Button type="submit" disabled={accept.isPending} size="lg" className="w-full">
        {accept.isPending && <Loader2 aria-hidden className="size-4 animate-spin" />}
        {accept.isPending ? t('acceptInvite.submitting') : t('acceptInvite.submit')}
      </Button>
    </form>
  )
}
