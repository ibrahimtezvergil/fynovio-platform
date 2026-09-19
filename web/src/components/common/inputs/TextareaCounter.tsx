import { cn } from '@/lib/utils'
import { Textarea } from '@/components/ui/textarea'
import type { FieldControlProps } from './types'

export interface TextareaCounterProps extends FieldControlProps {
  value: string
  onValueChange: (value: string) => void
  maxLength: number
  placeholder?: string
  rows?: number
  disabled?: boolean
  className?: string
}

/**
 * Textarea with a live budget. The field does not hard-stop at `maxLength` —
 * it lets the overflow through and turns the counter negative, because
 * silently swallowing a pasted paragraph is worse than showing it is too long.
 */
export function TextareaCounter({
  value,
  onValueChange,
  maxLength,
  placeholder,
  rows = 4,
  disabled,
  className,
  ...rest
}: TextareaCounterProps) {
  const remaining = maxLength - value.length

  return (
    <div className={cn('flex flex-col gap-1.5', className)}>
      <Textarea
        value={value}
        rows={rows}
        placeholder={placeholder}
        disabled={disabled}
        onChange={(event) => onValueChange(event.target.value)}
        aria-invalid={remaining < 0 || undefined}
        {...rest}
      />
      <span
        aria-live="polite"
        className={cn(
          'tnum self-end text-[11.5px]',
          remaining < 0 ? 'text-[var(--nx-neg)] font-[550]' : 'text-muted-foreground',
        )}
      >
        {remaining} / {maxLength}
      </span>
    </div>
  )
}
