import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

interface StateCardProps {
  title: string
  /** When this state is the right one — the reason it exists, not what it looks like. */
  description: string
  /** Spans both columns, for a demo that needs the full width. */
  wide?: boolean
  className?: string
  children: ReactNode
}

/**
 * One state on display, with the rule that governs it printed above. Same
 * frame as `ToastCard` on the notification page so the two galleries read as
 * one set — a label, a sentence, and then the thing itself.
 */
export function StateCard({ title, description, wide, className, children }: StateCardProps) {
  return (
    <div
      className={cn(
        'flex flex-col gap-3 rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-4',
        wide && 'lg:col-span-2',
        className,
      )}
    >
      <div className="flex min-w-0 flex-col gap-0.5">
        <p className="text-[13px] leading-5 font-[590]">{title}</p>
        <p className="text-muted-foreground text-[11.5px] leading-4">{description}</p>
      </div>
      <div className="min-w-0 overflow-hidden rounded-md border border-[var(--nx-hairline-soft)] bg-[var(--nx-canvas)]">
        {children}
      </div>
    </div>
  )
}
