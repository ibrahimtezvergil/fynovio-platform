import { zodResolver } from '@hookform/resolvers/zod'
import { Building2, Check, Landmark, LoaderCircle, Mail, Phone, RotateCcw, ShieldCheck, UserPlus, Users, X } from 'lucide-react'
import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { FormProvider, useForm, useFormContext } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { PageHeader } from '@/components/common/PageHeader'
import { Field } from '@/components/common/Field'
import { SectionNav, type NavSection } from '@/components/common/SectionNav'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { useAttemptKeys } from '@/lib/mutations/attemptKey'
import type { ApiError } from '@/types'
import { useCompanyAccess, useCompanySettings, useGrantCompanyRole, useInviteCompanyMember, useRevokeCompanyRole, useUpdateCompanySettings } from '../api'
import {
  COMPANY_CURRENCY_VALUES,
  COMPANY_TIMEZONE_VALUES,
  companySettingsFormSchema,
  settingsToFormValues,
  type CompanySettingsFormValues,
} from '../schema'

/** Matches the personal settings cards: their grouping is conveyed by the page navigation, not repeated icons. */
function SectionHeading({ title, description }: { title: string; description: string }) {
  return (
    <div className="flex min-w-0 flex-1 flex-col gap-0.5">
      <h2 className="font-heading text-[17px] leading-tight font-[620] tracking-[-0.024em]">{title}</h2>
      <p className="text-muted-foreground text-[12.5px]">{description}</p>
    </div>
  )
}

