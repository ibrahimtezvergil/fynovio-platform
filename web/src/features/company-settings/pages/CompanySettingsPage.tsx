import { zodResolver } from '@hookform/resolvers/zod'
import { Building2, Check, Landmark, LoaderCircle, Mail, Phone, RotateCcw } from 'lucide-react'
import { useEffect } from 'react'
import { FormProvider, useForm, useFormContext } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { PageHeader } from '@/components/common/PageHeader'
import { Field } from '@/components/common/Field'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { useAttemptKeys } from '@/lib/mutations/attemptKey'
import type { ApiError } from '@/types'
import { useCompanySettings, useUpdateCompanySettings } from '../api'
import {
  COMPANY_CURRENCY_VALUES,
  COMPANY_TIMEZONE_VALUES,
  companySettingsFormSchema,
  settingsToFormValues,
  type CompanySettingsFormValues,
} from '../schema'

function SectionHeading({ icon: Icon, title, description }: { icon: typeof Building2; title: string; description: string }) {
  return (
    <div className="flex items-start gap-3">
      <span className="nx-material flex size-9 shrink-0 items-center justify-center rounded-[12px] text-muted-foreground">
        <Icon aria-hidden className="size-4" strokeWidth={1.8} />
      </span>
      <div>
        <h2 className="text-[15.5px] font-[620] tracking-[-0.018em]">{title}</h2>
        <p className="text-muted-foreground mt-0.5 text-[12.5px] leading-5">{description}</p>
      </div>
    </div>
  )
}

function ProfileFields() {
  const { t } = useTranslation('company-settings')
  const { register, formState: { errors } } = useFormContext<CompanySettingsFormValues>()
  return (
    <Card className="gap-5 px-6 pt-[22px] pb-6">
      <SectionHeading icon={Building2} title={t('identity.title')} description={t('identity.description')} />
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field label={t('identity.displayName')} error={errors.displayName?.message}>
          {(props) => <Input {...props} autoComplete="organization" {...register('displayName')} />}
        </Field>
        <Field label={t('identity.legalName')} hint={t('identity.legalNameHint')} error={errors.legalName?.message}>
          {(props) => <Input {...props} autoComplete="organization" {...register('legalName', { setValueAs: emptyToNull })} />}
        </Field>
      </div>
    </Card>
  )
}

function LegalFields() {
  const { t } = useTranslation('company-settings')
  const { register, formState: { errors } } = useFormContext<CompanySettingsFormValues>()
  return (
    <Card className="gap-5 px-6 pt-[22px] pb-6">
      <SectionHeading icon={Landmark} title={t('legal.title')} description={t('legal.description')} />
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field label={t('legal.taxNumber')} error={errors.taxNumber?.message}>
          {(props) => <Input {...props} {...register('taxNumber', { setValueAs: emptyToNull })} />}
        </Field>
        <Field label={t('legal.taxOffice')} error={errors.taxOffice?.message}>
          {(props) => <Input {...props} {...register('taxOffice', { setValueAs: emptyToNull })} />}
        </Field>
      </div>
    </Card>
  )
}

function ContactFields() {
  const { t } = useTranslation('company-settings')
  const { register, formState: { errors } } = useFormContext<CompanySettingsFormValues>()
  return (
    <Card className="gap-5 px-6 pt-[22px] pb-6">
      <SectionHeading icon={Mail} title={t('contact.title')} description={t('contact.description')} />
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field label={t('contact.email')} error={errors.email?.message}>
          {(props) => <Input {...props} type="email" autoComplete="email" {...register('email', { setValueAs: emptyToNull })} />}
        </Field>
        <Field label={t('contact.phone')} error={errors.phone?.message}>
          {(props) => <Input {...props} type="tel" autoComplete="tel" {...register('phone', { setValueAs: emptyToNull })} />}
        </Field>
        <Field label={t('contact.address')} error={errors.address?.message} className="sm:col-span-2">
          {(props) => <Textarea {...props} autoComplete="street-address" {...register('address', { setValueAs: emptyToNull })} />}
        </Field>
        <Field label={t('contact.timezone')} error={errors.timezone?.message}>
          {(props) => (
            <Select {...props} {...register('timezone')}>
              {COMPANY_TIMEZONE_VALUES.map((zone) => <option key={zone} value={zone}>{t(`timezone.${zone}`)}</option>)}
            </Select>
          )}
        </Field>
        <Field label={t('contact.currency')} error={errors.currencyCode?.message}>
          {(props) => (
            <Select {...props} {...register('currencyCode')}>
              {COMPANY_CURRENCY_VALUES.map((currency) => <option key={currency} value={currency}>{t(`currency.${currency}`)}</option>)}
            </Select>
          )}
        </Field>
      </div>
    </Card>
  )
}

