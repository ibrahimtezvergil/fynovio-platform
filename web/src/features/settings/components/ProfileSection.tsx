import { useFormContext, useWatch } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { SectionHeading } from '@/features/settings/components/SectionHeading'
import type { SettingsValues } from '@/features/settings/schema'
import { initialsOf } from '@/lib/utils'

export function ProfileSection() {
  const { t } = useTranslation('settings')
  const {
    register,
    control,
    formState: { errors },
  } = useFormContext<SettingsValues>()

  // Only the avatar depends on the live name, so only it subscribes to it.
  const name = useWatch({ control, name: 'name' })

  return (
    <Card id="profil" className="scroll-mt-24 gap-5 px-6 pt-[22px] pb-6">
      <SectionHeading title={t('profile.title')} description={t('profile.description')} />

      <div className="flex items-center gap-4">
        <span
          aria-hidden
          className="nx-avatar size-16 rounded-full text-[21px] shadow-[inset_0_1px_0_var(--nx-specular)]"
        >
          {initialsOf(name)}
        </span>
        <div className="flex flex-col gap-2">
          <div className="flex gap-2">
            <Button variant="secondary" size="sm">
              {t('profile.uploadPhoto')}
            </Button>
            <Button variant="ghost" size="sm">
              {t('profile.removePhoto')}
            </Button>
          </div>
          <span className="text-muted-foreground text-[11.5px]">{t('profile.photoHint')}</span>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field label={t('profile.fullName')} error={errors.name?.message}>
          {(props) => <Input {...props} autoComplete="name" {...register('name')} />}
        </Field>
        <Field label={t('profile.jobTitle')} error={errors.jobTitle?.message}>
          {(props) => <Input {...props} autoComplete="organization-title" {...register('jobTitle')} />}
        </Field>
        <Field label={t('profile.email')} error={errors.email?.message}>
          {(props) => <Input {...props} type="email" autoComplete="email" {...register('email')} />}
        </Field>
        <Field label={t('profile.phone')} error={errors.phone?.message}>
          {(props) => (
            <Input {...props} type="tel" autoComplete="tel" className="tnum" {...register('phone')} />
          )}
        </Field>
      </div>
    </Card>
  )
}
