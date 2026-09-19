import type { LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'

/**
 * The tone ladder `.nx-pill` paints in `src/index.css`. A tone is a *hue*, not
 * a meaning — "amber" is not "warning". What a tone means is decided once per
 * domain, in that domain's status registry, so the same colour never says
 * "waiting" on one screen and "shipped" on the next.
 */
export type StatusTone =
  | 'gray'
  | 'blue'
  | 'teal'
  | 'green'
  | 'amber'
  | 'red'
  | 'purple'
  | 'outline'

/** One entry of a domain vocabulary: what the status is called, and how it reads. */
export interface StatusMeta {
  label: string
  tone: StatusTone
  /** Replaces the dot. Only for vocabularies where the mark adds meaning. */
  icon?: LucideIcon
}

/** A whole vocabulary — every value of a status union, mapped to its badge. */
export type StatusRegistry<T extends string> = Record<T, StatusMeta>

interface StatusBadgeProps extends StatusMeta {
  /** `sm` for dense surfaces — kanban cards, nested lists. */
  size?: 'sm' | 'md'
  className?: string
}

/**
 * Colour-coded process state: pending, approved, cancelled, paid.
 *
 * The mark plus the label carry the status, never the fill alone — the badge
 * survives greyscale, colour blindness and a printed page (WCAG 1.4.1). Every
 * fill/text pair in the ladder clears 6:1 in both themes.
 */
export function StatusBadge({ label, tone, icon: Icon, size = 'md', className }: StatusBadgeProps) {
  return (
    <span
      data-tone={tone}
      className={cn('nx-pill', size === 'sm' && 'h-[22px] gap-1.5 px-2 text-[11px]', className)}
    >
      {Icon ? (
        <Icon
          aria-hidden
          strokeWidth={2}
          className={cn('-ml-0.5 shrink-0', size === 'sm' ? 'size-3' : 'size-3.5')}
        />
      ) : (
        <span aria-hidden className="nx-pill__dot" />
      )}
      {label}
    </span>
  )
}
