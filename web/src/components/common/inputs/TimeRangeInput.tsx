import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { CONTROL_INPUT, CONTROL_SHELL } from './styles'
import type { FieldControlProps } from './types'

export interface TimeRange {
  /** `HH:mm`, the only form a native time input speaks. */
  start: string
  end: string
}

export interface TimeRangeInputProps extends FieldControlProps {
  value: TimeRange
  onValueChange: (value: TimeRange) => void
  /** Quarter-hour granularity by default; shifts run on the clock, not the second. */
  step?: number
  disabled?: boolean
  className?: string
}

/** Minutes between the two ends, or `null` while either is empty. */
export function timeRangeMinutes({ start, end }: TimeRange): number | null {
  if (!start || !end) return null
  const [startHour, startMinute] = start.split(':').map(Number)
  const [endHour, endMinute] = end.split(':').map(Number)
  return endHour * 60 + endMinute - (startHour * 60 + startMinute)
}

/**
 * Working hours, a meeting slot, a delivery window. The end is bounded by the
 * start through the native `min` attribute, so an inverted range never leaves
 * the control — an overnight shift is a different control, not this one.
 */
export function TimeRangeInput({
  value,
  onValueChange,
  step = 900,
  disabled,
  className,
  id,
  ...aria
}: TimeRangeInputProps) {
  const { t } = useTranslation('common')
  const invalid = (timeRangeMinutes(value) ?? 1) <= 0

  return (
    <div
      className={cn(CONTROL_SHELL, 'items-center gap-1 px-1.5', className)}
      aria-invalid={invalid || undefined}
      role="group"
      {...aria}
    >
      <input
        id={id}
        type="time"
        aria-label={t('timeRangeInput.startTime')}
        value={value.start}
        step={step}
        disabled={disabled}
        onChange={(event) => onValueChange({ ...value, start: event.target.value })}
        className={cn(CONTROL_INPUT, 'tnum px-1.5 text-center')}
      />
      <span aria-hidden className="text-muted-foreground shrink-0 text-[13px]">
        –
      </span>
      <input
        type="time"
        aria-label={t('timeRangeInput.endTime')}
        value={value.end}
        min={value.start || undefined}
        step={step}
        disabled={disabled}
        onChange={(event) => onValueChange({ ...value, end: event.target.value })}
        className={cn(CONTROL_INPUT, 'tnum px-1.5 text-center')}
      />
    </div>
  )
}
