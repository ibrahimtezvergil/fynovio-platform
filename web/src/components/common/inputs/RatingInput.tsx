import { Star } from 'lucide-react'
import { useState } from 'react'
import { cn } from '@/lib/utils'
import type { FieldControlProps } from './types'

export interface RatingInputProps extends FieldControlProps {
  value: number
  onValueChange: (value: number) => void
  max?: number
  /** Clicking the current rating clears it — the way a filter should behave. */
  clearable?: boolean
  disabled?: boolean
  className?: string
}

/**
 * Lead scoring, supplier ratings, ticket satisfaction. A real radio group
 * underneath, so arrow keys move between the stars and a screen reader reads
 * "3 / 5" instead of five unlabelled buttons.
 */
export function RatingInput({
  value,
  onValueChange,
  max = 5,
  clearable = true,
  disabled,
  className,
  ...aria
}: RatingInputProps) {
  const [hovered, setHovered] = useState<number | null>(null)
  const shown = hovered ?? value

  return (
    <div
      role="radiogroup"
      className={cn('flex items-center gap-1', className)}
      onMouseLeave={() => setHovered(null)}
      {...aria}
    >
      {Array.from({ length: max }, (_, index) => {
        const score = index + 1
        const filled = score <= shown
        return (
          <button
            key={score}
            type="button"
            role="radio"
            aria-checked={score === value}
            aria-label={`${score} / ${max}`}
            disabled={disabled}
            onMouseEnter={() => setHovered(score)}
            onFocus={() => setHovered(score)}
            onBlur={() => setHovered(null)}
            onClick={() => onValueChange(clearable && score === value ? 0 : score)}
            className={cn(
              'flex cursor-pointer items-center justify-center rounded-sm border-0 bg-transparent p-0.5',
              'outline-none transition-transform duration-[150ms] ease-fluid hover:scale-110',
              'focus-visible:ring-3 focus-visible:ring-ring/40',
              'disabled:pointer-events-none disabled:opacity-45',
            )}
          >
            <Star
              aria-hidden
              className={cn(
                'size-5 transition-colors duration-[150ms] ease-fluid',
                filled ? 'text-[var(--nx-st-amber-fg)]' : 'text-[var(--nx-label-3)]',
              )}
              strokeWidth={1.75}
              fill={filled ? 'currentColor' : 'none'}
            />
          </button>
        )
      })}
      <span className="text-muted-foreground tnum ml-1.5 text-[12.5px]">
        {value > 0 ? `${value} / ${max}` : '—'}
      </span>
    </div>
  )
}
