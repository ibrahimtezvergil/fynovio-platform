import type { SharedView } from '@/lib/shared-views/schema'

/** What the table calls its columns: a built-in one by its key, a tenant field as `cf:<key>`. */
export const columnIdOf = (column: SharedView['columns'][number]) => (column.kind === 'field' ? `cf:${column.key}` : column.key)

/**
 * A shared view decides which columns the table shows, and in what order. The selection column and the row actions are the
 * table's own and always stay, first and last. A column the view names that this table does not have — a field that has been
 * deprecated since the view was saved — is skipped: the view keeps working with the columns that remain.
 */
export function applySharedView<TColumn>(columns: readonly TColumn[], view: SharedView | null | undefined, idOf: (column: TColumn) => string | undefined): TColumn[] {
  if (!view) return [...columns]
  const byId = new Map(columns.flatMap((column) => { const id = idOf(column); return id === undefined ? [] : [[id, column] as const] }))
  const chosen = view.columns.flatMap((column) => { const found = byId.get(columnIdOf(column)); return found ? [found] : [] })
  const first = columns.filter((column) => idOf(column) === 'select')
  const last = columns.filter((column) => idOf(column) === 'rowActions')
  return [...first, ...chosen, ...last]
}
