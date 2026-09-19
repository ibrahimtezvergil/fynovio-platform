# 02 · Foundations

The values everything else is assembled from. Canonical source:
`src/styles/tokens.css` (values) and `src/index.css` (mapping).

---

## The three-layer variable architecture

```
src/styles/tokens.css          --nx-*            raw values, per theme
        ↓ mapped by
src/index.css  @theme inline   --primary, --muted, --border, --color-tone-*, …
        ↓ consumed by
components                     Tailwind utilities + .nx-* classes
```

**Rule:** a component reads layer 2 or layer 3. Never layer 1.

The exception, and it is a gap rather than a licence: a handful of components
still reach for `var(--nx-hairline)` / `var(--nx-tint-fill)` inline because
`index.css` has no class for that exact surface yet. When you touch one, add
the mapping rather than copying the pattern.

---

## Colour

### Ground and surfaces

| Token | Light | Dark | Use |
| --- | --- | --- | --- |
| `--nx-canvas` | `#F4F5FA` | `#0E1120` | The page. Never a component background. |
| `--nx-canvas-2` | `#EAECF5` | `#161826` | Recessed areas, wells. |
| `--nx-surface` | `#FFFFFF` | `#161826` | The one *opaque* card/popover ground. |
| `--nx-glass` / `--nx-glass-2` | white @ .72 / .88 | white @ .045 / .07 | Translucent material — the layer everything floats on. |
| `--nx-glass-over` | white @ .96 | `#161A2A` @ .96 | Menus and sheets: opaque enough to read over arbitrary content. |

### Edges

`--nx-hairline` is the default separation. `--nx-hairline-strong` is a hover or
emphasis step. `--nx-hairline-soft` is for grid dividers only. `--nx-specular`
is the inset top highlight that keeps a material in the plane.

A hairline is **not** a general-purpose border colour. If you need a visible
box, you probably need a material class instead.

### Label ramp

| Token | Role |
| --- | --- |
| `--nx-label` | Primary text. Maps to `--foreground`. |
| `--nx-label-2` | Secondary text, descriptions, column headers. Maps to `--muted-foreground`. |
| `--nx-label-3` | Placeholders and disabled hints only. **Never body copy.** |

### Accent

One value, aliased twice:

```css
--nx-accent: #6355C7;              /* light */  #A99EF0 /* dark */
--nx-tint: var(--nx-accent);       /* text, icons, tints */
--nx-accent-grad: linear-gradient(180deg, #8A7BE8, #6E5DD6);
--nx-accent-glow: …                /* the one shadow a control may cast */
```

`--nx-accent-grad` fills **only**: the primary button, the brand mark, meters.
Everything else that wants to look brand-coloured uses `--nx-tint` or
`--nx-tint-fill`.

### Status tones

Seven tones, each a background/foreground pair, defined per theme:
`gray · blue · teal · green · amber · red · purple`.

Every pair clears **6:1** in both themes. They are exposed three ways:

- as the `.nx-pill` ladder via `data-tone`,
- as Tailwind colours `--color-tone-{gray|blue|teal|green|amber|red|purple}`
  (for third-party surfaces that need a plain colour),
- as `--color-stage-*` aliases for the pipeline vocabulary.

`--nx-pos` / `--nx-neg` are the delta colours (up/down), not status tones.

### Chart colours

`--color-chart-1…5` exist, but **only three of them separate cleanly for
colour-vision**: 1 and 3 are both violet, 4 and 5 are green/teal. Any chart
that encodes identity by colour uses `SERIES` from
`src/features/demo-charts/components/palette.ts` — chart-1 · chart-2 · chart-5,
in that fixed order, never more than three. See [10 Data Visualization](./10-data-visualization.md).

---

## Typography

Two families, both from the Nocturne port:

```css
--font-sans:    "SF Pro Text",    -apple-system, BlinkMacSystemFont, "Inter", system-ui, sans-serif;
--font-heading: "SF Pro Display", -apple-system, BlinkMacSystemFont, "Inter", system-ui, sans-serif;
```

`h1`–`h4` take `--font-heading` automatically, with `letter-spacing: -0.026em`
and `line-height: 1.15`. Body is `--font-sans` at `14px / 1.45`, tracking
`-0.01em`.

### The scale in practice

| Role | Size | Weight | Where |
| --- | --- | --- | --- |
| Page title | 26px | 620 | `PageHeader` |
| Section heading | 17–20px | 600 | Card headers |
| Body / control label | 13.5px | 400–590 | Buttons, inputs, most UI |
| Grid cell | 13.5px (12px compact) | 400 | `--nx-d-text` |
| Secondary / caption | 12.5px | 400 | Descriptions, helper text |
| Eyebrow | 11px, uppercase, tracked | 600 | `.nx-eyebrow` |
| Badge | 12px (11px `sm`) | 590 | `.nx-pill` |

Sizes are half-pixel-precise on purpose (13.5, 12.5). Round them and the
optical rhythm against SF Pro's metrics breaks.

### Numerals

Every table gets `font-variant-numeric: tabular-nums` from the base layer.
Anywhere outside a table — a KPI card, an inline total — use the `tnum`
utility. A number column that is not digit-aligned is a bug.

---

## Space and radii

### Radii ladder

Every step is a real value from the artboard. Use the alias, never the px.

| Token | Tailwind | Value | Use |
| --- | --- | --- | --- |
| `--nx-r-tile` | `rounded-tile` | 9px | Icon tiles |
| `--nx-r-ctl-sm` | `rounded-sm` | 11px | Brand mark, small controls |
| `--nx-r-ctl` | `rounded-md` | 14px | Buttons, inputs — the default control |
| `--nx-r-nav` | `rounded-nav` | 15px | Nav items |
| `--nx-r-ctl-lg` | `rounded-lg` | 18px | Tenant / user rows |
| `--nx-r-card` | `rounded-xl` | 26px | Cards |
| `--nx-r-panel` | `rounded-2xl` | 28px | Panels, sheets |
| `--nx-r-pill` | `rounded-full` | 9999px | Pills, avatars |

