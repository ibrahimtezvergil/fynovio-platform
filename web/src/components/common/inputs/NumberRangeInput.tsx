import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { NumberInput } from './NumberInput'
import type { FieldControlProps } from './types'

export interface NumberRange {
  min: number | null
  max: number | null
}

export interface NumberRangeInputProps extends FieldControlProps {
  value: NumberRange
  onValueChange: (value: NumberRange) => void
  /** Bounds of the whole range, not of one end. */
  min?: number
  max?: number
  step?: number | 'any'
  format?: Intl.NumberFormatOptions
  prefix?: string
  suffix?: string
  disabled?: boolean
  className?: string
}

/**
 * A min and a max as one value, for the filters where a slider is too coarse
 * to type an exact figure into. Each end bounds the other, so the range can
 * never be entered inverted and no downstream query has to defend against it.
 */
export function NumberRangeInput({
  value,
  onValueChange,
  min,
  max,
  step = 'any',
  format,
  prefix,
  suffix,
  disabled,
  className,
  ...aria
}: NumberRangeInputProps) {
  const { t } = useTranslation('common')
  return (
    <div
      role="group"
      className={cn('flex items-center gap-2', className)}
      {...aria}
    >
      <NumberInput
        aria-label={t('numberRangeInput.lowerBound')}
        value={value.min}
        onValueChange={(next) => onValueChange({ ...value, min: next })}
        min={min}
        max={value.max ?? max}
        step={step}
        format={format}
        prefix={prefix}
        suffix={suffix}
        placeholder={t('numberRangeInput.min')}
        disabled={disabled}
      />
      <span aria-hidden className="text-muted-foreground shrink-0 text-[13px]">
        –
      </span>
      <NumberInput
        aria-label={t('numberRangeInput.upperBound')}
        value={value.max}
        onValueChange={(next) => onValueChange({ ...value, max: next })}
        min={value.min ?? min}
        max={max}
        step={step}
        format={format}
        prefix={prefix}
        suffix={suffix}
        placeholder={t('numberRangeInput.max')}
        disabled={disabled}
      />
    </div>
  )
}
