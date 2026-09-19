import { ArrowRightLeft, RefreshCw } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { MoneyInput } from './MoneyInput'
import { NumberInput } from './NumberInput'
import { CURRENCIES, formatMoney, roundMoney, type CurrencyCode } from './currencies'
import type { FieldControlProps } from './types'

export interface ExchangeValue {
  amount: number | null
  currency: CurrencyCode
  /** Units of `baseCurrency` per one `currency`. Always 1 when they match. */
  rate: number | null
}

export interface ExchangeRateInputProps extends FieldControlProps {
  value: ExchangeValue
  onValueChange: (value: ExchangeValue) => void
  /** The books' own currency — what the converted figure is posted in. */
  baseCurrency?: CurrencyCode
  currencies?: readonly CurrencyCode[]
  /** Today's published rate, offered as a one-click fill. */
  suggestedRates?: Partial<Record<CurrencyCode, number>>
  disabled?: boolean
  className?: string
}

/** The document's value in the base currency — the number that gets posted. */
export function convertedAmount(value: ExchangeValue, baseCurrency: CurrencyCode): number {
  if (value.currency === baseCurrency) return roundMoney(value.amount ?? 0)
  return roundMoney((value.amount ?? 0) * (value.rate ?? 0))
}

/**
 * A foreign-currency amount together with the rate it was booked at.
 *
 * The rate belongs on the document, not in a lookup at read time: an invoice
 * issued in euros is worth what the euro was worth on its own date, and
 * re-deriving that later from today's rate silently rewrites history.
 */
export function ExchangeRateInput({
  value,
  onValueChange,
  baseCurrency = 'TRY',
  currencies,
  suggestedRates,
  disabled,
  className,
  ...aria
}: ExchangeRateInputProps) {
  const { t } = useTranslation('common')
  const isBase = value.currency === baseCurrency
  const suggested = suggestedRates?.[value.currency]
  const converted = convertedAmount(value, baseCurrency)

  const setCurrency = (currency: CurrencyCode) =>
    onValueChange({
      ...value,
      currency,
      // The old rate belongs to the old currency; carrying it over is worse
      // than an empty field, because it looks deliberate.
      rate: currency === baseCurrency ? 1 : (suggestedRates?.[currency] ?? null),
    })

  return (
    <div role="group" className={cn('flex flex-col gap-2', className)} {...aria}>
      <div className="grid gap-2 sm:grid-cols-[minmax(0,1.3fr)_minmax(0,1fr)]">
        <MoneyInput
          aria-label={t('exchangeRateInput.amount')}
          value={value.amount}
          onValueChange={(amount) => onValueChange({ ...value, amount })}
          currency={value.currency}
          onCurrencyChange={setCurrency}
          currencies={currencies}
          disabled={disabled}
        />
        <NumberInput
          aria-label={t('exchangeRateInput.rateAriaLabel', { from: value.currency, to: baseCurrency })}
          value={isBase ? 1 : value.rate}
          onValueChange={(rate) => onValueChange({ ...value, rate })}
          min={0}
          step="any"
          smallStep={0.0001}
          largeStep={1}
          format={{ minimumFractionDigits: 4, maximumFractionDigits: 4 }}
          prefix={t('exchangeRateInput.ratePrefix')}
          placeholder="0,0000"
          disabled={disabled || isBase}
          readOnly={isBase}
        />
      </div>

      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-muted-foreground tnum flex items-center gap-1.5 text-[11.5px]">
          <ArrowRightLeft aria-hidden className="size-3.5" strokeWidth={1.75} />
          {isBase ? (
            <>{t('exchangeRateInput.alreadyInBase', { currency: baseCurrency })}</>
          ) : (
            <>
              1 {CURRENCIES[value.currency].symbol} = {value.rate ?? 0} {baseCurrency} ·{' '}
              <span className="text-foreground font-[550]">
                {formatMoney(converted, baseCurrency)}
              </span>
            </>
          )}
        </p>
        {!isBase && suggested !== undefined && suggested !== value.rate && (
          <button
            type="button"
            disabled={disabled}
            onClick={() => onValueChange({ ...value, rate: suggested })}
            className="text-accent-foreground flex cursor-pointer items-center gap-1.5 border-0 bg-transparent p-0 text-[11.5px] font-[550] underline-offset-4 outline-none hover:underline"
          >
            <RefreshCw aria-hidden className="size-3.5" strokeWidth={1.75} />
            {t('exchangeRateInput.applyTodaysRate', { rate: suggested })}
          </button>
        )}
      </div>
    </div>
  )
}
