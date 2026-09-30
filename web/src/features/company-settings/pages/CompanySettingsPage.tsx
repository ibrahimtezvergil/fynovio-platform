import { zodResolver } from '@hookform/resolvers/zod'
import { Building2, Check, Landmark, LoaderCircle, Mail, Phone, Plus, RotateCcw, Search, ShieldCheck, UserPlus, Users, X } from 'lucide-react'
import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { FormProvider, useForm, useFormContext } from 'react-hook-form'
import { useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { PageHeader } from '@/components/common/PageHeader'
import { Field } from '@/components/common/Field'
import { PageNav, type PageNavItem } from '@/components/common/PageNav'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { useTenantSwitchGuard } from '@/lib/auth'
import { useAttemptKeys } from '@/lib/mutations/attemptKey'
import type { ApiError } from '@/types'
import { TenantMenu } from '@/layouts/components/TenantMenu'
import { paths } from '@/routes/paths'
import { useCancelCompanyInvitation, useCompanyAccess, useCompanySettings, useCreateCompanyRole, useGrantCompanyRole, useInviteCompanyMember, useRevokeCompanyRole, useUpdateCompanyRole, useUpdateCompanySettings } from '../api'
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

function roleDisplayName(role: { key: string; name: string }, t: (key: string) => string) {
  if (role.key === 'tenant_administrator') return t('roles.administrator')
  if (role.key === 'support') return t('roles.support')
  return role.name
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

function AccessFields({ section }: { section: 'members' | 'roles' }) {
  const { t, i18n } = useTranslation('company-settings')
  const access = useCompanyAccess()
  const invite = useInviteCompanyMember()
  const cancelInvitation = useCancelCompanyInvitation()
  const grant = useGrantCompanyRole()
  const revoke = useRevokeCompanyRole()
  const createRole = useCreateCompanyRole()
  const updateRole = useUpdateCompanyRole()
  const keys = useAttemptKeys()
  const [email, setEmail] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [inviteRoleKey, setInviteRoleKey] = useState('')
  const [roleKeys, setRoleKeys] = useState<Record<string, string>>({})
  const [editingRoleKey, setEditingRoleKey] = useState<string | 'new' | null>(null)
  const [roleName, setRoleName] = useState('')
  const [selectedActions, setSelectedActions] = useState<string[]>([])
  const [actionSearch, setActionSearch] = useState('')

  const inviteMember = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const values = { email, displayName: displayName || undefined, locale: i18n.language,
      roleKey: access.data?.canGrant ? inviteRoleKey || undefined : undefined }
    const key = keys.begin(values)
    try {
      await invite.mutateAsync({ ...values, idempotencyKey: key })
      keys.settle(null)
      setEmail('')
      setDisplayName('')
      setInviteRoleKey('')
    } catch (error) {
      keys.settle(error as ApiError)
    }
  }
  const resendInvitation = async (invitation: { email: string; displayName: string | null; roleKey: string | null }) => {
    const values = { email: invitation.email, displayName: invitation.displayName || undefined,
      locale: i18n.language, roleKey: invitation.roleKey || undefined }
    const key = keys.begin({ ...values, resend: crypto.randomUUID() })
    try {
      await invite.mutateAsync({ ...values, idempotencyKey: key })
      keys.settle(null)
    } catch (error) {
      keys.settle(error as ApiError)
    }
  }
  const cancelPendingInvitation = async (invitationId: string) => {
    const key = keys.begin({ invitationId })
    try {
      await cancelInvitation.mutateAsync({ invitationId, idempotencyKey: key })
      keys.settle(null)
    } catch (error) {
      keys.settle(error as ApiError)
    }
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
  const openRoleEditor = (roleKey?: string) => {
    const role = roleKey ? access.data?.roles.find((candidate) => candidate.key === roleKey) : undefined
    setEditingRoleKey(roleKey ?? 'new')
    setRoleName(role?.name ?? '')
    setSelectedActions(role?.permissions.map((permission) => permission.actionKey) ?? [])
    setActionSearch('')
  }
  const saveRole = async () => {
    if (!access.data || !roleName.trim() || (editingRoleKey === 'new' && selectedActions.length === 0)) return
    const values = { name: roleName.trim(), actionKeys: selectedActions, expectedRevision: access.data.revision }
    const key = keys.begin({ ...values, roleKey: editingRoleKey })
    try {
      if (editingRoleKey === 'new') await createRole.mutateAsync({ ...values, idempotencyKey: key })
      else if (editingRoleKey) await updateRole.mutateAsync({ ...values, roleKey: editingRoleKey, idempotencyKey: key })
      keys.settle(null)
      setEditingRoleKey(null)
    } catch (error) {
      keys.settle(error as ApiError)
    }
  }
  const toggleAction = (actionKey: string) => setSelectedActions((current) =>
    current.includes(actionKey) ? current.filter((key) => key !== actionKey) : [...current, actionKey])
  const filteredActions = access.data?.availableActions.filter((action) => {
    const query = actionSearch.trim().toLocaleLowerCase()
    return !query || action.key.includes(query) || t(`roles.actions.${action.key}`, { defaultValue: action.key }).toLocaleLowerCase().includes(query)
  }) ?? []
  const accessDenied = (access.error as ApiError | null)?.status === 403

  return (
    <>
      {section === 'members' && <Card className="gap-5 px-6 pt-[22px] pb-6">
        <SectionHeading title={t('members.title')} description={t('members.description')} />
        {access.data?.canInvite && <form onSubmit={inviteMember} className="grid grid-cols-1 items-end gap-3 sm:grid-cols-2 xl:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,1fr)_auto]">
          <Field label={t('members.email')}>{(props) => <Input {...props} value={email} type="email" required onChange={(event) => setEmail(event.target.value)} />}</Field>
          <Field label={t('members.displayName')}>{(props) => <Input {...props} value={displayName} onChange={(event) => setDisplayName(event.target.value)} />}</Field>
          {access.data.canGrant && <Field label={t('members.inviteRole')}>{(props) => <Select {...props} value={inviteRoleKey} onChange={(event) => setInviteRoleKey(event.target.value)}><option value="">{t('members.noRole')}</option>{access.data?.roles.map((role) => <option key={role.key} value={role.key}>{role.name}</option>)}</Select>}</Field>}
          <Button type="submit" disabled={invite.isPending}><UserPlus aria-hidden />{t('members.invite')}</Button>
        </form>}
        {access.data && access.data.pendingInvitations.length > 0 && (
          <div className="space-y-2">
            <h3 className="text-sm font-medium">{t('members.pendingTitle')}</h3>
            <div className="divide-y rounded-[var(--nx-r-card)] border">
              {access.data.pendingInvitations.map((invitation) => (
                <div key={invitation.invitationId} className="flex flex-wrap items-center gap-3 px-4 py-3">
                  <div className="min-w-0 flex-1"><p className="truncate text-sm">{invitation.email}</p><p className="text-xs text-muted-foreground">{invitation.roleKey ? access.data.roles.find((role) => role.key === invitation.roleKey)?.name ?? invitation.roleKey : t('members.noRole')} · {t('members.expiresAt', { date: new Date(invitation.expiresAt).toLocaleDateString(i18n.language) })}</p></div>
                  {access.data.canInvite && <>{(!invitation.roleKey || access.data.canGrant) && <Button type="button" variant="outline" size="sm" disabled={invite.isPending} onClick={() => void resendInvitation(invitation)}>{t('members.resend')}</Button>}
                  <Button type="button" variant="ghost" size="sm" disabled={cancelInvitation.isPending} onClick={() => void cancelPendingInvitation(invitation.invitationId)}>{t('members.cancelInvitation')}</Button></>}
                </div>
              ))}
            </div>
          </div>
        )}
        {access.isError && <Alert variant="destructive"><AlertTitle>{t(accessDenied ? 'accessProblem.forbiddenTitle' : 'accessProblem.title')}</AlertTitle><AlertDescription>{t(accessDenied ? 'accessProblem.forbiddenDescription' : 'accessProblem.description')}</AlertDescription></Alert>}
        {access.isPending ? <div className="h-28 animate-pulse rounded-[var(--nx-r-card)] bg-muted" /> : (
          <div className="divide-y rounded-[var(--nx-r-card)] border">
            {access.data?.members.map((member) => {
              const memberKey = `${member.principalIssuer}/${member.principalSubject}`
              const availableRoles = access.data.roles.filter((role) => !member.assignments.some((assignment) => assignment.roleKey === role.key))
              return <div key={memberKey} className="flex flex-col gap-3 px-4 py-3.5 sm:flex-row sm:items-center">
                <div className="min-w-0 flex-1"><p className="truncate text-sm font-medium">{member.displayName}</p><p className="truncate text-xs text-muted-foreground">{member.email}</p></div>
                <div className="flex flex-wrap items-center gap-1.5">
                  {member.assignments.map((assignment) => <span key={assignment.assignmentId} className="inline-flex items-center gap-1 rounded-full bg-secondary px-2 py-1 text-xs">{assignment.roleName}{access.data.canRevoke && assignment.canRevoke && <button type="button" aria-label={t('members.removeRole', { role: assignment.roleName })} onClick={() => void revokeRole(assignment.assignmentId)} disabled={revoke.isPending}><X className="size-3" /></button>}</span>)}
                  {access.data.canGrant && member.status === 'active' && availableRoles.length > 0 && <><Select aria-label={t('members.selectRole')} value={roleKeys[memberKey] ?? ''} onChange={(event) => setRoleKeys((current) => ({ ...current, [memberKey]: event.target.value }))}><option value="">{t('members.selectRole')}</option>{availableRoles.map((role) => <option key={role.key} value={role.key}>{role.name}</option>)}</Select><Button size="sm" type="button" variant="outline" disabled={!roleKeys[memberKey] || grant.isPending} onClick={() => void grantRole(member.principalIssuer, member.principalSubject)}>{t('members.assignRole')}</Button></>}
                </div>
              </div>
            })}
            {access.data?.members.length === 0 && <p className="px-4 py-6 text-sm text-muted-foreground">{t('members.empty')}</p>}
          </div>
        )}
      </Card>}
      {section === 'roles' && <Card className="gap-5 px-6 pt-[22px] pb-6">
        <div className="flex flex-wrap items-start gap-3"><SectionHeading title={t('roles.title')} description={t('roles.description')} />
          {access.data?.canManageRoles && <Button type="button" size="sm" onClick={() => openRoleEditor()}><Plus aria-hidden />{t('roles.create')}</Button>}
        </div>
        <div className="divide-y rounded-[var(--nx-r-card)] border">
          {access.data?.roles.map((role) => <div key={role.key} className="px-4 py-3.5"><div className="flex flex-wrap items-center gap-2"><p className="mr-auto text-sm font-medium">{roleDisplayName(role, t)}</p>{role.origin === 'system_template' && <span className="rounded-md bg-secondary px-2 py-1 text-xs text-muted-foreground">{t('roles.systemManaged')}</span>}{role.canEdit && <Button type="button" size="sm" variant="outline" onClick={() => openRoleEditor(role.key)}>{t('roles.edit')}</Button>}</div>{role.permissions.length > 0 ? <details className="group mt-2"><summary className="text-muted-foreground flex cursor-pointer list-none items-center justify-between gap-3 text-xs hover:text-foreground"><span className="rounded-md bg-secondary px-2 py-1">{t('roles.permissionCount', { count: role.permissions.length })}</span><span className="group-open:hidden">{t('roles.showPermissions')}</span><span className="hidden group-open:inline">{t('roles.hidePermissions')}</span></summary><div className="mt-2 grid gap-1.5 sm:grid-cols-2">{role.permissions.map((permission) => <span key={`${permission.actionKey}/${permission.relation}`} className="truncate rounded-md bg-secondary px-2 py-1 text-xs" title={`${t(`roles.actions.${permission.actionKey}`, { defaultValue: permission.actionKey })} · ${permission.relation === 'owner' ? t('roles.ownerScope') : t('roles.tenantScope')}`}>{t(`roles.actions.${permission.actionKey}`, { defaultValue: permission.actionKey })}</span>)}</div></details> : <p className="text-muted-foreground mt-2 text-xs">{t('roles.noPermissions')}</p>}</div>)}
          {access.data?.roles.length === 0 && <p className="px-4 py-6 text-sm text-muted-foreground">{t('roles.empty')}</p>}
        </div>
        <Dialog open={editingRoleKey !== null} onOpenChange={(open) => { if (!open) setEditingRoleKey(null) }}>
          <DialogContent className="sm:max-w-2xl">
            <DialogHeader><DialogTitle>{editingRoleKey === 'new' ? t('roles.create') : t('roles.edit')}</DialogTitle><DialogDescription>{t('roles.editorDescription')}</DialogDescription></DialogHeader>
            <Field label={t('roles.name')}>{(props) => <Input {...props} value={roleName} disabled={editingRoleKey === 'support'} onChange={(event) => setRoleName(event.target.value)} />}</Field>
            <div className="space-y-3"><div className="flex flex-wrap items-center justify-between gap-2"><p className="text-sm font-medium">{t('roles.permissions')}</p><span className="text-xs text-muted-foreground">{t('roles.selected', { count: selectedActions.length })}</span></div>
              <div className="relative"><Search aria-hidden className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" /><Input aria-label={t('roles.search')} className="pl-9" value={actionSearch} onChange={(event) => setActionSearch(event.target.value)} /></div>
              <div className="max-h-72 divide-y overflow-y-auto rounded-[var(--nx-r-card)] border">{filteredActions.map((action) => <label key={action.key} className="flex cursor-pointer items-center gap-3 px-3 py-2.5 hover:bg-muted"><Checkbox aria-label={t(`roles.actions.${action.key}`, { defaultValue: action.key })} checked={selectedActions.includes(action.key)} onChange={() => toggleAction(action.key)} /><span className="min-w-0 flex-1"><span className="block text-sm">{t(`roles.actions.${action.key}`, { defaultValue: action.key })}</span><span className="block truncate text-xs text-muted-foreground">{action.ownerModule} · {action.key}</span></span></label>)}{filteredActions.length === 0 && <p className="px-3 py-6 text-sm text-muted-foreground">{t('roles.noAction')}</p>}</div>
            </div>
            <DialogFooter><Button type="button" variant="outline" onClick={() => setEditingRoleKey(null)}>{t('roles.cancel')}</Button><Button type="button" disabled={!roleName.trim() || (editingRoleKey === 'new' && selectedActions.length === 0) || createRole.isPending || updateRole.isPending} onClick={() => void saveRole()}>{t('roles.save')}</Button></DialogFooter>
          </DialogContent>
        </Dialog>
      </Card>}
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
  const { section } = useParams<{ section?: string }>()
  const currentSection = section === 'legal' || section === 'contact' || section === 'members' || section === 'roles' ? section : 'identity'
  const profile = useCompanySettings()
  const update = useUpdateCompanySettings()
  const keys = useAttemptKeys()
  const form = useForm<CompanySettingsFormValues>({ resolver: zodResolver(companySettingsFormSchema), mode: 'onBlur' })
  const setTenantSwitchConfirmation = useTenantSwitchGuard((state) => state.setConfirmation)
  const pendingSwitch = useRef<((proceed: boolean) => void) | null>(null)
  const [switchConfirmationOpen, setSwitchConfirmationOpen] = useState(false)
  const sections: readonly PageNavItem[] = useMemo(
    () => [
      { to: paths.companySettings, label: t('page.sectionIdentity'), icon: Building2 },
      { to: paths.companySettingsSection('legal'), label: t('page.sectionLegal'), icon: Landmark },
      { to: paths.companySettingsSection('contact'), label: t('page.sectionContact'), icon: Mail },
      { to: paths.companySettingsSection('members'), label: t('page.sectionMembers'), icon: Users },
      { to: paths.companySettingsSection('roles'), label: t('page.sectionRoles'), icon: ShieldCheck },
    ],
    [t],
  )

  useEffect(() => {
    if (profile.data) form.reset(settingsToFormValues(profile.data))
  }, [form, profile.data])

  const confirmTenantSwitch = useCallback(() => {
    if (!form.formState.isDirty || pendingSwitch.current) return Promise.resolve(!pendingSwitch.current)
    return new Promise<boolean>((resolve) => {
      pendingSwitch.current = resolve
      setSwitchConfirmationOpen(true)
    })
  }, [form.formState.isDirty])

  const answerTenantSwitch = useCallback((proceed: boolean) => {
    pendingSwitch.current?.(proceed)
    pendingSwitch.current = null
    setSwitchConfirmationOpen(false)
  }, [])

  useEffect(() => {
    setTenantSwitchConfirmation(confirmTenantSwitch)
    return () => {
      setTenantSwitchConfirmation(null)
      pendingSwitch.current?.(false)
      pendingSwitch.current = null
    }
  }, [confirmTenantSwitch, setTenantSwitchConfirmation])

  useEffect(() => {
    if (!form.formState.isDirty) return

    const warnBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault()
      event.returnValue = true
    }
    window.addEventListener('beforeunload', warnBeforeUnload)
    return () => window.removeEventListener('beforeunload', warnBeforeUnload)
  }, [form.formState.isDirty])

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
        <aside className="flex flex-col gap-3 lg:sticky lg:top-[88px]" aria-label={t('page.sectionNavLabel')}>
          <TenantMenu collapsed={false} />
          <PageNav items={sections} label={t('page.sectionNavLabel')} />
        </aside>
        <FormProvider {...form}>
          <div className="flex flex-col gap-4">
            {currentSection === 'identity' && <ProfileFields />}
            {currentSection === 'legal' && <LegalFields />}
            {currentSection === 'contact' && <ContactFields />}
            {(currentSection === 'members' || currentSection === 'roles') && <AccessFields section={currentSection} />}
            {form.formState.errors.root?.message && <Alert variant="destructive"><AlertTitle>{t('problem.conflictTitle')}</AlertTitle><AlertDescription>{form.formState.errors.root.message}</AlertDescription></Alert>}
            {currentSection !== 'members' && currentSection !== 'roles' && <SaveBar onSave={save} isUpdating={update.isPending} />}
          </div>
        </FormProvider>
      </div>
      <AlertDialog open={switchConfirmationOpen} onOpenChange={(open) => { if (!open) answerTenantSwitch(false) }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('context.unsavedTitle')}</AlertDialogTitle>
            <AlertDialogDescription>{t('context.unsavedDescription')}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={() => answerTenantSwitch(false)}>{t('context.cancel')}</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={() => answerTenantSwitch(true)}>{t('context.discardAndSwitch')}</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
