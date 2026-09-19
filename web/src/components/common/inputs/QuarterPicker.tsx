import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import type { FieldControlProps } from './types'

export type Quarter = 1 | 2 | 3 | 4

export interface QuarterValue {
  year: number
  quarter: Quarter
}

const QUARTER_VALUES: readonly Quarter[] = [1, 2, 3, 4]

export interface QuarterPickerProps extends FieldControlProps {
  value: QuarterValue
  onValueChange: (value: QuarterValue) => void
  minYear?: number
  maxYear?: number
  disabled?: boolean
  className?: string
}

/** First and last day of a quarter — what a report actually queries on. */
export function quarterRange({ year, quarter }: QuarterValue): { from: Date; to: Date } {
  const startMonth = (quarter - 1) * 3
  return {
    from: new Date(year, startMonth, 1),
    to: new Date(year, startMonth + 3, 0),
  }
}

/**
 * A fiscal quarter, picked as a quarter.
 *
 * Reporting periods are quarters and years far more often than they are date
 * ranges, and asking for "1 Ocak – 31 Mart" through a calendar is four clicks
 * and one off-by-one day away from a wrong report.
 */
export function QuarterPicker({
  value,
  onValueChange,
  minYear = 2000,
  maxYear = 2100,
  disabled,
  className,
  ...aria
}: QuarterPickerProps) {
  const { t } = useTranslation('common')
  const quarterMonths = t('quarterPicker.months', { returnObjects: true }) as Record<
    Quarter,
    string
  >
  const stepYear = (delta: number) => {
    const year = Math.min(Math.max(value.year + delta, minYear), maxYear)
    onValueChange({ ...value, year })
  }

  return (
    <div
      role="group"
      className={cn('flex flex-wrap items-center gap-2', className)}
      {...aria}
    >
      <div className="flex h-control shrink-0 items-center overflow-hidden rounded-md border border-[var(--nx-hairline)] bg-[var(--nx-fill)]">
        <button
          type="button"
          aria-label={t('quarterPicker.prevYear')}
          disabled={disabled || value.year <= minYear}
          onClick={() => stepYear(-1)}
          className="text-muted-foreground hover:text-foreground flex h-full w-8 cursor-pointer items-center justify-center border-0 bg-transparent outline-none hover:bg-[var(--nx-fill-hover)] disabled:pointer-events-none disabled:opacity-40"
        >
          <ChevronLeft aria-hidden className="size-4" strokeWidth={1.75} />
        </button>
        <span className="tnum w-12 text-center text-[13.5px] font-[590]">{value.year}</span>
        <button
          type="button"
          aria-label={t('quarterPicker.nextYear')}
          disabled={disabled || value.year >= maxYear}
          onClick={() => stepYear(1)}
          className="text-muted-foreground hover:text-foreground flex h-full w-8 cursor-pointer items-center justify-center border-0 bg-transparent outline-none hover:bg-[var(--nx-fill-hover)] disabled:pointer-events-none disabled:opacity-40"
        >
          <ChevronRight aria-hidden className="size-4" strokeWidth={1.75} />
        </button>
      </div>

      <div role="radiogroup" aria-label={t('quarterPicker.quarter')} className="nx-seg">
        {QUARTER_VALUES.map((quarter) => (
          <button
            key={quarter}
            type="button"
            role="radio"
            aria-checked={quarter === value.quarter}
            aria-label={t('quarterPicker.quarterAriaLabel', {
              year: value.year,
              quarter,
              months: quarterMonths[quarter],
            })}
            disabled={disabled}
            onClick={() => onValueChange({ ...value, quarter })}
            className="nx-seg__opt flex flex-col justify-center gap-0 px-3.5 leading-[1.15]"
          >
            <span className="text-[12.5px] font-[590]">
              {t('quarterPicker.shortLabel', { quarter })}
            </span>
            <span className="text-[10px] opacity-70">{quarterMonths[quarter]}</span>
          </button>
        ))}
      </div>
    </div>
  )
}