const emptyToNull = (value: string) => value === '' ? null : value

function SaveBar({ onSave, isUpdating }: { onSave: (values: CompanySettingsFormValues) => Promise<void>; isUpdating: boolean }) {
  const { t } = useTranslation('company-settings')
  const { handleSubmit, reset, formState: { isDirty } } = useFormContext<CompanySettingsFormValues>()
  return (
    <form onSubmit={handleSubmit(onSave)} className="nx-material sticky bottom-4 z-[4] flex flex-wrap items-center gap-3 rounded-[var(--nx-r-card)] px-5 py-3.5">
      <span aria-live="polite" className="text-muted-foreground flex-1 text-[12.5px]">
        {isDirty ? t('saveBar.unsaved') : t('saveBar.allSaved')}
      </span>
      <Button type="button" variant="ghost" disabled={!isDirty || isUpdating} onClick={() => reset()}>
        <RotateCcw aria-hidden strokeWidth={1.8} />
        {t('saveBar.discard')}
      </Button>
      <Button type="submit" disabled={!isDirty || isUpdating}>
        {isUpdating ? <LoaderCircle aria-hidden className="animate-spin" strokeWidth={1.8} /> : <Check aria-hidden strokeWidth={2} />}
        {t('saveBar.save')}
      </Button>
    </form>
  )
}

function companyProblem(error: ApiError, t: (key: string) => string) {
  if (error.status === 403) return { title: t('problem.forbiddenTitle'), description: t('problem.forbiddenDescription') }
  if (error.status === 404) return { title: t('problem.notFoundTitle'), description: t('problem.notFoundDescription') }
  return { title: t('problem.genericTitle'), description: error.message }
}

export default function CompanySettingsPage() {
  const { t } = useTranslation('company-settings')
  const profile = useCompanySettings()
  const update = useUpdateCompanySettings()
  const keys = useAttemptKeys()
  const form = useForm<CompanySettingsFormValues>({ resolver: zodResolver(companySettingsFormSchema), mode: 'onBlur' })

  useEffect(() => {
    if (profile.data) form.reset(settingsToFormValues(profile.data))
  }, [form, profile.data])

  const save = async (values: CompanySettingsFormValues) => {
    if (!profile.data) return
    const key = keys.begin({ values, expectedVersion: profile.data.rowVersion })
    try {
      const result = await update.mutateAsync({ values, expectedVersion: profile.data.rowVersion, idempotencyKey: key })
      keys.settle(null)
      form.reset(settingsToFormValues(result.settings))
    } catch (error) {
      const apiError = error as unknown as ApiError
      keys.settle(apiError)
      if (apiError.status === 409) {
        await profile.refetch()
        form.setError('root', { message: t('problem.conflict') })
      }
    }
  }

  if (profile.isPending) {
    return <div className="mx-auto w-full max-w-[960px] animate-pulse space-y-5"><div className="h-16 w-80 rounded-[var(--nx-r-card)] bg-muted" /><div className="h-72 rounded-[var(--nx-r-card)] bg-muted" /></div>
  }
  if (profile.isError) {
    const message = companyProblem(profile.error as unknown as ApiError, t)
    return (
      <div className="mx-auto flex w-full max-w-[960px] flex-col gap-5">
        <PageHeader title={t('page.title')} description={t('page.description')} />
        <Alert variant="destructive"><Phone aria-hidden /><AlertTitle>{message.title}</AlertTitle><AlertDescription>{message.description}</AlertDescription></Alert>
      </div>
    )
  }

  return (
    <div className="mx-auto flex w-full max-w-[960px] flex-col gap-5">
      <PageHeader title={t('page.title')} description={t('page.description')} eyebrow={t('page.eyebrow')} />
      <FormProvider {...form}>
        <div className="flex flex-col gap-4">
          <ProfileFields />
          <LegalFields />
          <ContactFields />
          {form.formState.errors.root?.message && <Alert variant="destructive"><AlertTitle>{t('problem.conflictTitle')}</AlertTitle><AlertDescription>{form.formState.errors.root.message}</AlertDescription></Alert>}
          <SaveBar onSave={save} isUpdating={update.isPending} />
        </div>
      </FormProvider>
    </div>
  )
}
