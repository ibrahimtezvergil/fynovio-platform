import type { LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'

export interface Segment<T extends string> {
  value: T
  label: string
  /** Optional leading glyph. The label always stays — an icon never carries the meaning alone. */
  icon?: LucideIcon
}

interface SegmentedControlProps<T extends string> {
  segments: readonly Segment<T>[]
  value: T
  onChange: (value: T) => void
  fullWidth?: boolean
  /**
   * Show only the icons. The label stays in the accessibility tree — a
   * segment with no accessible name is unreachable by anything but a mouse —
   * so this is legal only for segments that actually have an `icon`.
   */
  iconOnly?: boolean
  'aria-label'?: string
}

/**
 * Track-and-pill control. The selection is a lifted surface inside the pill
 * track and moves on the same spring as every other state change.
 */
export function SegmentedControl<T extends string>({
  segments,
  value,
  onChange,
  fullWidth,
  iconOnly = false,
  ...rest
}: SegmentedControlProps<T>) {
  return (
    <div
      role="tablist"
      aria-label={rest['aria-label']}
      className={cn('nx-seg', fullWidth && 'flex w-full')}
    >
      {segments.map((segment) => {
        const selected = segment.value === value
        const Icon = segment.icon
        return (
          <button
            key={segment.value}
            type="button"
            role="tab"
            aria-selected={selected}
            onClick={() => onChange(segment.value)}
            className={cn(
              'nx-seg__opt',
              Icon && 'inline-flex items-center gap-1.5',
              iconOnly && Icon && 'px-2.5',
              fullWidth && 'flex-1',
            )}
          >
            {Icon && <Icon aria-hidden className="size-3.5 shrink-0" strokeWidth={1.9} />}
            {iconOnly && Icon ? <span className="sr-only">{segment.label}</span> : segment.label}
          </button>
        )
      })}
    </div>
  )
}
