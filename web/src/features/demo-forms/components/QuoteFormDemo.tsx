import { zodResolver } from '@hookform/resolvers/zod'
import { ClipboardCheck, Undo2 } from 'lucide-react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { Field } from '@/components/common/Field'
import {
  ComboboxInput,
  DatePicker,
  DiscountInput,
  MoneyInput,
  PhoneInput,
  TagInput,
  TaxIdInput,
  formatMoney,
  resolveDiscount,
  type CurrencyCode,
} from '@/components/common/inputs'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { DemoSection } from '@/components/common/DemoSection'
import { CUSTOMERS } from '@/features/demo-forms/data/options'
import { quoteSchema, type QuoteDraft } from '@/features/demo-forms/schema'
import { useDraftGuard } from '@/lib/drafts/useDraftGuard'

const EMPTY: QuoteDraft = {
  customer: null,
  taxId: '',
  phone: '',
  amount: null,
  currency: 'TRY',
  discount: { mode: 'percent', value: null },
  closeDate: null,
  tags: [],
  terms: false,
}

/**
 * The kit under a real form. Each control goes through `Controller` because it
 * owns a value that is not a DOM string — a `Date`, a `number | null`, a
 * discount object — and `Field` wires the label, the hint and the error id.
 */
export function QuoteFormDemo() {
  const { t } = useTranslation('demo-forms')
  const {
    control,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<QuoteDraft>({
    resolver: zodResolver(quoteSchema),
    defaultValues: EMPTY,
    mode: 'onBlur',
  })

  const draft = useDraftGuard({ key: 'demo-forms/quote', control, isDirty, reset })

  // Scoped subscriptions rather than `watch()`: only the three fields the
  // summary line reads re-render this component.
  const amount = useWatch({ control, name: 'amount' }) ?? 0
  const currency = useWatch({ control, name: 'currency' }) as CurrencyCode
  const discount = useWatch({ control, name: 'discount' })
  const net = amount - resolveDiscount(amount, discount)

  const onSubmit = handleSubmit((values) => {
    toast.success(t('quoteFormDemo.toast.title'), {
      description: t('quoteFormDemo.toast.description', {
        customer: values.customer,
        amount: formatMoney(net, currency),
      }),
    })
    draft.discardDraft()
  })

  return (
    <>
      <DemoSection
        id="form"
        title={t('quoteFormDemo.title')}
        description={t('quoteFormDemo.description')}
        icon={ClipboardCheck}
      >
        {draft.hasSavedDraft && !isDirty && (
          <Alert variant="info" className="lg:col-span-2">
            <Undo2 aria-hidden />
            <AlertTitle>{t('quoteFormDemo.draft.title')}</AlertTitle>
            <AlertDescription>{t('quoteFormDemo.draft.description')}</AlertDescription>
            <div className="col-start-2 mt-2 flex gap-2">
              <Button size="sm" variant="outline" onClick={() => draft.discardDraft()}>
                {t('quoteFormDemo.draft.discard')}
              </Button>
              <Button size="sm" onClick={() => draft.restoreDraft()}>
                {t('quoteFormDemo.draft.restore')}
              </Button>
            </div>
          </Alert>
        )}

        <form onSubmit={onSubmit} noValidate className="contents">
          <Controller
            control={control}
            name="customer"
            render={({ field }) => (
              <Field label={t('quoteFormDemo.fields.customer')} error={errors.customer?.message}>
                {(props) => (
                  <ComboboxInput
                    {...props}
                    value={field.value}
                    onValueChange={field.onChange}
                    options={CUSTOMERS}
                  />
                )}
              </Field>
            )}
          />

          <Controller
            control={control}
            name="taxId"
            render={({ field }) => (
              <Field label={t('quoteFormDemo.fields.taxId')} error={errors.taxId?.message}>
                {(props) => (
                  <TaxIdInput {...props} value={field.value} onValueChange={field.onChange} />
                )}
              </Field>
            )}
          />

          <Controller
            control={control}
            name="phone"
            render={({ field }) => (
              <Field label={t('quoteFormDemo.fields.phone')} error={errors.phone?.message}>
                {(props) => (
                  <PhoneInput {...props} value={field.value} onValueChange={field.onChange} />
                )}
              </Field>
            )}
          />

          <Controller
            control={control}
            name="amount"
            render={({ field }) => (
              <Field label={t('quoteFormDemo.fields.amount')} error={errors.amount?.message}>
                {(props) => (
                  <Controller
                    control={control}
                    name="currency"
                    render={({ field: currencyField }) => (
                      <MoneyInput
                        {...props}
                        value={field.value}
                        onValueChange={field.onChange}
                        currency={currencyField.value as CurrencyCode}
                        onCurrencyChange={currencyField.onChange}
                      />
                    )}
                  />
                )}
              </Field>
            )}
          />

          <Controller
            control={control}
            name="discount"
            render={({ field }) => (
              <Field
                label={t('quoteFormDemo.fields.discount')}
                hint={amount > 0 ? t('quoteFormDemo.netAmountHint', { amount: formatMoney(net, currency) }) : undefined}
                error={errors.discount?.message}
              >
                {(props) => (
                  <DiscountInput
                    {...props}
                    discount={field.value}
                    onDiscountChange={field.onChange}
                    currency={currency}
                    base={amount}
                  />
                )}
              </Field>
            )}
          />

          <Controller
            control={control}
            name="closeDate"
            render={({ field }) => (
              <Field label={t('quoteFormDemo.fields.closeDate')} error={errors.closeDate?.message}>
                {(props) => (
                  <DatePicker
                    {...props}
                    value={field.value}
                    onValueChange={field.onChange}
                    min={new Date()}
                  />
                )}
              </Field>
            )}
          />

          <Controller
            control={control}
            name="tags"
            render={({ field }) => (
              <Field label={t('quoteFormDemo.fields.tags')} hint={t('quoteFormDemo.tagsHint')} error={errors.tags?.message}>
                {(props) => (
                  <TagInput
                    {...props}
                    value={field.value}
                    onValueChange={field.onChange}
                    maxTags={5}
                  />
                )}
              </Field>
            )}
          />

          <div className="flex flex-col justify-end gap-4 lg:col-span-2">
            <Controller
              control={control}
              name="terms"
              render={({ field }) => (
                <div className="flex flex-col gap-1.5">
                  <label className="flex items-start gap-2.5 text-[13.5px]">
                    <Checkbox
                      checked={field.value}
                      onChange={(event) => field.onChange(event.target.checked)}
                      aria-invalid={errors.terms ? true : undefined}
                    />
                    {t('quoteFormDemo.termsLabel')}
                  </label>
                  {errors.terms && (
                    <span role="alert" className="text-[11.5px] text-[var(--nx-neg)]">
                      {errors.terms.message}
                    </span>
                  )}
                </div>
              )}
            />

            <div className="flex flex-wrap items-center gap-2.5">
              <Button type="submit" disabled={isSubmitting}>
                {t('quoteFormDemo.submit')}
              </Button>
              <Button
                type="button"
                variant="ghost"
                onClick={() => {
                  reset(EMPTY)
                  draft.discardDraft()
                }}
              >
                {t('quoteFormDemo.reset')}
              </Button>
              <span className="text-muted-foreground text-[11.5px]">
                {t('quoteFormDemo.helperText')}
              </span>
            </div>
          </div>
        </form>
      </DemoSection>

      <AlertDialog open={draft.blocker.state === 'blocked'}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('quoteFormDemo.draft.leaveTitle')}</AlertDialogTitle>
            <AlertDialogDescription>{t('quoteFormDemo.draft.leaveDescription')}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel
              render={<Button variant="outline" />}
              onClick={() => draft.blocker.state === 'blocked' && draft.blocker.reset()}
            >
              {t('quoteFormDemo.draft.stay')}
            </AlertDialogCancel>
            <AlertDialogAction
              render={<Button variant="destructive" />}
              onClick={() => draft.blocker.state === 'blocked' && draft.blocker.proceed()}
            >
              {t('quoteFormDemo.draft.leave')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  )
}
