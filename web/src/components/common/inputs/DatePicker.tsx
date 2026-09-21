import { Popover } from '@base-ui/react/popover'
import type { TFunction } from 'i18next'
import { CalendarDays, X } from 'lucide-react'
import { useState } from 'react'
import type { DateRange } from 'react-day-picker'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Calendar } from './Calendar'
import { CONTROL_SHELL, POPUP } from './styles'
import type { FieldControlProps } from './types'

const DATE_STYLES = {
  long: { day: '2-digit', month: 'long', year: 'numeric' },
  short: { day: '2-digit', month: '2-digit', year: 'numeric' },
} satisfies Record<string, Intl.DateTimeFormatOptions>

const formatters = new Map<string, Intl.DateTimeFormat>()

/** The UI languages are Turkish (the default) and English; anything else reads as Turkish, like the rest of the app. */
const intlLocale = (language: string) => (language.toLowerCase().startsWith('en') ? 'en-US' : 'tr-TR')

function formatWith(style: keyof typeof DATE_STYLES, date: Date, language: string) {
  const locale = intlLocale(language)
  const key = `${locale}:${style}`
  let formatter = formatters.get(key)
  if (!formatter) {
    formatter = new Intl.DateTimeFormat(locale, DATE_STYLES[style])
    formatters.set(key, formatter)
  }
  return formatter.format(date)
}

/** `language` is the active i18n language; it defaults to Turkish so callers outside a component keep their output. */
export const formatDate = (date: Date, language = 'tr') => formatWith('long', date, language)
export const formatDateShort = (date: Date, language = 'tr') => formatWith('short', date, language)

const TRIGGER = [
  'cursor-pointer items-center gap-2.5 px-[13px] text-left text-[13.5px] outline-none',
  'data-popup-open:border-ring data-popup-open:bg-[var(--nx-surface)]',
  'disabled:pointer-events-none disabled:opacity-45',
].join(' ')

const CLEAR_BUTTON = [
  'absolute inset-y-0 right-0 flex w-9 cursor-pointer items-center justify-center',
  'border-0 bg-transparent text-muted-foreground outline-none hover:text-foreground',
].join(' ')

export interface DatePickerProps extends FieldControlProps {
  value: Date | null
  onValueChange: (value: Date | null) => void
  placeholder?: string
  /** Bounds the grid — a delivery date cannot fall before the order date. */
  min?: Date
  max?: Date
  clearable?: boolean
  disabled?: boolean
  className?: string
}

/** A single date, picked from a month grid rather than typed into three boxes. */
export function DatePicker({
  value,
  onValueChange,
  placeholder,
  min,
  max,
  clearable = true,
  disabled,
  className,
  id,
  ...aria
}: DatePickerProps) {
  const { t, i18n } = useTranslation('common')
  const resolvedPlaceholder = placeholder ?? t('datePicker.placeholder')
  const [open, setOpen] = useState(false)

  return (
    <div className={cn('relative', className)}>
      <Popover.Root open={open} onOpenChange={setOpen}>
        <Popover.Trigger
          id={id}
          disabled={disabled}
          className={cn(CONTROL_SHELL, TRIGGER, value && clearable && 'pr-9')}
          {...aria}
        >
          <CalendarDays
            aria-hidden
            className="text-muted-foreground size-4 shrink-0"
            strokeWidth={1.75}
          />
          <span className={cn('truncate', !value && 'text-[var(--nx-label-3)]')}>
            {value ? formatDate(value, i18n.language) : resolvedPlaceholder}
          </span>
        </Popover.Trigger>

        <Popover.Portal>
          <Popover.Positioner sideOffset={6} align="start" className="z-50 outline-none">
            <Popover.Popup className={cn(POPUP, 'p-3')}>
              <Calendar
                mode="single"
                selected={value ?? undefined}
                defaultMonth={value ?? undefined}
                startMonth={min}
                endMonth={max}
                disabled={[
                  ...(min ? [{ before: min }] : []),
                  ...(max ? [{ after: max }] : []),
                ]}
                onSelect={(date) => {
                  onValueChange(date ?? null)
                  if (date) setOpen(false)
                }}
              />
            </Popover.Popup>
          </Popover.Positioner>
        </Popover.Portal>
      </Popover.Root>

      {clearable && value && !disabled && (
        <button
          type="button"
          aria-label={t('datePicker.clear')}
          onClick={() => onValueChange(null)}
          className={CLEAR_BUTTON}
        >
          <X aria-hidden className="size-4" strokeWidth={1.75} />
        </button>
      )}
    </div>
  )
}

