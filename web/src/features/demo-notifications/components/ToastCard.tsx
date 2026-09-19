import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

type Tone = 'success' | 'error' | 'warning' | 'info'

/** `.nx-icon-tile` speaks the pipeline's colour names, not the toast's. */
const ICON_TONE: Record<Tone, 'green' | 'red' | 'amber' | 'blue'> = {
  success: 'green',
  error: 'red',
  warning: 'amber',
  info: 'blue',
}

interface ToastCardProps {
  tone: Tone
  icon: ReactNode
  title: string
  description: string
  /** The call as written, shown verbatim so the demo doubles as a snippet. */
  code: string
  /** Spans both columns — for demos that need the full width. */
  wide?: boolean
  children: ReactNode
}

/**
 * One toast trigger, its call site and its icon tone. The toast itself is
 * the payoff — this card is only the switchboard, so it stays as plain as
 * `OverlayDemo` on the modal page: name, description, trigger, done.
 */
export function ToastCard({ tone, icon, title, description, code, wide, children }: ToastCardProps) {
  return (
    <div
      className={cn(
        'flex flex-col gap-3 rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-4',
        wide && 'lg:col-span-2',
      )}
    >
      <div className="flex items-start gap-3">
        <span aria-hidden data-tone={ICON_TONE[tone]} className="nx-icon-tile mt-0.5">
          {icon}
        </span>
        <div className="flex min-w-0 flex-col gap-0.5">
          <p className="text-[13px] leading-5 font-[590]">{title}</p>
          <p className="text-muted-foreground text-[11.5px] leading-4">{description}</p>
        </div>
      </div>
      <div className="flex flex-wrap items-center gap-2">{children}</div>
      <code className="text-muted-foreground border-t border-[var(--nx-hairline-soft)] pt-2.5 font-mono text-[11px] break-all">
        {code}
      </code>
    </div>
  )
}
