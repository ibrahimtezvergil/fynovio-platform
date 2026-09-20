import { zodResolver } from '@hookform/resolvers/zod'
import { useMemo } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { useAddLine, useCancelLine } from '../api'
import { useKeyedCommand } from '../lib/useKeyedCommand'
import { addLineFormSchema, reasonFormSchema, type AddLineInput, type AddLineValues, type Opportunity, type OpportunityLine, type ReasonValues } from '../schema'
import { CommandDialog } from './CommandDialog'

interface LineDialogProps {
  opportunity: Opportunity
  onClose: () => void
  onReload: () => void
}

export function AddLineDialog({ opportunity, onClose, onReload }: LineDialogProps) {
  const { t } = useTranslation('opportunities')
  const schema = useMemo(() => addLineFormSchema(t), [t])
  const command = useKeyedCommand(useAddLine())
  const {
    control,
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<AddLineInput, unknown, AddLineValues>({
    resolver: zodResolver(schema),
    defaultValues: { productId: '', quantity: 1, unitPrice: '', isOptional: false },
  })

  const submit = handleSubmit(async (values) => {
    const result = await command.run({
      id: opportunity.id,
      expectedVersion: opportunity.rowVersion,
      ...values,
      sortOrder: opportunity.lines.length,
    })
    if (result) onClose()
  })

  return (
    <CommandDialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t('lines.add.title')}
      description={t('lines.add.description')}
      submitLabel={command.isPending ? t('lines.add.submitting') : t('lines.add.submit')}
      pending={command.isPending}
      problem={command.problem}
      onReload={onReload}
      onSubmit={submit}
    >
      <Field label={t('lines.add.productId.label')} hint={t('lines.add.productId.hint')} error={errors.productId?.message}>
        {(props) => <Input {...props} inputMode="numeric" autoComplete="off" {...register('productId')} />}
      </Field>
      <div className="grid grid-cols-2 gap-3">
        <Field label={t('lines.add.quantity.label')} error={errors.quantity?.message}>
          {(props) => <Input {...props} inputMode="numeric" autoComplete="off" {...register('quantity')} />}
        </Field>
        <Field label={t('lines.add.unitPrice.label')} error={errors.unitPrice?.message}>
          {(props) => <Input {...props} inputMode="decimal" autoComplete="off" {...register('unitPrice')} />}
        </Field>
      </div>
      <Controller
        control={control}
        name="isOptional"
        render={({ field }) => (
          <div className="flex items-center gap-2">
            <Checkbox id="line-optional" checked={field.value} onChange={(event) => field.onChange(event.target.checked)} />
            <Label htmlFor="line-optional">{t('lines.add.isOptional')}</Label>
          </div>
        )}
      />
    </CommandDialog>
  )
}

export function CancelLineDialog({ opportunity, line, onClose, onReload }: LineDialogProps & { line: OpportunityLine }) {
  const { t } = useTranslation('opportunities')
  const schema = useMemo(() => reasonFormSchema(t), [t])
  const command = useKeyedCommand(useCancelLine())
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ReasonValues>({ resolver: zodResolver(schema), defaultValues: { reason: '' } })

  const submit = handleSubmit(async ({ reason }) => {
    const result = await command.run({ id: opportunity.id, expectedVersion: opportunity.rowVersion, lineId: line.id, cancelReason: reason })
    if (result) onClose()
  })

  return (
    <CommandDialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t('lines.cancel.title', { id: line.id })}
      description={t('lines.cancel.description')}
      submitLabel={command.isPending ? t('lines.cancel.submitting') : t('lines.cancel.submit')}
      destructive
      pending={command.isPending}
      problem={command.problem}
      onReload={onReload}
      onSubmit={submit}
    >
      <Field label={t('lines.cancel.reason.label')} error={errors.reason?.message}>
        {(props) => <Textarea {...props} rows={3} {...register('reason')} />}
      </Field>
    </CommandDialog>
  )
}
