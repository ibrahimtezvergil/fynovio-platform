import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2, UserPlus } from 'lucide-react'
import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Field } from '@/components/common/Field'
import type { SelectOption } from '@/components/common/inputs/types'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { cn } from '@/lib/utils'
import { paths } from '@/routes/paths'
import { useCreateOpportunity, useDefaultPipelineStages } from '../api'
import { useCrmSettings } from '@/features/crm-settings/api'
import { summarizeCreation } from '../lib/creationSummary'
import { useKeyedCommand } from '../lib/useKeyedCommand'
import { createOpportunityFormSchema, type CreateOpportunityInput, type CreateOpportunityValues } from '../schema'
import { NewPartyDialog } from './NewPartyDialog'
import { OpportunityStartSummary } from './OpportunityStartSummary'
import { PartyPicker } from './PartyPicker'
import { ProblemNotice } from './ProblemNotice'

const WIZARD_STEPS = ['customer', 'amount', 'review'] as const

/**
 * Built from the real create contract and nothing more: customer, currency, estimated amount. Tenant and owner come from
 * the authenticated session and the workspace settings on the server, so neither is a field — the summary beside the form
 * only tells the person what those settings will do. The customer is chosen from the server-side Party search (never a raw id),
 * or added on the spot. The initial lifecycle (Draft, no stage) is the backend's; the entry stage is assigned by Open.
 * Wizard mode (a workspace setting) walks the same three facts as customer → amount → review.
 */
