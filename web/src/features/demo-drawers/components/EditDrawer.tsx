import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { Field } from '@/components/common/Field'
import { StatusBadge } from '@/components/common/StatusBadge'
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
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import {
  CUSTOMER_STATUS,
  CUSTOMER_STATUSES,
  type CustomerRecord,
} from '@/features/demo-drawers/data/customers'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet'
import { customerEditSchema, type CustomerEditValues } from '@/features/demo-drawers/schema'
import { OWNERS } from '@/features/demo-drawers/data/owners'
import { useSmartDefaults, useSmartDefaultsStore } from '@/store/useSmartDefaultsStore'

interface EditDrawerProps {
  record: CustomerRecord | null
  open: boolean
  onOpenChange: (open: boolean) => void
  onSave: (values: CustomerEditValues) => void
}

/**
 * The editing counterpart, with the guard that makes an editing drawer safe.
 *
 * A read-only panel can close on a backdrop click; an editing one cannot —
 * the same gesture that dismisses a preview would silently throw away typed
 * work. So the close request is intercepted while the form is dirty and
 * turned into a decision, and only a clean form closes straight away.
 */
export function EditDrawer({ record, open, onOpenChange, onSave }: EditDrawerProps) {
  const { t } = useTranslation('demo-drawers')
  const [confirming, setConfirming] = useState(false)
  const smartDefaults = useSmartDefaults<CustomerEditValues>('customer-edit')
  const rememberDefaults = useSmartDefaultsStore((state) => state.remember)

  const {
    register,
    handleSubmit,
    reset,
    watch,
    formState: { errors, isDirty },
  } = useForm<CustomerEditValues>({
    resolver: zodResolver(customerEditSchema),
    defaultValues: {
      name: '',
      owner: (smartDefaults.owner as string | undefined) ?? OWNERS[0],
      status: (smartDefaults.status as CustomerEditValues['status'] | undefined) ?? 'active',
      paymentTerm: '',
      note: '',
    },
  })

  // The drawer outlives the record it edits, so the form is re-seeded whenever
  // a different one arrives rather than only on mount.
  useEffect(() => {
    if (!record) return
    reset({
      name: record.name,
      owner: record.owner,
      status: record.status,
      paymentTerm: record.paymentTerm,
      note: record.note,
    })
  }, [record, reset])

  const status = watch('status')

  const requestClose = (next: boolean) => {
    if (!next && isDirty) {
      setConfirming(true)
      return
    }
    onOpenChange(next)
  }

  const submit = handleSubmit((values) => {
    // Only repetitive choices become defaults; record identity and notes must
    // never leak into a new customer's form.
    rememberDefaults('customer-edit', { owner: values.owner, status: values.status, paymentTerm: values.paymentTerm })
    onSave(values)
    toast.success(t('editDrawer.toastUpdated'), { description: values.name })
    onOpenChange(false)
  })

  return (
    <>
      <Sheet open={open} onOpenChange={requestClose}>
        <SheetContent className="gap-0 sm:max-w-lg">
          {/* The form element sits on the component that owns the submit, and
              the footer button reaches it by id — the footer is outside the
              scroll area, so it cannot be a descendant of the <form>. */}
          <SheetHeader>
            <SheetTitle>{t('editDrawer.title')}</SheetTitle>
            <SheetDescription>{t('editDrawer.description')}</SheetDescription>
          </SheetHeader>

          <form
            id="customer-edit-form"
            onSubmit={submit}
            noValidate
            className="flex flex-1 flex-col gap-4 overflow-y-auto px-5 py-5"
          >
            <Field label={t('editDrawer.fields.name')} error={errors.name?.message}>
              {(props) => <Input {...props} {...register('name')} />}
            </Field>

            <Field label={t('editDrawer.fields.owner')} error={errors.owner?.message}>
              {(props) => (
                <Select {...props} {...register('owner')}>
                  {OWNERS.map((owner) => (
                    <option key={owner} value={owner}>
                      {owner}
                    </option>
                  ))}
                </Select>
              )}
            </Field>

            <Field
              label={t('editDrawer.fields.status')}
              hint={t('editDrawer.fields.statusHint')}
            >
              {(props) => (
                <div className="flex items-center gap-3">
                  <Select {...props} {...register('status')} className="flex-1">
                    {CUSTOMER_STATUSES.map((value) => (
                      <option key={value} value={value}>
                        {CUSTOMER_STATUS[value].label}
                      </option>
                    ))}
                  </Select>
                  <StatusBadge {...CUSTOMER_STATUS[status]} />
                </div>
              )}
            </Field>

            <Field label={t('editDrawer.fields.paymentTerm')} error={errors.paymentTerm?.message}>
              {(props) => <Input {...props} {...register('paymentTerm')} />}
            </Field>

            <Field label={t('editDrawer.fields.note')} error={errors.note?.message}>
              {(props) => <Textarea {...props} rows={4} {...register('note')} />}
            </Field>
          </form>

          <SheetFooter>
            <Button variant="outline" onClick={() => requestClose(false)}>
              {t('editDrawer.cancel')}
            </Button>
            <Button type="submit" form="customer-edit-form" disabled={!isDirty}>
              {t('editDrawer.save')}
            </Button>
          </SheetFooter>
        </SheetContent>
      </Sheet>

      <AlertDialog open={confirming} onOpenChange={setConfirming}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('editDrawer.discardTitle')}</AlertDialogTitle>
            <AlertDialogDescription>{t('editDrawer.discardDescription')}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel render={<Button variant="outline" />}>
              {t('editDrawer.backToPanel')}
            </AlertDialogCancel>
            <AlertDialogAction
              render={<Button variant="destructive" />}
              onClick={() => {
                setConfirming(false)
                reset()
                onOpenChange(false)
              }}
            >
              {t('editDrawer.discardChanges')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  )
}
