import { zodResolver } from '@hookform/resolvers/zod'
import { useMemo } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { useCrmSettings } from '@/features/crm-settings/api'
import { useLoseOpportunity, useOpenOpportunity, useWinOpportunity } from '../api'
import { useKeyedCommand } from '../lib/useKeyedCommand'
import { endOfLocalDay, openFormSchema, reasonFormSchema, type OpenValues, type Opportunity, type ReasonValues } from '../schema'
import { CommandDialog } from './CommandDialog'

interface LifecycleDialogProps {
  opportunity: Pick<Opportunity, 'id' | 'rowVersion'>
  onClose: () => void
  onReload: () => void
}

const toDateInput = (date: Date) =>
  `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`

function defaultExpiry(): string {
  const date = new Date()
  date.setDate(date.getDate() + 30)
  return toDateInput(date)
}

export function OpenDialog({ opportunity, onClose, onReload }: Omit<LifecycleDialogProps, 'opportunity'> & { opportunity: Opportunity }) {
  const { t } = useTranslation('opportunities')
  const schema = useMemo(() => openFormSchema(t), [t])
  const command = useKeyedCommand(useOpenOpportunity())
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<OpenValues>({ resolver: zodResolver(schema), defaultValues: { expiryDate: defaultExpiry() } })

  const submit = handleSubmit(async ({ expiryDate }) => {
    const result = await command.run({
      id: opportunity.id,
      expectedVersion: opportunity.rowVersion,
      expiryDate: endOfLocalDay(expiryDate).toISOString(),
    })
    if (result) onClose()
  })
  const hasActiveRequiredLine = opportunity.lines.some((line) => !line.isCanceled && !line.isOptional)

  return (
    <CommandDialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t('open.title')}
      description={t('open.description')}
      submitLabel={command.isPending ? t('open.submitting') : t('open.submit')}
      pending={command.isPending}
      problem={command.problem}
      onReload={onReload}
      onSubmit={submit}
    >
      {!hasActiveRequiredLine && (
        <Alert>
          <AlertTitle>{t('open.missingLine.title')}</AlertTitle>
          <AlertDescription>{t('open.missingLine.description')}</AlertDescription>
        </Alert>
      )}
      <Field label={t('open.expiry.label')} hint={t('open.expiry.hint')} error={errors.expiryDate?.message}>
        {(props) => <Input {...props} type="date" min={toDateInput(new Date())} {...register('expiryDate')} />}
      </Field>
    </CommandDialog>
  )
}

export function WinDialog({ opportunity, onClose, onReload }: LifecycleDialogProps) {
  const { t } = useTranslation('opportunities')
  const command = useKeyedCommand(useWinOpportunity())

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    const result = await command.run({ id: opportunity.id, expectedVersion: opportunity.rowVersion })
    if (result) onClose()
  }

  return (
    <CommandDialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t('win.title')}
      description={t('win.description')}
      submitLabel={command.isPending ? t('win.submitting') : t('win.submit')}
      pending={command.isPending}
      problem={command.problem}
      onReload={onReload}
      onSubmit={submit}
    />
  )
}

export function LoseDialog({ opportunity, onClose, onReload }: LifecycleDialogProps) {
  const { t } = useTranslation('opportunities')
  const schema = useMemo(() => reasonFormSchema(t), [t])
  const command = useKeyedCommand(useLoseOpportunity())
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ReasonValues>({ resolver: zodResolver(schema), defaultValues: { reason: '' } })
  // A tenant that maintains a lost-reason catalog only accepts one of its active entries (matched by key).
  const settings = useCrmSettings()
  const reasons = settings.data?.lostReasons.filter((reason) => reason.status === 'Active') ?? []

  const submit = handleSubmit(async ({ reason }) => {
    const result = await command.run({ id: opportunity.id, expectedVersion: opportunity.rowVersion, lostReason: reason })
    if (result) onClose()
  })

  return (
    <CommandDialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t('lose.title')}
      description={t('lose.description')}
      submitLabel={command.isPending ? t('lose.submitting') : t('lose.submit')}
      destructive
      pending={command.isPending}
      problem={command.problem}
      onReload={onReload}
      onSubmit={submit}
    >
      <Field label={t('lose.reason.label')} error={errors.reason?.message}>
        {(props) =>
          reasons.length > 0 ? (
            <Select {...props} {...register('reason')}>
              <option value="">{t('lose.reason.placeholder')}</option>
              {reasons.map((reason) => (
                <option key={reason.id} value={reason.key}>
                  {reason.name}
                </option>
              ))}
            </Select>
          ) : (
            <Textarea {...props} rows={3} {...register('reason')} />
          )
        }
      </Field>
    </CommandDialog>
  )
}
