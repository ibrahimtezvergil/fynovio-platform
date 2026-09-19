import type { TFunction } from 'i18next'
import { useTranslation } from 'react-i18next'
import { InlineSelect } from './InlineSelect'
import { NumberInput } from './NumberInput'
import type { FieldControlProps } from './types'

/** The units a stock card is actually kept in — stable codes; labels are translated. */
const UNIT_VALUES = [
  'adet',
  'kutu',
  'koli',
  'paket',
  'kg',
  'lt',
  'm',
  'm2',
  'saat',
  'gun',
] as const

export type Unit = (typeof UNIT_VALUES)[number]

/** The unit picker's options, labelled in the active language. */
export function quantityUnits(t: TFunction<'common'>): { value: Unit; label: string }[] {
  const labels = t('quantityInput.units', { returnObjects: true }) as Record<Unit, string>
  return UNIT_VALUES.map((value) => ({ value, label: labels[value] }))
}

export interface QuantityInputProps extends FieldControlProps {
  value: number | null
  onValueChange: (value: number | null) => void
  unit?: Unit
  /** Supply it and the unit becomes a picker welded into the same shell. */
  onUnitChange?: (unit: Unit) => void
  min?: number
  max?: number
  step?: number
  /** Whole units for pieces, two decimals for weights and hours. */
  decimals?: number
  disabled?: boolean
  className?: string
}

/**
 * Quantity with its unit of measure and hold-to-repeat steppers. Stock moves
 * in pieces far more often than in kilos, so `step` defaults to a whole unit
 * and only the caller who deals in weights opens up decimals.
 */
export function QuantityInput({
  value,
  onValueChange,
  unit = 'adet',
  onUnitChange,
  min = 0,
  max,
  step = 1,
  decimals = 0,
  disabled,
  className,
  ...rest
}: QuantityInputProps) {
  const { t } = useTranslation('common')
  const units = quantityUnits(t)
  return (
    <NumberInput
      value={value}
      onValueChange={onValueChange}
      min={min}
      max={max}
      step={decimals > 0 ? 'any' : step}
      smallStep={decimals > 0 ? 0.1 : 1}
      largeStep={10}
      format={{ maximumFractionDigits: decimals }}
      steppers
      placeholder="0"
      disabled={disabled}
      className={className}
      suffix={onUnitChange ? undefined : units.find((item) => item.value === unit)?.label}
      trailing={
        onUnitChange && (
          <InlineSelect
            aria-label={t('quantityInput.unit')}
            value={unit}
            onChange={(next) => onUnitChange(next as Unit)}
            options={units}
            disabled={disabled}
          />
        )
      }
      {...rest}
    />
  )
}
