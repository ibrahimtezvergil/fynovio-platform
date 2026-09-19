import { NumberInput } from './NumberInput'
import type { FieldControlProps } from './types'

export interface PercentInputProps extends FieldControlProps {
  value: number | null
  onValueChange: (value: number | null) => void
  /** Whole percentage points — `18` is 18%, not `0.18`. */
  min?: number
  max?: number
  step?: number | 'any'
  disabled?: boolean
  className?: string
}

/**
 * A percentage in whole points, so the value that leaves the control is the
 * one a user would say out loud. Converting to a ratio is the caller's job and
 * happens once, at the point of arithmetic.
 */
export function PercentInput({
  value,
  onValueChange,
  min = 0,
  max = 100,
  step = 'any',
  disabled,
  className,
  ...rest
}: PercentInputProps) {
  return (
    <NumberInput
      value={value}
      onValueChange={onValueChange}
      min={min}
      max={max}
      step={step}
      smallStep={0.1}
      largeStep={5}
      format={{ maximumFractionDigits: 2 }}
      suffix="%"
      placeholder="0"
      disabled={disabled}
      className={className}
      {...rest}
    />
  )
}
