import { Equal, Scale } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { NumberInput } from './NumberInput'
import { formatMoney, roundMoney, type CurrencyCode } from './currencies'
import type { FieldControlProps } from './types'

export interface AllocationTarget {
  id: string
  label: string
  description?: string
}

export interface AllocationInputProps extends FieldControlProps {
  targets: readonly AllocationTarget[]
  /** Target id → share. Percentage points, or money when `mode` is `'amount'`. */
  value: Record<string, number | null>
  onValueChange: (value: Record<string, number | null>) => void
  /** What the shares must add up to: `100` for percent, the total for amounts. */
  total: number
  mode?: 'percent' | 'amount'
  currency?: CurrencyCode
  disabled?: boolean
  className?: string
}

const sum = (value: Record<string, number | null>) =>
  roundMoney(Object.values(value).reduce<number>((total, share) => total + (share ?? 0), 0))

/**
 * Splitting one figure across several targets — cost centres, projects,
 * branches — where the shares have to close exactly.
 *
 * The remainder is shown at all times rather than validated at submit, because
 * a distribution that is 0,01 short is not a mistake the user can find by
 * re-reading their own numbers.
 */
export function AllocationInput({
  targets,
  value,
  onValueChange,
  total,
  mode = 'percent',
  currency = 'TRY',
  disabled,
  className,
  ...aria
}: AllocationInputProps) {
  const { t } = useTranslation('common')
  const allocated = sum(value)
  const remainder = roundMoney(total - allocated)
  const isPercent = mode === 'percent'
  const format = (amount: number) =>
    isPercent ? `%${amount.toLocaleString('tr-TR')}` : formatMoney(amount, currency)

  const splitEvenly = () => {
    const share = roundMoney(total / targets.length)
    const shares = Object.fromEntries(targets.map((target) => [target.id, share]))
    // Rounding leaves a few kuruş over; the first row absorbs it so the sum closes.
    const drift = roundMoney(total - share * targets.length)
    shares[targets[0].id] = roundMoney(share + drift)
    onValueChange(shares)
  }

  const giveRemainderTo = (id: string) =>
    onValueChange({ ...value, [id]: roundMoney((value[id] ?? 0) + remainder) })

  return (
    <div role="group" className={cn('flex flex-col gap-2.5', className)} {...aria}>
      {targets.map((target) => (
        <div key={target.id} className="grid grid-cols-[minmax(0,1fr)_150px] items-center gap-3">
          <span className="flex min-w-0 flex-col">
            <span className="truncate text-[13px]">{target.label}</span>
            {target.description && (
              <span className="text-muted-foreground truncate text-[11px]">
                {target.description}
              </span>
            )}
          </span>
          <NumberInput
            aria-label={t('allocationInput.shareOf', { target: target.label })}
            value={value[target.id] ?? null}
            onValueChange={(next) => onValueChange({ ...value, [target.id]: next })}
            min={0}
            max={total}
            step="any"
            format={{ maximumFractionDigits: 2 }}
            prefix={isPercent ? undefined : currency}
            suffix={isPercent ? '%' : undefined}
            placeholder="0"
            disabled={disabled}
          />
        </div>
      ))}

      <div className="flex flex-wrap items-center justify-between gap-2 border-t border-[var(--nx-hairline)] pt-2.5">
        <div className="flex items-center gap-2">
          <button
            type="button"
            disabled={disabled}
            onClick={splitEvenly}
            className="text-accent-foreground flex cursor-pointer items-center gap-1.5 border-0 bg-transparent p-0 text-[11.5px] font-[550] underline-offset-4 outline-none hover:underline disabled:opacity-45"
          >
            <Equal aria-hidden className="size-3.5" strokeWidth={2} />
            {t('allocationInput.splitEvenly')}
          </button>
          {remainder !== 0 && (
            <button
              type="button"
              disabled={disabled}
              onClick={() => giveRemainderTo(targets[targets.length - 1].id)}
              className="text-accent-foreground flex cursor-pointer items-center gap-1.5 border-0 bg-transparent p-0 text-[11.5px] font-[550] underline-offset-4 outline-none hover:underline disabled:opacity-45"
            >
              <Scale aria-hidden className="size-3.5" strokeWidth={1.75} />
              {t('allocationInput.giveRemainderToLast')}
            </button>
          )}
        </div>
        <p aria-live="polite" className="tnum text-[12px]">
          <span className="text-muted-foreground">
            {t('allocationInput.allocatedSummary', { amount: format(allocated) })}{' '}
          </span>
          <span
            className={cn(
              'font-[590]',
              remainder === 0 ? 'text-[var(--nx-pos)]' : 'text-[var(--nx-neg)]',
            )}
          >
            {format(remainder)}
          </span>
        </p>
      </div>
    </div>
  )
}
