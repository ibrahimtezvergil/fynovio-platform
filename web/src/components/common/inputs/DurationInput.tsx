import { useTranslation } from 'react-i18next'
import { i18n } from '@/lib/i18n'
import { InlineSelect } from './InlineSelect'
import { NumberInput } from './NumberInput'
import { cn } from '@/lib/utils'
import type { FieldControlProps } from './types'

const MINUTE_STEPS = [0, 15, 30, 45] as const

export interface DurationInputProps extends FieldControlProps {
  /** Total minutes — the only unit that survives arithmetic without rounding. */
  value: number | null
  onValueChange: (value: number | null) => void
  maxHours?: number
  disabled?: boolean
  className?: string
}

/** `150` → `2 sa 30 dk`. Module-scope, so it reads whatever language is active at call time. */
export function formatDuration(minutes: number): string {
  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60
  const hoursUnit = i18n.t('durationInput.hoursUnit', { ns: 'common' })
  const minutesUnit = i18n.t('durationInput.minutesUnit', { ns: 'common' })
  if (hours === 0) return `${rest} ${minutesUnit}`
  return rest === 0 ? `${hours} ${hoursUnit}` : `${hours} ${hoursUnit} ${rest} ${minutesUnit}`
}

/**
 * Hours and minutes typed the way people say them, stored as a single minute
 * count. Billable time, service duration, a task estimate — anything that gets
 * summed later, which is exactly why it must not be stored as `"2:30"`.
 */
export function DurationInput({
  value,
  onValueChange,
  maxHours = 99,
  disabled,
  className,
  ...aria
}: DurationInputProps) {
  const { t } = useTranslation('common')
  const total = value ?? 0
  const hours = Math.floor(total / 60)
  const minutes = total % 60

  return (
    <div role="group" className={cn('flex items-center gap-2', className)} {...aria}>
      <NumberInput
        aria-label={t('durationInput.hours')}
        value={value === null ? null : hours}
        onValueChange={(next) => onValueChange((next ?? 0) * 60 + minutes)}
        min={0}
        max={maxHours}
        steppers
        suffix={t('durationInput.hoursUnit')}
        placeholder="0"
        disabled={disabled}
      />
      <NumberInput
        aria-label={t('durationInput.minutes')}
        value={value === null ? null : minutes}
        onValueChange={(next) => onValueChange(hours * 60 + Math.min(next ?? 0, 59))}
        min={0}
        max={59}
        step={5}
        suffix={t('durationInput.minutesUnit')}
        placeholder="0"
        disabled={disabled}
        trailing={
          <InlineSelect
            aria-label={t('durationInput.minuteQuickPick')}
            value={String(MINUTE_STEPS.includes(minutes as 0) ? minutes : 0)}
            onChange={(next) => onValueChange(hours * 60 + Number(next))}
            options={MINUTE_STEPS.map((step) => ({ value: String(step), label: `:${String(step).padStart(2, '0')}` }))}
          />
        }
      />
    </div>
  )
}