export function OpportunityForm() {
  const { t, i18n } = useTranslation('opportunities')
  const navigate = useNavigate()
  const settings = useCrmSettings()
  const [step, setStep] = useState(0)
  const [addingParty, setAddingParty] = useState(false)
  const isWizard = settings.data?.opportunityCreationMode === 'Wizard'
  const lastStep = WIZARD_STEPS.length - 1
  const onStep = (name: (typeof WIZARD_STEPS)[number]) => !isWizard || WIZARD_STEPS[step] === name
  const hasDefaultPipeline = settings.data?.defaultPipelineDefinitionId != null
  const defaultStages = useDefaultPipelineStages(hasDefaultPipeline)
  const summary = useMemo(() => summarizeCreation(settings.data, defaultStages.data), [settings.data, defaultStages.data])
  const blocked = summary.blocker !== null
  const schema = useMemo(() => createOpportunityFormSchema(t), [t])
  const command = useKeyedCommand(useCreateOpportunity())
  // The form stores the Party id; the picker needs the whole option (its label) to keep showing the chosen name.
  const [party, setParty] = useState<SelectOption | null>(null)
  const {
    register,
    control,
    handleSubmit,
    trigger,
    setValue,
    formState: { errors },
  } = useForm<CreateOpportunityInput, unknown, CreateOpportunityValues>({
    resolver: zodResolver(schema),
    defaultValues: { partyId: '', currency: 'TRY', estimatedAmount: '' },
  })
  const stepBody = useRef<HTMLDivElement>(null)
  const stepMoved = useRef(false)

  // A wizard step change moves focus into the new step, so a keyboard user does not have to tab back from the button they pressed.
  useEffect(() => {
    if (!stepMoved.current) return
    stepBody.current?.querySelector<HTMLElement>('input, select, button')?.focus()
  }, [step])

  const create = (addLine: boolean) => handleSubmit(async (values) => {
    const result = await command.run(values)
    if (result) navigate(paths.crmOpportunity(result.opportunityId), { replace: true, state: addLine ? { addLine: true } : undefined })
  })
  const submit = create(false)
  const submitAndAddLine = create(true)

  const go = async (next: number) => {
    if (next > step && !(await trigger(step === 0 ? ['partyId'] : ['currency', 'estimatedAmount']))) return
    stepMoved.current = true
    setStep(next)
  }
  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    // Enter inside a wizard step means "next", never "create".
    if (isWizard && step < lastStep) {
      event.preventDefault()
      void go(step + 1)
      return
    }
    void submit(event)
  }

  const currency = useWatch({ control, name: 'currency' })
  const amountText = useWatch({ control, name: 'estimatedAmount' })
  // Same parser as the submit, so the review never shows a different number than the one that will be sent.
  const parsedAmount = schema.shape.estimatedAmount.safeParse(amountText)
  const formattedAmount = parsedAmount.success ? new Intl.NumberFormat(i18n.language, { style: 'currency', currency: currency || 'TRY' }).format(parsedAmount.data) : String(amountText)

  const showButtons = !isWizard || step === lastStep

  return (
    <div className={cn('grid items-start gap-6', settings.data && 'lg:grid-cols-[minmax(0,34rem)_20rem]')}>
      <form onSubmit={onSubmit} noValidate className="grid gap-4" aria-label={t('form.title')}>
        {isWizard && (
          <ol className="flex items-center gap-2 text-[12.5px]" aria-label={t('form.stepOf', { current: step + 1, total: WIZARD_STEPS.length })}>
            {WIZARD_STEPS.map((name, index) => (
              <li key={name} aria-current={index === step ? 'step' : undefined} className={cn('flex items-center gap-2', index === step ? 'font-[600]' : 'text-muted-foreground')}>
                <span className={cn('grid size-5 place-items-center rounded-full border text-[11px]', index === step && 'border-[var(--nx-tint)] bg-[var(--nx-tint)] text-white', index < step && 'border-[var(--nx-tint)] text-[var(--nx-tint)]')}>{index + 1}</span>
                {t(`form.steps.${name}`)}
                {index < WIZARD_STEPS.length - 1 && <span aria-hidden className="bg-border h-px w-6" />}
              </li>
            ))}
          </ol>
        )}

        <div ref={stepBody} className="grid gap-4">
          {onStep('customer') && (
            <div className="grid gap-2">
              <Field label={t('form.partyId.label')} hint={t('form.partyId.hint')} error={errors.partyId?.message}>
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
              </Field>
              <p className="text-muted-foreground flex flex-wrap items-center gap-x-2 text-[12.5px]">
                {t('form.newPartyPrompt')}
                <Button type="button" variant="link" size="sm" className="h-auto p-0" onClick={() => setAddingParty(true)}>
                  <UserPlus aria-hidden strokeWidth={1.8} />
                  {t('form.newPartyAction')}
                </Button>
              </p>
            </div>
          )}

          {onStep('amount') && (
            <div className="grid grid-cols-[120px_1fr] gap-3">
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
            </div>
          )}

          {isWizard && step === lastStep && (
            <Card className="gap-3 px-5 pt-[18px] pb-5">
              <h2 className="font-heading text-[15px] leading-tight font-[620]">{t('form.review.title')}</h2>
              <dl className="grid gap-3 text-[13px] sm:grid-cols-2">
                <div className="grid gap-0.5"><dt className="text-muted-foreground text-[12px]">{t('form.review.customer')}</dt><dd className="font-medium">{party?.label}</dd></div>
                <div className="grid gap-0.5"><dt className="text-muted-foreground text-[12px]">{t('form.review.amount')}</dt><dd className="font-medium">{formattedAmount}</dd></div>
              </dl>
              <p className="text-muted-foreground text-[12.5px]">{t('form.review.hint')}</p>
            </Card>
          )}
        </div>

        {command.problem && <ProblemNotice problem={command.problem} />}

        <div className="flex flex-wrap items-center gap-2">
          {isWizard && step > 0 && <Button type="button" variant="ghost" disabled={command.isPending} onClick={() => void go(step - 1)}>{t('form.previousStep')}</Button>}
          {isWizard && step < lastStep && <Button type="button" onClick={() => void go(step + 1)}>{t('form.next')}</Button>}
          {showButtons && (
            <>
              <Button type="submit" disabled={command.isPending || blocked}>
                {command.isPending && <Loader2 aria-hidden className="animate-spin" />}
                {command.isPending ? t('form.submitting') : t('form.submit')}
              </Button>
              <Button type="button" variant="outline" disabled={command.isPending || blocked} onClick={(event) => void submitAndAddLine(event)}>
                {t('form.submitAndAddLine')}
              </Button>
            </>
          )}
          <Button type="button" variant="ghost" disabled={command.isPending} onClick={() => navigate(paths.crmOpportunities)}>
            {t('common.cancel')}
          </Button>
        </div>
      </form>

      {settings.data && <OpportunityStartSummary summary={summary} loading={hasDefaultPipeline && defaultStages.isPending} />}

      {/* Outside the <form>: a portal's submit event still bubbles through the React tree and would submit the opportunity. */}
      {addingParty && (
        <NewPartyDialog
          onClose={() => setAddingParty(false)}
          onCreated={(option) => {
            setParty(option)
            setValue('partyId', option.value, { shouldValidate: true })
          }}
        />
      )}
    </div>
  )
}
