import { zodResolver } from '@hookform/resolvers/zod'
import { ShieldCheck } from 'lucide-react'
import { useId } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { Field } from '@/components/common/Field'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { SectionHeading } from '@/features/settings/components/SectionHeading'
import { passwordSchema, type PasswordValues } from '@/features/settings/schema'

/**
 * A form of its own, on purpose: a password change is a transaction, not a
 * preference. It never counts towards the settings diff, and "sign out other
 * devices" travels with it because that is the action it qualifies.
 */
export function SecuritySection() {
  const { t } = useTranslation('settings')
  const signOutId = useId()

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<PasswordValues>({
    resolver: zodResolver(passwordSchema),
    defaultValues: { currentPassword: '', newPassword: '', signOutOtherDevices: true },
    mode: 'onBlur',
  })

  const onSubmit = handleSubmit(() => {
    toast.error(t('security.noBackendYet'))
    reset()
  })

  return (
    <Card id="guvenlik" className="scroll-mt-24 gap-4 px-6 pt-[22px] pb-6">
      <SectionHeading title={t('security.title')} description={t('security.description')} />

      <div className="flex flex-wrap items-center gap-3.5 rounded-[var(--nx-r-ctl)] bg-[var(--nx-st-green-bg)] px-4 py-3.5">
        <ShieldCheck
          aria-hidden
          strokeWidth={1.7}
          className="size-5 shrink-0 text-[var(--nx-st-green-fg)]"
        />
        <span className="flex min-w-0 flex-1 flex-col gap-0.5 text-[var(--nx-st-green-fg)]">
          <span className="text-[13px] font-[590]">{t('security.twoFactorOn')}</span>
          <span className="text-[12px] opacity-85">{t('security.twoFactorSince')}</span>
        </span>
      </div>

      <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field label={t('security.currentPassword')} error={errors.currentPassword?.message}>
            {(props) => (
              <Input
                {...props}
                type="password"
                autoComplete="current-password"
                {...register('currentPassword')}
              />
            )}
          </Field>
          <Field label={t('security.newPassword')} error={errors.newPassword?.message}>
            {(props) => (
              <Input
                {...props}
                type="password"
                autoComplete="new-password"
                {...register('newPassword')}
              />
            )}
          </Field>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <div className="flex flex-1 items-center gap-2.5">
            <Checkbox id={signOutId} {...register('signOutOtherDevices')} />
            <label htmlFor={signOutId} className="text-muted-foreground cursor-pointer text-[13px]">
              {t('security.signOutOtherDevices')}
            </label>
          </div>
          <Button type="submit" variant="secondary" disabled={isSubmitting}>
            {t('security.updatePassword')}
          </Button>
        </div>
      </form>
    </Card>
  )
}