/* ---- range --------------------------------------------------------------- */

function startOfDay(date: Date): Date {
  const copy = new Date(date)
  copy.setHours(0, 0, 0, 0)
  return copy
}

function addDays(date: Date, days: number): Date {
  const copy = startOfDay(date)
  copy.setDate(copy.getDate() + days)
  return copy
}

/** The ranges every report filter offers, relative to today. */
export function rangePresets(
  t: TFunction<'common'>,
  today = new Date(),
): { label: string; range: DateRange }[] {
  const base = startOfDay(today)
  const monthStart = new Date(base.getFullYear(), base.getMonth(), 1)
  const quarterStart = new Date(base.getFullYear(), Math.floor(base.getMonth() / 3) * 3, 1)
  return [
    { label: t('datePicker.presets.today'), range: { from: base, to: base } },
    { label: t('datePicker.presets.last7Days'), range: { from: addDays(base, -6), to: base } },
    { label: t('datePicker.presets.last30Days'), range: { from: addDays(base, -29), to: base } },
    { label: t('datePicker.presets.thisMonth'), range: { from: monthStart, to: base } },
    { label: t('datePicker.presets.thisQuarter'), range: { from: quarterStart, to: base } },
    {
      label: t('datePicker.presets.thisYear'),
      range: { from: new Date(base.getFullYear(), 0, 1), to: base },
    },
  ]
}

export interface DateRangePickerProps extends FieldControlProps {
  value: DateRange | null
  onValueChange: (value: DateRange | null) => void
  placeholder?: string
  /** Presets carry most of the traffic; the grid is for the exceptions. */
  presets?: { label: string; range: DateRange }[]
  disabled?: boolean
  className?: string
}

/** A start and an end as one value, with the ranges people actually ask for. */
export function DateRangePicker({
  value,
  onValueChange,
  placeholder,
  presets,
  disabled,
  className,
  id,
  ...aria
}: DateRangePickerProps) {
  const { t, i18n } = useTranslation('common')
  const resolvedPlaceholder = placeholder ?? t('datePicker.rangePlaceholder')
  const resolvedPresets = presets ?? rangePresets(t)
  const [open, setOpen] = useState(false)

  const label = value?.from
    ? value.to
      ? `${formatDateShort(value.from, i18n.language)} – ${formatDateShort(value.to, i18n.language)}`
      : `${formatDateShort(value.from, i18n.language)} – …`
    : null

  return (
    <div className={cn('relative', className)}>
      <Popover.Root open={open} onOpenChange={setOpen}>
        <Popover.Trigger
          id={id}
          disabled={disabled}
          className={cn(CONTROL_SHELL, TRIGGER, value?.from && 'pr-9')}
          {...aria}
        >
          <CalendarDays
            aria-hidden
            className="text-muted-foreground size-4 shrink-0"
            strokeWidth={1.75}
          />
          <span className={cn('tnum truncate', !label && 'text-[var(--nx-label-3)]')}>
            {label ?? resolvedPlaceholder}
          </span>
        </Popover.Trigger>

        <Popover.Portal>
          <Popover.Positioner sideOffset={6} align="start" className="z-50 outline-none">
            <Popover.Popup className={cn(POPUP, 'flex gap-3 p-3')}>
              <div className="flex w-36 shrink-0 flex-col gap-0.5 border-r border-[var(--nx-hairline)] pr-3">
                {resolvedPresets.map((preset) => (
                  <button
                    key={preset.label}
                    type="button"
                    onClick={() => {
                      onValueChange(preset.range)
                      setOpen(false)
                    }}
                    className="text-muted-foreground hover:bg-[var(--nx-fill-hover)] hover:text-foreground flex h-8 cursor-pointer items-center rounded-sm border-0 bg-transparent px-2.5 text-left text-[12.5px] outline-none"
                  >
                    {preset.label}
                  </button>
                ))}
              </div>
              <Calendar
                mode="range"
                numberOfMonths={2}
                selected={value ?? undefined}
                defaultMonth={value?.from ?? undefined}
                onSelect={(range) => onValueChange(range ?? null)}
              />
            </Popover.Popup>
          </Popover.Positioner>
        </Popover.Portal>
      </Popover.Root>

      {value?.from && !disabled && (
        <button
          type="button"
          aria-label={t('datePicker.clearRange')}
          onClick={() => onValueChange(null)}
          className={CLEAR_BUTTON}
        >
          <X aria-hidden className="size-4" strokeWidth={1.75} />
        </button>
      )}
    </div>
  )
}
