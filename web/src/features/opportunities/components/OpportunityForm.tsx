import { zodResolver } from '@hookform/resolvers/zod'
import { Check, Loader2 } from 'lucide-react'
import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Field } from '@/components/common/Field'
import { CustomFieldInputs } from '@/components/custom-fields/CustomFieldInput'
import type { SelectOption } from '@/components/common/inputs/types'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { useCustomFieldDefinitions } from '@/lib/custom-fields/api'
import { customFieldErrors, requiredErrors } from '@/lib/custom-fields/errors'
import { activeFields, missingRequired, toPayload, type CustomFieldDrafts } from '@/lib/custom-fields/values'
import { cn } from '@/lib/utils'
import { paths } from '@/routes/paths'
import { useCreateOpportunity, useDefaultPipelineStages } from '../api'
import { useCrmSettings } from '@/features/crm-settings/api'
import { summarizeCreation } from '../lib/creationSummary'
import { useKeyedCommand } from '../lib/useKeyedCommand'
import { createOpportunityFormSchema, type CreateOpportunityInput, type CreateOpportunityValues, type PartyReference } from '../schema'
import { NewPartyDialog } from './NewPartyDialog'
import { OpportunityStartBlocker, OpportunityStartInfo } from './OpportunityStartSummary'
import { PartyPicker } from './PartyPicker'
import { ProblemNotice } from './ProblemNotice'
import { ReferenceFieldInput } from './ReferenceFieldInput'
import { SelectedParty } from './SelectedParty'

const WIZARD_STEPS = ['customer', 'amount'] as const

/**
 * Built from the real create contract and nothing more: customer, currency, estimated amount. Tenant and owner come from
 * the authenticated session and the workspace settings on the server, so neither is a field — the summary beside the form
 * only tells the person what those settings will do. The customer is chosen from the server-side Party search (never a raw id),
 * or added on the spot. The initial lifecycle (Draft, no stage) is the backend's; the entry stage is assigned by Open.
 * Wizard mode (a workspace setting) walks the same two facts, customer then amount — the summary beside the form is the review.
 */
