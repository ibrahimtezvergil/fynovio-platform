import { useTranslation } from 'react-i18next'
import { InlineSelect } from './InlineSelect'
import { NumberInput } from './NumberInput'
import { CURRENCIES, CURRENCY_CODES, type CurrencyCode } from './currencies'
import type { FieldControlProps } from './types'

/** Two decimals, grouped — but only once the field is blurred and committed. */
const MONEY_FORMAT: Intl.NumberFormatOptions = {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
}

export interface MoneyInputProps extends FieldControlProps {
  value: number | null
  onValueChange: (value: number | null) => void
  currency?: CurrencyCode
  /** Supply it and the currency becomes a picker welded into the same shell. */
  onCurrencyChange?: (currency: CurrencyCode) => void
  currencies?: readonly CurrencyCode[]
  min?: number
  max?: number
  /** Refunds and credit notes need it; a price list does not. */
  allowNegative?: boolean
  placeholder?: string
  disabled?: boolean
  readOnly?: boolean
  className?: string
}

/**
 * An amount and its currency as one control.
 *
 * The amount is a real `number` at all times — never a string that has to be
 * un-formatted before arithmetic — and the field rounds it to two decimals on
 * blur through its own `Intl` format, which is what keeps a quote's line
 * totals from drifting a kuruş against the server's.
 */
export function MoneyInput({
  value,
  onValueChange,
  currency = 'TRY',
  onCurrencyChange,
  currencies = CURRENCY_CODES,
  min,
  max,
  allowNegative = false,
  placeholder,
  disabled,
  readOnly,
  className,
  ...rest
}: MoneyInputProps) {
  const { t } = useTranslation('common')
  const resolvedPlaceholder = placeholder ?? t('moneyInput.placeholder')
  return (
    <NumberInput
      value={value}
      onValueChange={onValueChange}
      min={min ?? (allowNegative ? undefined : 0)}
      max={max}
      step="any"
      smallStep={0.01}
      largeStep={100}
      format={MONEY_FORMAT}
      prefix={CURRENCIES[currency].symbol}
      placeholder={resolvedPlaceholder}
      disabled={disabled}
      readOnly={readOnly}
      className={className}
      trailing={
        onCurrencyChange && (
          <InlineSelect
            aria-label={t('moneyInput.currency')}
            value={currency}
            onChange={(next) => onCurrencyChange(next as CurrencyCode)}
            options={currencies.map((code) => ({ value: code, label: code }))}
            disabled={disabled || readOnly}
          />
        )
      }
      {...rest}
    />
  )
}
