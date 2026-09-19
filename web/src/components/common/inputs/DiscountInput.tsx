import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { NumberInput } from './NumberInput'
import {
  CURRENCIES,
  formatDecimal,
  formatMoney,
  roundMoney,
  type CurrencyCode,
} from './currencies'
import type { FieldControlProps } from './types'

export type DiscountMode = 'percent' | 'amount'

export interface Discount {
  mode: DiscountMode
  /** Percentage points when `mode` is `'percent'`, money when it is `'amount'`. */
  value: number | null
}

export interface DiscountInputProps extends FieldControlProps {
  discount: Discount
  onDiscountChange: (discount: Discount) => void
  currency?: CurrencyCode
  /** The line or quote total the discount applies to, for the resolved figure. */
  base?: number
  /** The "worth this much" line under the field. Off inside a dense grid. */
  showResolved?: boolean
  disabled?: boolean
  className?: string
}

/**
 * What the discount is actually worth against `base`, clamped so a careless
 * fixed discount can never turn a line negative.
 */
export function resolveDiscount(base: number, discount: Discount): number {
  const value = discount.value ?? 0
  const raw = discount.mode === 'percent' ? (base * value) / 100 : value
  return roundMoney(Math.min(Math.max(raw, 0), base))
}

const MODE_BUTTON = [
  'flex h-[26px] w-8 cursor-pointer items-center justify-center rounded-sm border-0',
  'text-[12.5px] font-[550] outline-none transition-colors duration-[150ms] ease-fluid',
].join(' ')

/**
 * One amount, two meanings. ERP discounts arrive both ways — "10%" off a price
 * list, "500 ₺" out of a negotiation — and making the user convert between
 * them by hand is how a quote ends up wrong, so the mode travels with the
 * value instead of being inferred later.
 */
export function DiscountInput({
  discount,
  onDiscountChange,
  currency = 'TRY',
  base,
  showResolved = true,
  disabled,
  className,
  ...rest
}: DiscountInputProps) {
  const { t } = useTranslation('common')
  const isPercent = discount.mode === 'percent'

  const setMode = (mode: DiscountMode) => {
    if (mode === discount.mode) return
    // The number means something different in each mode, so it never carries over.
    onDiscountChange({ mode, value: null })
  }

  return (
    <div className={cn('flex flex-col gap-1.5', className)}>
      <NumberInput
        value={discount.value}
        onValueChange={(value) => onDiscountChange({ ...discount, value })}
        min={0}
        max={isPercent ? 100 : base}
        step="any"
        smallStep={isPercent ? 0.5 : 1}
        largeStep={isPercent ? 5 : 100}
        format={{ maximumFractionDigits: 2 }}
        placeholder="0"
        disabled={disabled}
        trailing={
          <span
            role="radiogroup"
            aria-label={t('discountInput.type')}
            className="nx-seg my-auto mr-[5px] shrink-0 gap-0.5 p-0.5"
          >
            {(['percent', 'amount'] as const).map((mode) => (
              <button
                key={mode}
                type="button"
                role="radio"
                aria-checked={mode === discount.mode}
                disabled={disabled}
                onClick={() => setMode(mode)}
                className={cn(
                  MODE_BUTTON,
                  mode === discount.mode
                    ? 'text-foreground bg-[var(--nx-glass-2)] shadow-[inset_0_0_0_1px_var(--nx-hairline-strong),inset_0_1px_0_var(--nx-specular)]'
                    : 'text-muted-foreground bg-transparent hover:text-foreground',
                )}
              >
                {mode === 'percent' ? '%' : CURRENCIES[currency].symbol}
              </button>
            ))}
          </span>
        }
        {...rest}
      />
      {base !== undefined && showResolved && (
        <p className="text-muted-foreground tnum text-[11.5px]">
          {isPercent ? t('discountInput.amountEquivalent') : t('discountInput.rateEquivalent')}:{' '}
          <span className="text-foreground font-[550]">
            {isPercent
              ? formatMoney(resolveDiscount(base, discount), currency)
              : `%${formatDecimal(base > 0 ? (resolveDiscount(base, discount) / base) * 100 : 0)}`}
          </span>
        </p>
      )}
    </div>
  )
}
