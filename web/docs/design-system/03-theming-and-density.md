# 03 · Theming & Density

Two independent axes. Theme decides *colour*; density decides *size*. They
never touch each other, and neither is ever a component prop.

---

## Theme

### The three states

The user's preference is `'light' | 'dark' | 'system'`, persisted in
`useAppStore`. What is actually on screen is `'light' | 'dark'` — the resolution
of that preference against the OS.

```
preference (store, persisted)  →  applyTheme()  →  .dark class on <html>
                               ↘  useResolvedTheme()  →  what is on screen
```

- **`applyTheme()`** in `src/store/useAppStore.ts` writes the `.dark` class.
- **`useResolvedTheme()`** folds `'system'` against the OS through
  `useSyncExternalStore`. **Any component that needs to know what is on screen
  reads this** — no component mirrors the media query itself.

```tsx
const isDark = useResolvedTheme() === 'dark'
```

### Writing theme-aware values

Define every colour in **both** `:root` and `.dark` in `tokens.css`, even when
the value is identical. Geometry, motion and metrics are theme-independent and
live in the shared `:root` block at the bottom of the file.

Never write a `.dark`-only override inside a component. If a value needs to
differ by theme, it is a token.

### What changes between themes — and what doesn't

| Changes | Stays |
| --- | --- |
| Ground, surfaces, glass alphas | Radii |
| Hairlines, specular | Blur amounts |
| Label ramp | Motion curve and durations |
| Accent (`#6355C7` ↔ `#A99EF0`) | Metrics (sidebar, topbar, row heights) |
| Status tone pairs | The gradient (`--nx-accent-grad` is identical in both) |
| Shadow colour and depth | Ambient wash stops |

Both themes cast real shadows. The dark theme is not "the light theme with
shadows removed".

### Checking a change

Any styling change is verified in **both** themes. See
[13 Definition of Done](./13-ux-definition-of-done.md).

---

## Density

### One boolean, expressed as custom properties

```
isCompact (store, persisted)  →  applyDensity()  →  <html data-density="compact">
                                                  ↓
                              tokens.css selects the --nx-d-* block
                                                  ↓
                    .nx-dense rebinds --nx-control-height / --nx-row-height
                                                  ↓
          h-control, .nx-row, .nx-grid, Input, Select, Button follow — no props
```

**`.nx-dense` is the opt-in half.** `data-density` on `<html>` only *selects*
the token block; nothing resizes until a region carries the class. That is what
lets the topbar, sidebar and KPI cards stay comfortable on a compact page.

### The two blocks

| Property | Comfortable | Compact |
| --- | --- | --- |
| Row | 52px | 32px |
| Relaxed row (avatar / pill) | 62px | 38px |
| Grid header | 42px | 30px |
| Cell padding Y / X | 8 / 16px | 3 / 11px |
| Edge gutter | 24px | 16px |
| Text | 13.5px | 12px |
| Control height | 38px | 32px |
| Small control | 30px | 28px |
| Gap / small gap | 12 / 9px | 7 / 5px |
| Zebra stripe | transparent | `--nx-fill` |
| Divider | `--nx-hairline-soft` | `--nx-hairline` |
| Cell wrapping | `normal` | `nowrap` |

Compact is **a working density, not a smaller copy**. Losing 20px of row height
costs scan-line legibility, so compact pays it back with a zebra stripe and a
full-strength divider. `38px` for a relaxed compact row rather than 32 is the
same reasoning: an avatar or a 26px pill needs 6px of air to stop reading as
clipped.

### Opting a region in

```tsx
<DensityScope>…</DensityScope>     // generic region
<Toolbar>…</Toolbar>               // already a density region
<DataTable … />                    // already a density region
```

`DensityScope` deliberately **does not read the store**. The preference is
already on `<html>` and custom properties inherit, so subscribing would only
re-render the whole region — table included — on every flip. `DensityToggle` is
the only component that needs the value.

Pin a region to one density with the `density` prop:

```tsx
<DensityScope density="comfortable">…</DensityScope>
```

### What must never go compact

- The topbar and the sidebar.
- Navigation of any kind.
- KPI and summary cards.
- Anything a first-time user meets before they have opted in.

These never carry `.nx-dense`. The density switch is a power-user affordance;
the frame around the work stays stable.

### The 44px trade

A 32px compact row is below the 44px pointer target, and `hit-min` **cannot**
rescue it — at that pitch the widened hit boxes of adjacent rows would overlap.

This is compact's explicit, documented trade. It is acceptable only because
compact is:

1. **opt-in** — never the default,
2. **persisted** — the user chose it deliberately, once,
3. **one click from reversible.**

Inside a *comfortable* row, anything clickable that renders smaller than 44px
carries the `hit-min` utility, which widens the hit box without changing the
row height.

### Adding a density-aware value

1. Add the pair to both `[data-density]` blocks in `tokens.css` as `--nx-d-*`.
2. If a component needs it structurally (not just a size), rebind it in
   `.nx-dense` in `index.css`.
3. Never branch on density in JavaScript. If you are writing
   `isCompact ? a : b` in a component, the value belongs in the token block.
