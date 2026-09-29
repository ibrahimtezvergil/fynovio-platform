import { zodResolver } from '@hookform/resolvers/zod'
import { useMemo } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import type { SelectOption } from '@/components/common/inputs/types'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { useCreateParty } from '../api'
import { useKeyedCommand } from '../lib/useKeyedCommand'
import { newPartyFormSchema, type NewPartyValues } from '../schema'
import { CommandDialog } from './CommandDialog'

interface NewPartyDialogProps {
  onClose: () => void
  /** The freshly created customer, already in the shape the picker shows. */
  onCreated: (option: SelectOption) => void
}

/** For the customer who is not in the list yet: the salesperson adds them here instead of leaving the form. */
export function NewPartyDialog({ onClose, onCreated }: NewPartyDialogProps) {
  const { t } = useTranslation('opportunities')
  const schema = useMemo(() => newPartyFormSchema(t), [t])
  const command = useKeyedCommand(useCreateParty())
  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<NewPartyValues>({ resolver: zodResolver(schema), defaultValues: { partyType: 'Organization', name: '', surname: '', phone: '', email: '' } })
  const isPerson = useWatch({ control, name: 'partyType' }) === 'Person'

  const submit = handleSubmit(async (values) => {
    const result = await command.run({ partyType: values.partyType, name: values.name, surname: isPerson ? values.surname || null : null, phone: values.phone || null, email: values.email || null })
    if (!result) return
    const label = [values.name, isPerson ? values.surname : ''].filter(Boolean).join(' ')
    onCreated({ value: String(result.id), label, description: values.email || (isPerson ? t('newParty.type.Person') : t('newParty.type.Organization')) })
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
