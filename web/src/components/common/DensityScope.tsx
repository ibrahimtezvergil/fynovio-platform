import type { ElementType, ReactNode } from 'react'
import { cn } from '@/lib/utils'
import type { Density } from '@/types'

interface DensityScopeProps {
  children: ReactNode
  /**
   * Pins the region to one density. Omit it — the normal case — and the region
   * inherits the app-wide preference from `<html data-density>`.
   */
  density?: Density
  /** Defaults to a plain `div`; pass `'section'`, `'main'`, `Card`… when the wrapper is semantic. */
  as?: ElementType
  className?: string
}

/**
 * Marks a subtree as density-aware.
 *
 * `.nx-dense` rebinds `--nx-control-height` and `--nx-row-height` from the
 * `--nx-d-*` block, so everything inside that already resolves its height
 * through those tokens — `Input`, `Select`, `Button`, `.nx-row`, `.nx-grid` —
 * follows the switch with no prop of its own.
 *
 * It deliberately does not read the store. The preference is already on
 * `<html>` and custom properties inherit, so subscribing here would buy
 * nothing but a re-render of the whole region — a table included — on every
 * flip. `DensityToggle` is the only component that needs to know the value.
 */
export function DensityScope({
  children,
  density,
  as: Tag = 'div',
  className,
}: DensityScopeProps) {
  return (
    <Tag data-density={density} className={cn('nx-dense', className)}>
      {children}
    </Tag>
  )
}
