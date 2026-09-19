# 04 · Component Standards

How a component is placed, shaped, named and documented.

---

## The three layers

| Layer | Path | Rule |
| --- | --- | --- |
| **Primitives** | `src/components/ui/` | shadcn-generated. Hand-edit only when *intentionally* customizing a primitive — not as a first resort. |
| **Shared** | `src/components/common/` (+ `common/inputs/`, `components/data-table/`) | Cross-feature building blocks. Anything two features both need. |
| **Feature** | `src/features/<name>/components/` | Owned by one feature. Never imported by another feature. |

**Code only moves up.** A feature component needed by a second feature is
promoted into `common/` in its own change — it is not imported across the
feature boundary. This is enforced by `no-restricted-imports` overrides in
`.oxlintrc.json`.

Layouts and routes may import from features. Features may never import layouts,
route guards, or the router. `@/routes/paths` is the one allowed exception.

### Where does it go?

```
Is it a shadcn primitive?            → components/ui/
Will a second feature need it?       → components/common/
Is it a form control?                → components/common/inputs/ (+ barrel export)
Is it grid machinery?                → components/data-table/
Otherwise                            → features/<name>/components/
```

Do not pre-promote. A component with one consumer belongs to that feature until
a second consumer actually exists.

---

## The controlled-component contract

Every shared input in `common/inputs/` obeys one contract:

```tsx
value: T
onValueChange: (value: T) => void
```

Not `onChange`. Not uncontrolled. The rename is load-bearing: it signals that
the callback receives a *value*, not a DOM event, and it keeps the control
compatible with the props `Field`'s render prop hands down:

```tsx
<Field label="Tutar">{(props) => <MoneyInput {...props} currency="TRY" />}</Field>
```

Any new control that does not satisfy `<Field>{(p) => <X {...p} />}</Field>`
is not finished.

The barrel (`common/inputs/index.ts`) re-exports everything and tree-shakes
cleanly — importing `MoneyInput` must not pull in the rich-text editor or the
phone-number metadata. Adding a control means adding it to the barrel.

---

## Prop conventions

| Convention | Example |
| --- | --- |
| Variants through `cva`, never boolean soup | `variant="secondary" size="sm"` |
| `className` last, merged with `cn()` | `cn('base', className)` |
| Composition over slots | `Toolbar` takes `children`, not a `filters` prop |
| Booleans read as state, not instructions | `disabled`, `openOnHover` — not `shouldOpen` |
| Optional semantic element via `as` | `<DensityScope as="section">` |
| Registries as data, JSX at the edge | `STAGE_META` is data; `StageBadge` renders it |

**Composition rule of thumb:** when a prop's value would be JSX that differs on
every page, it wants to be `children`. `Toolbar` documents this explicitly — a
`filters` prop "would only be a `children` with extra steps."

---

## Interaction states

Every interactive component implements the full set. A component missing one of
these is incomplete, not minimal.

| State | Standard |
| --- | --- |
| **Rest** | Material class or semantic background |
| **Hover** | `--nx-fill-hover`, or `shadow-md` for a card that lifts |
| **Focus-visible** | `ring-3 ring-ring/40` + `border-ring`. Never removed, never replaced with a colour change alone |
| **Active / press** | `active:scale-[0.97] active:duration-[120ms]` |
| **Selected** | `--nx-tint-fill` background, or `aria-expanded` for triggers |
| **Disabled** | `pointer-events-none opacity-40`. Never a grey that could read as a tone |
| **Invalid** | `aria-invalid:border-destructive` + destructive ring |
| **Loading** | Skeleton or inline spinner — see [09 States & Feedback](./09-states-and-feedback.md) |

The base layer also gives every element a global `:focus-visible` outline
(2px `--nx-tint`, 2px offset). Component rings refine it; nothing suppresses it.

---

## Documentation is part of the component

This codebase documents *reasoning*, not mechanics. The bar is set by files
like `DensityScope.tsx`, `Toolbar.tsx` and `tokens.css`: a doc comment explains
**why the alternative was rejected**, not what the props do.

Required on any shared component:

- A one-line summary of what it is for.
- The non-obvious decision, if there is one, and what it rules out.
- A usage snippet when the API is compositional.

Not required: prop-by-prop prose. Types carry that.

```tsx
/**
 * The strip above a grid: search and filters left, view controls right.
 *
 * It is a density region, so the controls it holds shrink with the switch
 * while the page header above it — which is not one — does not.
 */
```

---

## When to build vs. reuse

Before adding a component, in order:

1. **Does a shadcn primitive cover it?** Use it. Do not re-implement a dialog.
2. **Does `common/` already have it?** The input kit is large — check the
   barrel before writing a control.
3. **Can it be composed from existing parts?** Most "new components" are a
   `Card` + a `Toolbar` + a `DataTable`.
4. **Only then**: build it, and decide its layer with the table above.

A new dependency needs a stronger case than a new component. Everything in the
current roadmap is buildable with what is already in `package.json`.

---

## Anti-patterns

| Don't | Do |
| --- | --- |
| `rounded-[14px]` | `rounded-md` |
| `h-[38px]` | `h-control` |
| `bg-[#6355C7]` | `bg-primary` / `text-accent-foreground` |
| `style={{ ... }}` for anything themeable | A token + a utility |
| A `density` prop threaded through a tree | `DensityScope` |
| A `isDark` prop threaded through a tree | `useResolvedTheme()` |
| Reading `useStore()` with no selector | `useStore(s => s.slice)` / `useShallow` |
| A feature importing another feature | Promote to `common/` |
| Colour-only status | `StatusBadge` |
| Editing `components/ui/*` to change a colour | Change the mapping in `index.css` |

---

## Zustand access

v5 dropped the implicit shallow compare. `useStore()` with no selector
re-renders on **every** unrelated write.

```tsx
const collapsed = useAppStore((s) => s.sidebarCollapsed)              // ✅
const { theme, density } = useAppStore(useShallow((s) => ({ … })))    // ✅ several slices
const state = useAppStore()                                          // ❌
```

Stores hold what outlives the interaction. Form fields never go in a store —
that is react-hook-form's job. See [07 Forms](./07-forms.md).

---

## Deprecation

Mark with `@deprecated` naming the replacement, update the doc that describes
it, and remove once nothing imports it:

```tsx
/** @deprecated Use `StatusBadge` with a domain registry instead. */
```

Never delete a shared component in the same change that introduces its
replacement.