function ProfileFields() {
  const { t } = useTranslation('company-settings')
  const { register, formState: { errors } } = useFormContext<CompanySettingsFormValues>()
  return (
    <Card id="sirket-kimligi" className="scroll-mt-24 gap-5 px-6 pt-[22px] pb-6">
      <SectionHeading title={t('identity.title')} description={t('identity.description')} />
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
    <Card id="vergi-bilgileri" className="scroll-mt-24 gap-5 px-6 pt-[22px] pb-6">
      <SectionHeading title={t('legal.title')} description={t('legal.description')} />
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
    <Card id="iletisim-ve-varsayilanlar" className="scroll-mt-24 gap-5 px-6 pt-[22px] pb-6">
      <SectionHeading title={t('contact.title')} description={t('contact.description')} />
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

function AccessFields() {
  const { t, i18n } = useTranslation('company-settings')
  const access = useCompanyAccess()
  const invite = useInviteCompanyMember()
  const grant = useGrantCompanyRole()
  const revoke = useRevokeCompanyRole()
  const keys = useAttemptKeys()
  const [email, setEmail] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [roleKeys, setRoleKeys] = useState<Record<string, string>>({})

  const inviteMember = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    await invite.mutateAsync({ email, displayName: displayName || undefined, locale: i18n.language })
    setEmail('')
    setDisplayName('')
  }
  const grantRole = async (issuer: string, subject: string) => {
    const roleKey = roleKeys[`${issuer}/${subject}`]
    if (!roleKey) return
    const key = keys.begin({ issuer, subject, roleKey })
    try {
      await grant.mutateAsync({ principalIssuer: issuer, principalSubject: subject, roleKey, idempotencyKey: key })
      keys.settle(null)
      setRoleKeys((current) => ({ ...current, [`${issuer}/${subject}`]: '' }))
    } catch (error) {
      keys.settle(error as ApiError)
    }
  }
  const revokeRole = async (assignmentId: number) => {
    const key = keys.begin({ assignmentId })
    try {
      await revoke.mutateAsync({ assignmentId, idempotencyKey: key })
      keys.settle(null)
    } catch (error) {
      keys.settle(error as ApiError)
    }
  }

  return (
    <>
      <Card id="kullanicilar" className="scroll-mt-24 gap-5 px-6 pt-[22px] pb-6">
        <SectionHeading title={t('members.title')} description={t('members.description')} />
        <form onSubmit={inviteMember} className="grid grid-cols-1 items-end gap-3 sm:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_auto]">
          <Field label={t('members.email')}>{(props) => <Input {...props} value={email} type="email" required onChange={(event) => setEmail(event.target.value)} />}</Field>
          <Field label={t('members.displayName')}>{(props) => <Input {...props} value={displayName} onChange={(event) => setDisplayName(event.target.value)} />}</Field>
          <Button type="submit" disabled={invite.isPending}><UserPlus aria-hidden />{t('members.invite')}</Button>
        </form>
        {access.isError && <Alert variant="destructive"><AlertTitle>{t('accessProblem.title')}</AlertTitle><AlertDescription>{t('accessProblem.description')}</AlertDescription></Alert>}
        {access.isPending ? <div className="h-28 animate-pulse rounded-[var(--nx-r-card)] bg-muted" /> : (
          <div className="divide-y rounded-[var(--nx-r-card)] border">
            {access.data?.members.map((member) => {
              const memberKey = `${member.principalIssuer}/${member.principalSubject}`
              const availableRoles = access.data.roles.filter((role) => !member.assignments.some((assignment) => assignment.roleKey === role.key))
              return <div key={memberKey} className="flex flex-col gap-3 px-4 py-3.5 sm:flex-row sm:items-center">
                <div className="min-w-0 flex-1"><p className="truncate text-sm font-medium">{member.displayName}</p><p className="truncate text-xs text-muted-foreground">{member.email}</p></div>
                <div className="flex flex-wrap items-center gap-1.5">
                  {member.assignments.map((assignment) => <span key={assignment.assignmentId} className="inline-flex items-center gap-1 rounded-full bg-secondary px-2 py-1 text-xs">{assignment.roleName}<button type="button" aria-label={t('members.removeRole', { role: assignment.roleName })} onClick={() => void revokeRole(assignment.assignmentId)} disabled={revoke.isPending}><X className="size-3" /></button></span>)}
                  {member.status === 'active' && availableRoles.length > 0 && <><Select aria-label={t('members.selectRole')} value={roleKeys[memberKey] ?? ''} onChange={(event) => setRoleKeys((current) => ({ ...current, [memberKey]: event.target.value }))}><option value="">{t('members.selectRole')}</option>{availableRoles.map((role) => <option key={role.key} value={role.key}>{role.name}</option>)}</Select><Button size="sm" type="button" variant="outline" disabled={!roleKeys[memberKey] || grant.isPending} onClick={() => void grantRole(member.principalIssuer, member.principalSubject)}>{t('members.assignRole')}</Button></>}
                </div>
              </div>
            })}
            {access.data?.members.length === 0 && <p className="px-4 py-6 text-sm text-muted-foreground">{t('members.empty')}</p>}
          </div>
        )}
      </Card>
      <Card id="roller-ve-izinler" className="scroll-mt-24 gap-5 px-6 pt-[22px] pb-6">
        <SectionHeading title={t('roles.title')} description={t('roles.description')} />
        <div className="divide-y rounded-[var(--nx-r-card)] border">
          {access.data?.roles.map((role) => <div key={role.key} className="px-4 py-3.5"><p className="text-sm font-medium">{role.name}</p><p className="mt-1 font-mono text-[11px] text-muted-foreground">{role.actionKeys.join(' · ') || t('roles.noPermissions')}</p></div>)}
          {access.data?.roles.length === 0 && <p className="px-4 py-6 text-sm text-muted-foreground">{t('roles.empty')}</p>}
        </div>
      </Card>
    </>
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
  const sections: readonly NavSection[] = useMemo(
    () => [
      { id: 'sirket-kimligi', label: t('page.sectionIdentity'), icon: Building2 },
      { id: 'vergi-bilgileri', label: t('page.sectionLegal'), icon: Landmark },
      { id: 'iletisim-ve-varsayilanlar', label: t('page.sectionContact'), icon: Mail },
      { id: 'kullanicilar', label: t('page.sectionMembers'), icon: Users },
      { id: 'roller-ve-izinler', label: t('page.sectionRoles'), icon: ShieldCheck },
    ],
    [t],
  )

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
    return <div className="mx-auto w-full max-w-[1320px] animate-pulse space-y-5"><div className="h-16 w-80 rounded-[var(--nx-r-card)] bg-muted" /><div className="h-72 rounded-[var(--nx-r-card)] bg-muted" /></div>
  }
  if (profile.isError) {
    const message = companyProblem(profile.error as unknown as ApiError, t)
    return (
      <div className="mx-auto flex w-full max-w-[1320px] flex-col gap-5">
        <PageHeader title={t('page.title')} description={t('page.description')} />
        <Alert variant="destructive"><Phone aria-hidden /><AlertTitle>{message.title}</AlertTitle><AlertDescription>{message.description}</AlertDescription></Alert>
      </div>
    )
  }

  return (
    <div className="mx-auto flex w-full max-w-[1320px] flex-col gap-5">
      <PageHeader title={t('page.title')} description={t('page.description')} eyebrow={t('page.eyebrow')} />
      <div className="grid grid-cols-1 items-start gap-5 lg:grid-cols-[236px_minmax(0,1fr)]">
        <SectionNav sections={sections} label={t('page.sectionNavLabel')} />
        <FormProvider {...form}>
          <div className="flex flex-col gap-4">
            <ProfileFields />
            <LegalFields />
            <ContactFields />
            <AccessFields />
            {form.formState.errors.root?.message && <Alert variant="destructive"><AlertTitle>{t('problem.conflictTitle')}</AlertTitle><AlertDescription>{form.formState.errors.root.message}</AlertDescription></Alert>}
            <SaveBar onSave={save} isUpdating={update.isPending} />
          </div>
        </FormProvider>
      </div>
    </div>
  )
}
