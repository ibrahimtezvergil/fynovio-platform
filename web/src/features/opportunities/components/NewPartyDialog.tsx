import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useMemo } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { useCreateParty } from '../api'
import { useKeyedCommand } from '../lib/useKeyedCommand'
import { draftFromQuery } from '../lib/partyDraft'
import { newPartyFormSchema, type NewPartyValues, type PartyReference } from '../schema'
import { CommandDialog } from './CommandDialog'

interface NewPartyDialogProps {
  /** What was typed in the customer search; sorted into name, phone or e-mail so nothing is retyped. */
  initialQuery: string
  onClose: () => void
  /** The freshly created customer, in the shape the search returns. */
  onCreated: (party: PartyReference) => void
}

/** For the customer who is not in the list yet: the salesperson adds them here instead of leaving the form. */
export function NewPartyDialog({ initialQuery, onClose, onCreated }: NewPartyDialogProps) {
  const { t } = useTranslation('opportunities')
  const schema = useMemo(() => newPartyFormSchema(t), [t])
  const command = useKeyedCommand(useCreateParty())
  const {
    register,
    control,
    handleSubmit,
    setFocus,
    formState: { errors },
  } = useForm<NewPartyValues>({ resolver: zodResolver(schema), defaultValues: { partyType: 'Organization', surname: '', ...draftFromQuery(initialQuery) } })
  // The dialog opens with the cursor where typing is needed, so "create" is: click, confirm the name, Enter.
  useEffect(() => {
    const frame = requestAnimationFrame(() => setFocus('name'))
    return () => cancelAnimationFrame(frame)
  }, [setFocus])
  const isPerson = useWatch({ control, name: 'partyType' }) === 'Person'

  const submit = handleSubmit(async (values) => {
    const result = await command.run({ partyType: values.partyType, name: values.name, surname: isPerson ? values.surname || null : null, phone: values.phone || null, email: values.email || null })
    if (!result) return
    const displayName = [values.name, isPerson ? values.surname : ''].filter(Boolean).join(' ')
    onCreated({ id: result.id, partyType: values.partyType, displayName, email: values.email || null, phone: values.phone || null })
    onClose()
  })

  return (
    <CommandDialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t('newParty.title')}
      description={t('newParty.description')}
      submitLabel={command.isPending ? t('newParty.submitting') : t('newParty.submit')}
      pending={command.isPending}
      problem={command.problem}
      onReload={onClose}
      onSubmit={submit}
    >
      <Field label={t('newParty.type.label')}>
        {(props) => (
          <Select {...props} {...register('partyType')}>
            <option value="Organization">{t('newParty.type.Organization')}</option>
            <option value="Person">{t('newParty.type.Person')}</option>
          </Select>
        )}
      </Field>
      <div className={isPerson ? 'grid grid-cols-2 gap-3' : 'grid gap-3'}>
        <Field label={isPerson ? t('newParty.name.person') : t('newParty.name.organization')} error={errors.name?.message}>
          {(props) => <Input {...props} autoComplete="off" {...register('name')} />}
        </Field>
        {isPerson && (
          <Field label={t('newParty.surname')} error={errors.surname?.message}>
            {(props) => <Input {...props} autoComplete="off" {...register('surname')} />}
          </Field>
        )}
      </div>
      <div className="grid grid-cols-2 gap-3">
        <Field label={t('newParty.phone.label')} error={errors.phone?.message}>
          {(props) => <Input {...props} type="tel" autoComplete="off" {...register('phone')} />}
        </Field>
        <Field label={t('newParty.email.label')} error={errors.email?.message}>
          {(props) => <Input {...props} type="email" autoComplete="off" {...register('email')} />}
        </Field>
      </div>
    </CommandDialog>
  )
}