### Metrics

| Token | Value | Notes |
| --- | --- | --- |
| `--nx-sidebar-width` | 266px | Expanded |
| `--nx-sidebar-width-collapsed` | 72px | 14px gutter × 2 around a 44px square |
| `--nx-topbar-height` | 64px | |
| `--nx-control-height` | 38px | Rebound by `.nx-dense` |
| `--nx-row-height` | 52px | Rebound by `.nx-dense` |
| `--nx-row-height-relaxed` | 62px | Rows carrying an avatar or a pill |
| `--nx-hit-min` | 44px | The pointer-target floor |

Exposed as Tailwind spacing: `spacing-sidebar`, `spacing-topbar`,
`spacing-control`, `spacing-row`, `spacing-nav-item`.

### Gaps

Inside a density region, gaps come from `--nx-d-gap` (12px / 7px) and
`--nx-d-gap-sm` (9px / 5px). Outside one, use Tailwind's scale and stay on
multiples that read as 2.5 / 3.5 / 4 / 6 — the app's visual rhythm is not on a
strict 8px grid, and forcing one onto it fights the radii ladder.

---

## Elevation and material

Three material classes, and the difference between them is the whole system:

| Class | Blur | Hairline | Specular | Cast | Used by |
| --- | --- | --- | --- | --- | --- |
| `.nx-material` | ✅ | ✅ | ✅ | ❌ **none** | Sidebar, topbar, inline controls |
| `.nx-card` | ✅ | ✅ | ✅ | `--nx-elev` | Floating cards |
| `.nx-overlay` | deeper | brighter | ✅ | `--nx-elev-over` | Dialogs, sheets, popovers |

**Both themes cast real shadows.** This reverses an earlier "dark gets edge
light only" rule and the reversal is deliberate — see the note in `tokens.css`.

The primary button is the **one control** that casts, via `--nx-accent-glow`.
No other inline control may.

Elevation steps: `--nx-elev` (rest) → `--nx-elev-lift` (hover) →
`--nx-elev-over` (overlays). Mapped to `shadow-sm` / `shadow-md` / `shadow-lg`.

Blur: `--nx-blur` = `blur(24px) saturate(170%)`, `--nx-blur-deep` = `blur(40px)`.
The 170% saturation is not decoration — it is what keeps translucent surfaces
from going grey over a coloured ground.

---

## Motion

One spring for every state change:

```css
--nx-ease: cubic-bezier(0.16, 1, 0.3, 1);   /* ease-fluid */
--nx-dur: 0.25s;                            /* the default */
--nx-dur-fast: 0.12s;                       /* press feedback */
```

Rules:

- **One easing.** `ease-fluid` for everything. A second curve reads as a second
  product.
- **250ms is the default.** 120ms for a press (`active:scale-[0.97]`), 400ms
  only for the theme cross-fade on `body`.
- **Animate what moved.** The collapsed sidebar animates `width` only — labels
  are *dropped*, not faded, because fading them means animating layout.
- **Motion is never the message.** The skeleton sweep is decoration; the layout
  it holds open is the information. Under `prefers-reduced-motion: reduce` the
  sweep stops and nothing is lost.
- **Chart entry animation is off** (`STATIC_MARK`). recharts animates on rAF,
  which a background tab freezes at frame zero — the user returns to an empty
  chart.

### Reduced motion

Currently honoured by the skeleton only. **Gap:** any new non-essential
animation must ship with its `prefers-reduced-motion` branch in the same change.

---

## Iconography

`lucide-react`, stroke width **1.7** for UI chrome (1.5 in older code, 2 inside
badges where the mark is small). Sizes track the control:

| Context | Size |
| --- | --- |
| Inside `size-xs` controls | `size-3` (12px) |
| Inside `size-sm` controls | `size-3.5` (14px) |
| Default controls, nav, topbar | `size-4` (16px) / `size-[17px]` |
| Empty-state mark | `size-6`+ |

Every decorative icon carries `aria-hidden`. An icon that is the *only* content
of a control needs an `aria-label` on the control. Full artwork lives in
`src/components/common/illustrations.tsx` and is tone-coloured, never
multi-coloured.

---

## Z-index

The scale is short and should stay that way:

| Layer | Value | What |
| --- | --- | --- |
| Content | `z-0` / auto | Page content |
| Raised in-flow | `z-1`, `z-[2]`, `z-[4]` | Sticky cells, resize handles, specular overlays |
| App chrome | `z-[5]` | Topbar |
| Sticky in-page | `z-10` | Sticky grid headers, save bars |
| Portalled | `z-50` | Every overlay — dialog, sheet, drawer, popover, tooltip, toast |

**Rule:** portalled content is always `z-50` and never fights, because a portal
escapes the stacking context. If you find yourself wanting `z-60`, the element
should have been portalled.

---

## Breakpoints

Tailwind defaults, used with a strong bias toward `sm:` and `lg:`:

| Prefix | Min width | Used for |
| --- | --- | --- |
| `sm:` | 640px | The main content reflow point — by far the most used |
| `md:` | 768px | Rare |
| `lg:` | 1024px | Sidebar, multi-column dashboards |
| `xl:` | 1280px | Wide dashboard grids |

This is a desktop-first B2B product: the design target is 1280–1920px, and
below `sm:` the goal is *usable*, not equivalent. Grids scroll horizontally
inside their own container rather than reflowing into cards.
