import { ChevronDown } from 'lucide-react'
import { cn } from '@/lib/utils'
import { CONTROL_DIVIDER } from './styles'

export interface InlineSelectOption {
  value: string
  label: string
}

interface InlineSelectProps {
  /** Never rendered — this control's label lives on the field it is welded to. */
  'aria-label': string
  value: string
  onChange: (value: string) => void
  options: readonly InlineSelectOption[]
  disabled?: boolean
  className?: string
}

/**
 * The tail of a composite control: a native `<select>` with the chrome stripped
 * off, so a currency or unit picker reads as part of the field it sits in
 * rather than as a second control parked beside it.
 */
export function InlineSelect({
  value,
  onChange,
  options,
  disabled,
  className,
  ...aria
}: InlineSelectProps) {
  return (
    <span className={cn('relative flex shrink-0 items-center', CONTROL_DIVIDER, className)}>
      <select
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
        className="text-muted-foreground h-full cursor-pointer appearance-none bg-transparent pr-7 pl-3 text-[12.5px] font-[550] outline-none disabled:cursor-not-allowed"
        {...aria}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      <ChevronDown
        aria-hidden
        strokeWidth={1.7}
        className="text-muted-foreground pointer-events-none absolute right-2.5 size-3.5"
      />
    </span>
  )
}
