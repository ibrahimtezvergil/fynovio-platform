# 06 · Data Grids

The grid is the product's centre of gravity. These rules are not stylistic —
a grid that breaks them is measurably slower to read.

Implementation: `src/components/data-table/` (TanStack Table v9 wrapper),
`.nx-grid` / `.nx-datagrid` in `src/index.css`.

---

## Alignment — the one rule that is never bent

| Content | Alignment |
| --- | --- |
| Text, entity names, labels | **Left** |
| Every number, currency amount, percentage | **Right** |
| Status badges and pills | **Right** |
| Dates | Left (they read as labels, not magnitudes) |
| Actions / row menu | Right, last column |

Set it once, in the column definition:

```ts
meta: { align: 'right' }
```

That flag also switches the column to **tabular figures**, which is the half
that actually makes a number column scannable. Never right-align with a
utility class on the cell — the header would not follow.

Column definitions stay **data-only**: no JSX chrome, no Tailwind classes. The
renderer reads `meta` and decides. Register the slot once per table:

```ts
tableFeatures({ columnMeta: {} as DataTableColumnMeta })
```

---

## Row heights

| | Comfortable | Compact |
| --- | --- | --- |
| Standard row | 52px | 32px |
| Row with an avatar or a status pill | 62px | 38px |
| Header row | 42px | 30px |

Comfortable is the default and stays the default. See
[03 Theming & Density](./03-theming-and-density.md) for the 44px trade compact
makes, and why it is acceptable.

A `DataTable` is already a density region — do not wrap it in another one.

---

## Features are opt-in per table

TanStack v9 registers capabilities explicitly, and the wrapper renders only
what is registered (it asks `hasApi` before calling anything optional). Register
what the table actually does, and nothing more:

```ts
export const pipelineFeatures = tableFeatures({
  rowSelectionFeature,
  rowSortingFeature,
  rowPaginationFeature,
})
```

Omitting a feature is a design statement, not an oversight — with no
`rowSortingFeature`, `column.getCanSort` is absent and headers correctly render
as plain labels rather than dead buttons. Document the omission in the table
file, as `saasGridTable.tsx` and `pipelineTable.tsx` both do.

| Capability | Feature |
| --- | --- |
| Column visibility | `columnVisibilityFeature` |
| Row selection | `rowSelectionFeature` |
| Column resizing | `columnSizingFeature` **+** `columnResizingFeature` (the first is not optional; the second writes into its state) |
| Sorting | `rowSortingFeature` + the sorted row model |
| Pagination | `rowPaginationFeature` + the paginated row model |

---

## State ownership

Sorting, pagination and the global filter are owned by the **URL**, through
`useTableSearchParams` — external `state` plus matching `on<Slice>Change`
handlers. The table never holds those slices.

```tsx
const { pagination, sorting, globalFilter, onPaginationChange, … } =
  useTableSearchParams({ prefix: 'deals' })
```

- `prefix` namespaces the keys so two grids can share one URL.
- `replaceHistory` defaults to `true`: paging must not pollute the back button.
- `reset()` returns everything to defaults in **one** navigation.

Selection, column visibility and sizing stay in table state — they are session
preferences, not addressable views. Saved views (a named snapshot of all of it)
are a separate feature; see the UX backlog.

---

## Search

Grid search is debounced at 250ms through `ToolbarSearch`. Keystrokes stay
local; only the settled value reaches `onChange` — which is a filter pass today
and a query key once a backend exists.

`ToolbarSearch` adjusts its draft **during render** rather than in an effect, so
an external reset (a cleared filter, a shared link) lands in one pass instead of
two. Copy that pattern rather than an effect, if you build a similar control.

---

## Compact-mode legibility

Compact does not merely shrink. `.nx-dense .nx-grid` also gains:

- a **zebra stripe** (`--nx-d-zebra`; transparent at comfortable, so it is one
  rule rather than a branch),
- a **full-strength divider** instead of the soft one,
- `white-space: nowrap` with ellipsis — a compact row is one line by
  definition, and a wrapping cell would silently break the 32px pitch.

---

## Selection and bulk actions

- The header checkbox reflects `all` / `some` / `none` and must render the
  indeterminate state.
- A selected row is `--nx-tint-fill` via `tr[data-selected]`.
- Selection survives sorting and paging within a view; it is cleared by a
  filter change or `reset()`.
- The selection count is announced — the bulk bar's count is a `role="status"`
  region, not just a visual number.

---

## Empty, loading, error

| State | Treatment |
| --- | --- |
| First load | Skeleton rows at the real row height, holding the column layout. Never a spinner over an empty table. |
| Refetch with data on screen | Keep the data, dim nothing, show a subtle inline indicator. Never blank a populated table. |
| No rows, no filter | `EmptyState` with a primary action — the one that ends the emptiness. |
| No rows, filters applied | A *different* message: say the filter is why, and offer to clear it. |
| Error | Inline error with a retry. The toolbar stays usable. |

Conflating "no data yet" with "no results for this filter" is the single most
common failure here. They are different states with different actions.

---

## When not to use a grid

- **Fewer than ~5 rows with 2–3 fields** → a list (`.nx-row`) reads better.
- **The user is comparing two records** → compare view, not a filtered grid.
- **The work is stage-based** → Kanban.
- **The work is time-based** → calendar or timeline.

The multi-view switcher exists precisely so the same dataset can be any of
these. Choosing a grid by default is a decision, so make it deliberately.

---

## Checklist for a new grid

- [ ] Every numeric column carries `meta.align: 'right'`
- [ ] Column definitions are data-only (no JSX chrome, no classes)
- [ ] Only the features the table uses are registered
- [ ] Sorting/paging/search go through `useTableSearchParams` with a `prefix`
- [ ] A row carrying an avatar or a pill uses the relaxed height
- [ ] Empty ≠ no-results: two distinct states
- [ ] Skeleton matches the real column layout
- [ ] Verified in both densities and both themes
