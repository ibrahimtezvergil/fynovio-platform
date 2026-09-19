import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { RichSelect } from './RichSelect'
import type { FieldControlProps, SelectOption } from './types'

export interface CascadeLevel {
  /** Accessible name for this step — "İl", "İlçe", "Mahalle". */
  label: string
  placeholder?: string
  /** The options this level offers, given everything chosen above it. */
  optionsFor: (path: readonly (string | null)[]) => readonly SelectOption[]
}

export interface CascadingSelectProps extends FieldControlProps {
  levels: readonly CascadeLevel[]
  /** One entry per level, `null` where nothing is chosen yet. */
  value: readonly (string | null)[]
  onValueChange: (value: (string | null)[]) => void
  disabled?: boolean
  className?: string
}

/**
 * Dependent selects in a row: province → district → neighbourhood, family →
 * category → sub-category.
 *
 * Choosing at one level clears every level below it. Leaving a stale district
 * under a changed province is the failure this control exists to prevent — it
 * is invisible on screen and only surfaces as a bad address on a delivery note.
 */
export function CascadingSelect({
  levels,
  value,
  onValueChange,
  disabled,
  className,
  ...aria
}: CascadingSelectProps) {
  const { t } = useTranslation('common')
  const select = (index: number, next: string) => {
    const updated = levels.map((_, level) =>
      level < index ? (value[level] ?? null) : level === index ? next : null,
    )
    onValueChange(updated)
  }

  return (
    <div
      role="group"
      className={cn('grid gap-2.5 sm:grid-cols-3', className)}
      {...aria}
    >
      {levels.map((level, index) => {
        const path = value.slice(0, index)
        const ready = index === 0 || value[index - 1] != null
        const options = ready ? level.optionsFor(path) : []
        return (
          <RichSelect
            key={level.label}
            aria-label={level.label}
            value={value[index] ?? null}
            onValueChange={(next) => select(index, next)}
            options={options}
            placeholder={
              ready
                ? (level.placeholder ?? level.label)
                : t('cascadingSelect.chooseFirst', { level: levels[index - 1].label })
            }
            disabled={disabled || !ready}
          />
        )
      })}
    </div>
  )
}