export function OpportunityForm() {
  const { t } = useTranslation('opportunities')
  const navigate = useNavigate()
  const settings = useCrmSettings()
  const [step, setStep] = useState(0)
  // Set while the create-customer dialog is open; holds what was typed in the search so the dialog starts filled in.
  const [newPartyQuery, setNewPartyQuery] = useState<string | null>(null)
  const [partyRecord, setPartyRecord] = useState<PartyReference | null>(null)
  const isWizard = settings.data?.opportunityCreationMode === 'Wizard'
  const lastStep = WIZARD_STEPS.length - 1
  const onStep = (name: (typeof WIZARD_STEPS)[number]) => !isWizard || WIZARD_STEPS[step] === name
  const hasDefaultPipeline = settings.data?.defaultPipelineDefinitionId != null
  const defaultStages = useDefaultPipelineStages(hasDefaultPipeline)
  const summary = useMemo(() => summarizeCreation(settings.data, defaultStages.data), [settings.data, defaultStages.data])
  const blocked = summary.blocker !== null
  const schema = useMemo(() => createOpportunityFormSchema(t), [t])
  const createMutation = useCreateOpportunity()
  const command = useKeyedCommand(createMutation)
  const definitions = useCustomFieldDefinitions()
  const fields = useMemo(() => activeFields(definitions.data ?? []), [definitions.data])
  const [customDrafts, setCustomDrafts] = useState<CustomFieldDrafts>({})
  const [missing, setMissing] = useState<string[]>([])
  const customErrors = useMemo(
    () => ({ ...customFieldErrors(t, createMutation.error), ...requiredErrors(t, missing) }),
    [t, createMutation.error, missing],
  )
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
    const required = missingRequired(fields, customDrafts)
    setMissing(required)
    if (required.length > 0) return
    const result = await command.run(fields.length > 0 ? { ...values, customFields: toPayload(fields, customDrafts) } : values)
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

  const showButtons = !isWizard || step === lastStep
  const stepLabel = (name: (typeof WIZARD_STEPS)[number]) => t(`form.steps.${name}`)
  const canCreate = !command.isPending && !blocked
  const barMessage = blocked
    ? t('form.bar.blocked')
    : isWizard && step < lastStep
      ? t('form.stepOf', { current: step + 1, total: WIZARD_STEPS.length })
      : party ? t('form.bar.ready') : t('form.bar.pickCustomer')

  return (
    <>
      <form onSubmit={onSubmit} noValidate aria-label={t('form.title')} className="flex flex-col gap-5">
        {isWizard && (
          <ol className="bg-card/60 flex items-center gap-x-2 rounded-[var(--nx-r-card)] border px-3 py-2 text-[13px] sm:gap-x-3 sm:px-4" aria-label={t('form.stepOf', { current: step + 1, total: WIZARD_STEPS.length })}>
            {WIZARD_STEPS.map((name, index) => (
              <li key={name} className="flex items-center gap-2 sm:gap-3">
                {/* Any step is one click away: going back is always free, going forward checks the step being left. */}
                <button
                  type="button"
                  aria-current={index === step ? 'step' : undefined}
                  disabled={command.isPending}
                  onClick={() => index !== step && void go(index)}
                  className={cn('hover:bg-muted flex items-center gap-2 rounded-full py-1 pr-3 pl-1 transition-colors', index === step ? 'font-[600]' : 'text-muted-foreground')}
                >
                  <span className={cn('grid size-6 place-items-center rounded-full border text-[11.5px]', index === step && 'border-[var(--nx-tint)] bg-[var(--nx-tint)] text-white', index < step && 'border-[var(--nx-tint)] text-[var(--nx-tint)]')}>
                    {index < step ? <Check aria-hidden className="size-3.5" strokeWidth={2.4} /> : index + 1}
                  </span>
                  {stepLabel(name)}
                </button>
                {index < WIZARD_STEPS.length - 1 && <span aria-hidden className="bg-border h-px w-6 sm:w-10" />}
              </li>
            ))}
          </ol>
        )}

        {settings.data && <OpportunityStartBlocker summary={summary} />}

        <div ref={stepBody} className={cn('grid items-start gap-5', !isWizard && 'lg:grid-cols-2')}>
            {onStep('customer') && (
              <Card>
                <CardHeader>
                  <CardTitle>{t('form.sections.customer.title')}</CardTitle>
                  <CardDescription>{t('form.sections.customer.description')}</CardDescription>
                </CardHeader>
                <CardContent className="grid gap-4">
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
                            onPartyChange={setPartyRecord}
                            onCreate={setNewPartyQuery}
                          />
                        )}
                      />
                    )}
                  </Field>
                  {party && partyRecord && <SelectedParty party={partyRecord} />}
                </CardContent>
              </Card>
            )}

            {onStep('amount') && (
              <Card>
                <CardHeader>
                  <CardTitle>{t('form.sections.amount.title')}</CardTitle>
                  <CardDescription>{t('form.sections.amount.description')}</CardDescription>
                </CardHeader>
                <CardContent className="grid gap-4 sm:grid-cols-[minmax(0,11rem)_minmax(0,1fr)]">
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
                </CardContent>
              </Card>
            )}

            {fields.length > 0 && onStep('amount') && (
              <Card className="lg:col-span-full">
                <CardHeader>
                  <CardTitle>{t('customFields.title')}</CardTitle>
                  <CardDescription>{t('customFields.formDescription')}</CardDescription>
                </CardHeader>
                <CardContent>
                  <CustomFieldInputs
                    definitions={fields}
                    drafts={customDrafts}
                    errors={customErrors}
                    disabled={command.isPending}
                    renderReference={(props) => <ReferenceFieldInput {...props} />}
                    onChange={(drafts) => { setCustomDrafts(drafts); setMissing([]) }}
                  />
                </CardContent>
              </Card>
            )}

            {command.problem && <div className="lg:col-span-full"><ProblemNotice problem={command.problem} /></div>}
        </div>

        <div className="nx-material sticky bottom-4 z-[4] flex flex-wrap items-center gap-3 rounded-[var(--nx-r-card)] px-5 py-3.5">
          {settings.data && <OpportunityStartInfo summary={summary} loading={hasDefaultPipeline && defaultStages.isPending} />}
          <span role="status" className="text-muted-foreground min-w-48 flex-1 text-[12.5px]">{barMessage}</span>
          <Button type="button" variant="ghost" disabled={command.isPending} onClick={() => navigate(paths.crmOpportunities)}>
            {t('common.cancel')}
          </Button>
          {isWizard && step > 0 && <Button type="button" variant="outline" disabled={command.isPending} onClick={() => void go(step - 1)}>{t('form.previousStep')}</Button>}
          {isWizard && step < lastStep && <Button type="button" onClick={() => void go(step + 1)}>{t('form.next')}</Button>}
          {showButtons && (
            <>
              <Button type="button" variant="outline" disabled={!canCreate} onClick={(event) => void submitAndAddLine(event)}>
                {t('form.submitAndAddLine')}
              </Button>
              <Button type="submit" disabled={!canCreate}>
                {command.isPending && <Loader2 aria-hidden className="animate-spin" />}
                {command.isPending ? t('form.submitting') : t('form.submit')}
              </Button>
            </>
          )}
        </div>
      </form>

      {/* Outside the <form>: a portal's submit event still bubbles through the React tree and would submit the opportunity. */}
      {newPartyQuery !== null && (
        <NewPartyDialog
          initialQuery={newPartyQuery}
          onClose={() => setNewPartyQuery(null)}
          onCreated={(created) => {
            setParty({ value: String(created.id), label: created.displayName })
            setPartyRecord(created)
            setValue('partyId', String(created.id), { shouldValidate: true })
          }}
        />
      )}
    </>
  )
}
