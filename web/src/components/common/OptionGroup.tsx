import type { LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'

export interface Option<T extends string> {
  value: T
  label: string
  icon?: LucideIcon
}

interface OptionGroupProps<T extends string> {
  label: string
  options: readonly Option<T>[]
  value: T
  onChange: (value: T) => void
}

/**
 * Flat radio row used inside the profile popover: no track, the selected item
 * is a tinted rounded rect. Distinct from SegmentedControl, which is the
 * pill-on-grey-track filter used in page headers.
 */
export function OptionGroup<T extends string>({
  label,
  options,
  value,
  onChange,
}: OptionGroupProps<T>) {
  return (
    <div role="radiogroup" aria-label={label} className="flex items-center gap-1 px-1.5 py-1.5">
      {options.map((option) => {
        const selected = option.value === value
        return (
          <button
            key={option.value}
            type="button"
            role="radio"
            aria-checked={selected}
            onClick={() => onChange(option.value)}
            className={cn(
              'flex flex-1 cursor-pointer items-center justify-center gap-1.5 rounded-sm px-2.5 py-1.5',
              'text-[12.5px] font-[550] whitespace-nowrap',
              'transition-colors duration-[250ms] ease-fluid',
              selected
                ? 'bg-accent text-accent-foreground font-[590]'
                : 'text-muted-foreground hover:text-foreground hover:bg-[var(--nx-fill-hover)]',
            )}
          >
            {option.icon && <option.icon aria-hidden className="size-4" strokeWidth={1.5} />}
            {option.label}
          </button>
        )
      })}
    </div>
  )
}
