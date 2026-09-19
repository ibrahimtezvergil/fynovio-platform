import type { RowData } from '@tanstack/react-table'
import type { CSSProperties } from 'react'
import { hasApi, type AnyTable } from '@/components/data-table/types'

/** CSS custom properties are restricted to `[A-Za-z0-9-_]`; column ids are not. */
function cssSafe(id: string): string {
  return id.replace(/[^a-zA-Z0-9_-]/g, '_')
}

export function columnSizeVar(columnId: string): string {
  return `--col-${cssSafe(columnId)}-size`
}

/** `width: calc(var(--col-x-size) * 1px)` — unitless numbers keep the calc cheap. */
export function columnWidthStyle(columnId: string): CSSProperties {
  return { width: `calc(var(${columnSizeVar(columnId)}) * 1px)` }
}

/**
 * Builds one flat map of column widths for the whole grid.
 *
 * `columnResizeMode: 'onChange'` commits `columnSizing` on every animation
 * frame of a drag. Reading `column.getSize()` inside every `<td>` would then
 * walk the sizing model once per cell per frame; writing the sizes once as CSS
 * variables on the `<table>` element lets the browser do the rest.
 *
 * Callers should memoize on the `columnSizing` / `columnResizing` snapshots —
 * see `DataTable`.
 */
export function buildColumnSizeVars<TData extends RowData>(
  table: AnyTable<TData>,
): CSSProperties {
  const vars: Record<string, string> = {}

  for (const header of table.getFlatHeaders()) {
    // `getSize` belongs to columnSizingFeature — absent on grids that skip it.
    if (!hasApi(header, 'getSize')) continue
    vars[columnSizeVar(header.column.id)] = String(header.getSize())
  }

  if (hasApi(table, 'getTotalSize')) {
    vars['--table-total-size'] = String(table.getTotalSize())
  }

  return vars as CSSProperties
}
