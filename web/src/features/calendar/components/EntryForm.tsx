import { zodResolver } from '@hookform/resolvers/zod'
import { Link2, Loader2, X } from 'lucide-react'
import { parseISO } from 'date-fns'
import { useId, useMemo, useState } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import { ColorInput } from '@/components/common/inputs/ColorInput'
import { DatePicker } from '@/components/common/inputs/DatePicker'
import { Button } from '@/components/ui/button'
import { DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { useCreateEntry, useReplaceEntry } from '../api'
import { entryFormSchema, type EntryFormInput, type EntryFormValues } from '../lib/form'
import { linkFallbackLabel } from '../lib/links'
import type { Problem } from '../lib/problem'
import { useKeyedCommand } from '../lib/useKeyedCommand'
import { defaultFields, fieldsFromEntry, formatIsoDate, timingFromFields, type TimingFields } from '../lib/time'
import type { CalendarEntry, CalendarLinkRef } from '../schema'
import { ProblemNotice } from './ProblemNotice'

export const DEFAULT_COLOR = '#3c8cf0'

export interface EntryPrefill {
  fields?: TimingFields
  link?: CalendarLinkRef
}

interface EntryFormProps {
  /** Present when editing; absent when creating. */
  entry?: CalendarEntry
  prefill?: EntryPrefill
  /** Called after the server confirmed the save. */
  onDone: () => void
  onCancel: () => void
  onReload: () => void
}

/** What the chip shows for the link the form currently holds. `ref` is what is sent; the label is display only. */
interface HeldLink {
  ref: CalendarLinkRef
  /** `undefined` for an `unavailable` link: the caller may not see a name. */
  label?: string
  unavailable: boolean
}

function initialLink(entry: CalendarEntry | undefined, prefill: EntryPrefill | undefined): HeldLink | null {
  if (entry?.link) return { ref: entry.link.ref, label: entry.link.label, unavailable: entry.link.state !== 'accessible' }
  return !entry && prefill?.link ? { ref: prefill.link, unavailable: false } : null
}

const toDate = (isoDate: string) => (isoDate ? parseISO(isoDate) : null)

/**
 * Create or edit one entry (a `PUT` replaces every field, so the whole form is always sent). Each save attempt is one
 * logical action: its idempotency key is held while the outcome is unknown (network failure, 5xx) and released on any
 * definitive answer, so pressing Save again after a dropped connection replays instead of creating a duplicate.
 */
export function EntryForm({ entry, prefill, onDone, onCancel, onReload }: EntryFormProps) {
  const { t } = useTranslation('calendar')
  const schema = useMemo(() => entryFormSchema(t), [t])
  const createCommand = useKeyedCommand(useCreateEntry())
  const replaceCommand = useKeyedCommand(useReplaceEntry())
  const command = entry ? replaceCommand : createCommand
  const [link, setLink] = useState<HeldLink | null>(() => initialLink(entry, prefill))
  const allDayLabelId = useId()

  const initial = entry ? fieldsFromEntry(entry) : (prefill?.fields ?? defaultFields())
  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<EntryFormInput, unknown, EntryFormValues>({
    resolver: zodResolver(schema),
    defaultValues: { title: entry?.title ?? '', notes: entry?.notes ?? '', color: entry?.color ?? DEFAULT_COLOR, ...initial },
  })
  const allDay = useWatch({ control, name: 'allDay' })

  const submit = handleSubmit(async (values) => {
    const body = {
      title: values.title,
      notes: values.notes.trim() === '' ? null : values.notes,
      color: values.color,
      ...timingFromFields(values),
      link: link ? { ...link.ref } : null,
    }
    const result = entry
      ? await replaceCommand.run({ id: entry.id, expectedVersion: entry.rowVersion, body })
      : await createCommand.run({ body })
    if (result) onDone()
  })

  const problem: Problem | null = command.problem
  const heading = entry ? t('form.editTitle') : t('form.createTitle')

  return (
    <form onSubmit={submit} noValidate className="grid gap-4">
      <DialogHeader>
        <DialogTitle className="pr-8">{heading}</DialogTitle>
        <DialogDescription>{entry ? t('form.editDescription') : t('form.createDescription')}</DialogDescription>
      </DialogHeader>

      <Field label={t('form.title.label')} error={errors.title?.message}>
        {(props) => <Input {...props} autoComplete="off" maxLength={400} {...register('title')} />}
      </Field>

      <div className="flex items-center justify-between gap-3">
        <span id={allDayLabelId} className="text-[13px] font-[550]">
          {t('form.allDay')}
        </span>
        <Controller
          control={control}
          name="allDay"
          render={({ field }) => <Switch checked={field.value} onCheckedChange={field.onChange} aria-labelledby={allDayLabelId} />}
        />
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={allDay ? t('form.firstDay') : t('form.startDate')} error={errors.startDate?.message}>
          {(props) => (
            <Controller
              control={control}
              name="startDate"
              render={({ field }) => (
                <DatePicker {...props} clearable={false} value={toDate(field.value)} onValueChange={(date) => field.onChange(date ? formatIsoDate(date) : '')} />
              )}
            />
          )}
        </Field>
        {!allDay && (
          <Field label={t('form.startTime')} error={errors.startTime?.message}>
            {(props) => <Input {...props} type="time" step={60} {...register('startTime')} />}
          </Field>
        )}
        <Field label={allDay ? t('form.lastDay') : t('form.endDate')} error={errors.endDate?.message}>
          {(props) => (
            <Controller
              control={control}
              name="endDate"
              render={({ field }) => (
                <DatePicker {...props} clearable={false} value={toDate(field.value)} onValueChange={(date) => field.onChange(date ? formatIsoDate(date) : '')} />
              )}
            />
          )}
        </Field>
        {!allDay && (
          <Field label={t('form.endTime')} hint={t('form.endTimeHint')} error={errors.endTime?.message}>
            {(props) => <Input {...props} type="time" step={60} {...register('endTime')} />}
          </Field>
        )}
      </div>

      <Field label={t('form.color.label')} error={errors.color?.message}>
        {(props) => (
          <Controller
            control={control}
            name="color"
            render={({ field }) => <ColorInput {...props} value={field.value} onValueChange={(color) => field.onChange(color.toLowerCase())} />}
          />
        )}
      </Field>

      <Field label={t('form.notes.label')} error={errors.notes?.message}>
        {(props) => <Textarea {...props} rows={3} {...register('notes')} />}
      </Field>

      {link && (
        <fieldset className="m-0 grid min-w-0 gap-1.5 border-0 p-0">
          <legend className="text-muted-foreground p-0 text-[12.5px] font-[550]">{t('form.link.label')}</legend>
          <span className="border-[var(--nx-hairline)] bg-[var(--nx-fill)] inline-flex max-w-full items-center gap-2 self-start rounded-md border py-1 pr-1 pl-2.5 text-[13px]">
            <Link2 aria-hidden className="text-muted-foreground size-3.5 shrink-0" />
            <span className="truncate">{link.unavailable ? t('link.unavailable') : (link.label ?? linkFallbackLabel(t, link.ref))}</span>
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              aria-label={t('form.link.remove')}
              disabled={command.isPending}
              onClick={() => setLink(null)}
            >
              <X aria-hidden />
            </Button>
          </span>
        </fieldset>
      )}

      {problem && <ProblemNotice problem={problem} onReload={entry ? onReload : undefined} />}

      <DialogFooter>
        <Button type="button" variant="ghost" disabled={command.isPending} onClick={onCancel}>
          {t('cancel')}
        </Button>
        <Button type="submit" disabled={command.isPending}>
          {command.isPending && <Loader2 aria-hidden className="animate-spin" />}
          {command.isPending ? t('form.saving') : t('form.save')}
        </Button>
      </DialogFooter>
    </form>
  )
}
