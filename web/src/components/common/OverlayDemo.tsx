import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

interface OverlayDemoProps {
  /** The primitive as it is imported — this page doubles as the index. */
  name: string
  title: string
  description?: string
  /** What the trigger last did, so the open/close contract stays visible. */
  state?: ReactNode
  /** Spans both columns — for demos that need the full width. */
  wide?: boolean
  children: ReactNode
}

/**
 * One overlay, its contract and its last outcome. The readout matters more
 * here than on the form page: an overlay's whole API is *when* it opens and
 * *why* it closed, and neither is visible once it has gone away again.
 */
export function OverlayDemo({
  name,
  title,
  description,
  state,
  wide,
  children,
}: OverlayDemoProps) {
  return (
    <div
      className={cn(
        'flex flex-col gap-3 rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-4',
        wide && 'lg:col-span-2',
      )}
    >
      <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
        <p className="text-[13px] leading-5 font-[590]">{title}</p>
        <code className="text-brand-graphic bg-[var(--nx-tint-fill)] rounded-sm px-1.5 py-0.5 font-mono text-[10.5px]">
          {name}
        </code>
      </div>
      {description && (
        <p className="text-muted-foreground -mt-1.5 text-[11.5px] leading-4">{description}</p>
      )}
      <div className="flex flex-wrap items-center gap-2">{children}</div>
      {state !== undefined && (
        <p className="text-muted-foreground border-t border-[var(--nx-hairline-soft)] pt-2.5 font-mono text-[11px] break-all">
          <span className="text-[var(--nx-label-3)]">state → </span>
          <span className="text-foreground">{state}</span>
        </p>
      )}
    </div>
  )
}
