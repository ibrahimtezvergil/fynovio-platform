import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

/** What the emptiness means, which is all the artwork is allowed to colour. */
type Tone = 'neutral' | 'success' | 'warning' | 'danger'

const TONE_COLOR: Record<Tone, string> = {
  neutral: 'text-[var(--nx-tint)]',
  success: 'text-[var(--nx-st-green-fg)]',
  warning: 'text-[var(--nx-st-amber-fg)]',
  danger: 'text-[var(--nx-st-red-fg)]',
}

interface EmptyStateProps {
  /** The compact mark, for an empty state living inside a card or a table. */
  icon?: LucideIcon
  /**
   * Full artwork from `@/components/common/illustrations`, for an empty state
   * that owns the whole viewport. Wins over `icon` when both are given.
   */
  illustration?: ReactNode
  /** Colours the artwork only — never the copy, which stays on the label ramp. */
  tone?: Tone
  title: string
  description: string
  /** Primary action first — the one thing that ends the emptiness. */
  action?: ReactNode
  className?: string
}

/** Says all three things: what happened, why it's empty, what to do next. */
export function EmptyState({
  icon: Icon,
  illustration,
  tone = 'neutral',
  title,
  description,
  action,
  className,
}: EmptyStateProps) {
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center gap-2.5 px-6 py-16 text-center',
        className,
      )}
    >
      {illustration ? (
        <div className={cn('mb-1 w-full max-w-[190px]', TONE_COLOR[tone])}>{illustration}</div>
      ) : (
        Icon && (
          <span className="nx-icon-tile size-11 rounded-[var(--nx-r-ctl-lg)]">
            <Icon aria-hidden className="size-5.5" strokeWidth={1.7} />
          </span>
        )
      )}
      <p className="font-heading mt-1 text-[15px] font-[620] tracking-[-0.022em]">{title}</p>
      <p className="text-muted-foreground max-w-[280px] text-[12.5px] leading-[1.5]">
        {description}
      </p>
      {action && (
        <div className="mt-2 flex flex-wrap items-center justify-center gap-2">{action}</div>
      )}
    </div>
  )
}
