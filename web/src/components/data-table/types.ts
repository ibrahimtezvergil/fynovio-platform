import type {
  Cell,
  Header,
  ReactTable,
  Row,
  RowData,
  Table,
  TableFeatures,
} from '@tanstack/react-table'
import type { Density } from '@/types'

/**
 * Per-column presentation metadata.
 *
 * Column definitions stay data-only — no JSX chrome, no Tailwind classes — and
 * the renderer reads this slot to decide alignment and labelling. Register it
 * once per table with the `columnMeta` slot:
 *
 * ```ts
 * tableFeatures({ columnMeta: {} as DataTableColumnMeta })
 * ```
 */
export interface DataTableColumnMeta {
  /** Cell + header text alignment. Numeric columns should be `'right'`. */
  align?: 'left' | 'center' | 'right'
  /** Plain-text name for menus (column visibility) where a JSX header can't be used. */
  label?: string
  /** Extra classes for the `<th>`. */
  headerClassName?: string
  /** Extra classes for every `<td>` in the column. */
  cellClassName?: string
}

/**
 * `TFeatures = any` selects table-core's permissive branch: its feature maps are
 * written so an `any` feature set exposes *every* feature API
 * (`IsAny<TFeatures> extends true ? …` in `types/TableFeatures.d.ts`). That is
 * the surface a renderer shared by grids with different feature sets is written
 * against — `getVisibleCells` for one grid, `getResizeHandler` for another.
 */
export type AnyTableFeatures = any

export type AnyTable<TData extends RowData> = ReactTable<AnyTableFeatures, TData, any>
export type AnyCoreTable<TData extends RowData> = Table<AnyTableFeatures, TData>
export type AnyRow<TData extends RowData> = Row<AnyTableFeatures, TData>
export type AnyHeader<TData extends RowData> = Header<AnyTableFeatures, TData, unknown>
export type AnyCell<TData extends RowData> = Cell<AnyTableFeatures, TData, unknown>

/**
 * Row density. Aliases the app-wide `Density` so a grid can be handed
 * `useDensity().density` straight from the store with no mapping.
 */
export type DataTableDensity = Density

/**
 * Widening, in exactly one place.
 *
 * A concrete instance registers a handful of features, so it is structurally
 * *narrower* than the all-features surface above and TypeScript rejects the
 * assignment — correctly, in general: an unregistered API really is missing at
 * runtime.
 *
 * The renderer components below are the exception that earns the cast. They
 * accept a properly typed `ReactTable<TFeatures, TData>` at their prop
 * boundary, widen it here, and then call nothing optional without asking
 * `hasApi` first. The alternative — a renderer generic over every feature
 * combination — buys no safety the guards don't already provide.
 */
export function widenTable<TData extends RowData>(table: object): AnyTable<TData> {
  return table as unknown as AnyTable<TData>
}

/** Same widening for the core instance handed to header and cell renderers. */
export function widenCoreTable<TData extends RowData>(table: object): AnyCoreTable<TData> {
  return table as unknown as AnyCoreTable<TData>
}

export function widenRow<TData extends RowData>(row: object): AnyRow<TData> {
  return row as unknown as AnyRow<TData>
}

/**
 * Runtime guard for feature APIs that only exist when their plugin is
 * registered. `table.getRowModel` is core and always present; `table.setSorting`
 * only appears once `rowSortingFeature` is in `tableFeatures({ ... })`.
 *
 * This is what keeps the widening above honest.
 */
export function hasApi(target: object | undefined | null, key: string): boolean {
  if (!target) return false
  return typeof (target as Record<string, unknown>)[key] === 'function'
}

/** Prop shape shared by every component that takes a whole table. */
export interface TableProp<TFeatures extends TableFeatures, TData extends RowData> {
  table: ReactTable<TFeatures, TData, any>
}
