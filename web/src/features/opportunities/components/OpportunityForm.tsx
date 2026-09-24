import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Field } from '@/components/common/Field'
import type { SelectOption } from '@/components/common/inputs/types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { paths } from '@/routes/paths'
import { useCreateOpportunity } from '../api'
import { useCrmSettings } from '@/features/crm-settings/api'
import { useKeyedCommand } from '../lib/useKeyedCommand'
import { createOpportunityFormSchema, type CreateOpportunityInput, type CreateOpportunityValues } from '../schema'
import { PartyPicker } from './PartyPicker'
import { ProblemNotice } from './ProblemNotice'

/**
 * Built from the real Phase 2 create contract and nothing more. Tenant and owner are taken from the
 * authenticated session by the backend, so neither is a field. The customer is chosen from the server-side Party search
 * (never typed as a raw id). The initial lifecycle (Draft, no stage) is the
 * backend's; the entry stage is assigned by Open.
 */
export function OpportunityForm() {
  const { t } = useTranslation('opportunities')
  const navigate = useNavigate()
  const settings = useCrmSettings()
  const [step, setStep] = useState(0)
  const isWizard = settings.data?.opportunityCreationMode === 'Wizard'
  const schema = useMemo(() => createOpportunityFormSchema(t), [t])
  const command = useKeyedCommand(useCreateOpportunity())
  // The form stores the Party id; the picker needs the whole option (its label) to keep showing the chosen name.
  const [party, setParty] = useState<SelectOption | null>(null)
  const {
    register,
    control,
    handleSubmit,
    trigger,
    formState: { errors },
  } = useForm<CreateOpportunityInput, unknown, CreateOpportunityValues>({
    resolver: zodResolver(schema),
    defaultValues: { partyId: '', currency: 'TRY', estimatedAmount: '' },
  })

  const submit = handleSubmit(async (values) => {
    const result = await command.run(values)
    if (result) navigate(paths.crmOpportunity(result.opportunityId), { replace: true })
  })
  const continueWizard = async () => {
    if (await trigger('partyId')) setStep(1)
  }

  return (
    <form onSubmit={submit} noValidate className="grid max-w-xl gap-4">
      {(!isWizard || step === 0) && <Field label={t('form.partyId.label')} hint={t('form.partyId.hint')} error={errors.partyId?.message}>
        {(props) => (
          <Controller
            control={control}
            name="partyId"
            render={({ field }) => (
              <PartyPicker
                {...props}
                value={party}
                onValueChange={(option) => {
                  setParty(option)
                  field.onChange(option?.value ?? '')
                }}
              />
            )}
          />
        )}
      </Field>}
      {(!isWizard || step === 1) && <div className="grid grid-cols-[120px_1fr] gap-3">
        <Field label={t('form.currency.label')} error={errors.currency?.message}>
        {(props) => (
          <Select {...props} {...register('currency')}>
            <option value="TRY">{t('form.currency.options.TRY')}</option>
            <option value="USD">{t('form.currency.options.USD')}</option>
            <option value="EUR">{t('form.currency.options.EUR')}</option>
          </Select>
        )}
        </Field>
        <Field label={t('form.estimatedAmount.label')} hint={t('form.estimatedAmount.hint')} error={errors.estimatedAmount?.message}>
          {(props) => <Input {...props} inputMode="decimal" autoComplete="off" placeholder="0,00" {...register('estimatedAmount')} />}
        </Field>
      </div>}
      {command.problem && <ProblemNotice problem={command.problem} />}
      <div className="flex items-center gap-2">
        {isWizard && step === 0 && <Button type="button" onClick={() => void continueWizard()}>{t('form.next')}</Button>}
        {isWizard && step === 1 && <Button type="button" variant="ghost" disabled={command.isPending} onClick={() => setStep(0)}>{t('form.previousStep')}</Button>}
        {(!isWizard || step === 1) && <Button type="submit" disabled={command.isPending}>
          {command.isPending && <Loader2 aria-hidden className="animate-spin" />}
          {command.isPending ? t('form.submitting') : t('form.submit')}
        </Button>}
        <Button type="button" variant="ghost" disabled={command.isPending} onClick={() => navigate(paths.crmOpportunities)}>
          {t('common.cancel')}
        </Button>
      </div>
    </form>
  )
}
