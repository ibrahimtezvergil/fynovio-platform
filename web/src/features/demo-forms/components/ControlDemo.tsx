import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

interface ControlDemoProps {
  /** The component as it is imported — this page doubles as the index. */
  name: string
  title: string
  description?: string
  /** What the control currently emits, so the value contract is visible. */
  value?: ReactNode
  /** Spans both columns — for controls that need the full width. */
  wide?: boolean
  children: ReactNode
}

/**
 * One control, its contract and its live value. The readout is the point of
 * the page: it shows what leaves the field, which is the thing a schema and a
 * request body have to agree on.
 */
export function ControlDemo({
  name,
  title,
  description,
  value,
  wide,
  children,
}: ControlDemoProps) {
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
      {children}
      {value !== undefined && (
        <p className="text-muted-foreground border-t border-[var(--nx-hairline-soft)] pt-2.5 font-mono text-[11px] break-all">
          <span className="text-[var(--nx-label-3)]">value → </span>
          <span className="text-foreground">{value}</span>
        </p>
      )}
    </div>
  )
}

/** How an emitted value is printed in the readout — `null` must look like null. */
export function show(value: unknown): string {
  if (value === null || value === undefined) return 'null'
  if (typeof value === 'string') return value.length > 0 ? `"${value}"` : '""'
  if (Array.isArray(value)) return value.length > 0 ? JSON.stringify(value) : '[]'
  if (value instanceof Date) return value.toISOString().slice(0, 10)
  if (typeof value === 'object') return JSON.stringify(value)
  return String(value)
}
