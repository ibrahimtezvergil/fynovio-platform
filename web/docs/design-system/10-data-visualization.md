# 10 · Data Visualization

Charts are **reports, not motion pieces**. Every rule here follows from that.

Implementation: `src/components/ui/chart.tsx` (shadcn wrapping recharts),
palette and motion in `src/features/demo-charts/components/`.
Gallery at `/demo/charts`.

---

## Colour

### Never more than three categorical series

`--color-chart-1…5` exist, but they are **not five categorical hues**: 1 and 3
are both violet, 4 and 5 are green/teal. Run through a colour-vision check the
closest pairs land around **ΔE 5–9** — well under the ~15 a full-colour reader
needs to tell two fills apart.

So any chart that encodes identity by colour uses `SERIES`:

```ts
export const SERIES = [
  'var(--color-chart-1)', // violet — the app accent
  'var(--color-chart-2)', // amber
  'var(--color-chart-5)', // teal
] as const
```

In that fixed order. Never more than three.

**More than three categories is not a colour problem — it is the wrong form.**
Rank them along one hue (`RAMP`), or split the chart.

### The other palettes

| Export | For |
| --- | --- |
| `RAMP` (`[1, .76, .54, .36, .22]`) | Ranked magnitudes: one hue stepped by opacity. Stays monotonic on both the white card and the dark one, which a second colour would not. |
| `STAGE_COLOR` | Pipeline stages — the same tokens the board and the badges use, so a legend and a badge cannot disagree. |
| `STATUS_COLOR` | Positive / negative outcomes. **Reserved** — never reused as a series hue. |
| `SURFACE_GAP` | The 2px `var(--card)` stroke that punches a gap between stacked segments and neighbouring bars. |

These live in `demo-charts/components/palette.ts`. A second feature that needs
them **moves the file up into `common/` first** — it does not import across the
feature boundary.

---

## Form

| Question | Chart |
| --- | --- |
| How did this change over time? | Line / area |
| How do these compare right now? | Bar (horizontal when labels are long) |
| What is the composition? | Stacked bar — **not** a pie beyond ~3 slices |
| Where does the flow leak? | Funnel |
| Do two measures relate? | Scatter |
| How far to target? | Meter / progress, not a gauge |

Rules:

- **Never two y-scales on one chart.** Two units on one plot manufactures
  correlations that are not there. Split it, or index both to a baseline.
- **Bar charts start at zero.** Always. Line charts may crop, and should say so.
- **Sort bars by value**, not alphabetically, unless the category order carries
  meaning (stages, months).
- Pie/donut only for a true part-to-whole with ≤3 slices, and always with values
  printed.

---

## Motion

**Entry animation is off.** Every mark spreads `STATIC_MARK`:

```ts
export const STATIC_MARK = { isAnimationActive: false } as const
```

recharts animates on `requestAnimationFrame`, which a background tab throttles
to a stop — a chart that mounts while the tab is hidden freezes at frame zero
and never finishes drawing. The reader switches to the tab and finds an empty
card.

Hover and tooltip transitions stay on. Those are interaction, not entry.

---

## Labels, axes, tooltips

- **Format numbers through the shared formatters** (`data/format.ts`,
  `Intl.NumberFormat('tr-TR')`). An axis showing `1200000` instead of `1,2 Mn ₺`
  is a defect.
- **Axis labels are units, not restatements.** "₺" or "adet", not "Tutar (₺)"
  when the title already says Tutar.
- **Tooltips give the exact value** the chart approximates, with its unit and
  its category. `tooltipRow.tsx` is the shared row renderer — use it.
- **Legends only when there is more than one series.** A one-series chart's
  legend is a second title.
- Grid lines are `--nx-hairline-soft`, never a full-strength divider. The data
  is the ink.

---

## Accessibility

Charts are the weakest surface for accessibility in any dashboard. Minimum bar:

- The chart has an accessible name (the card's heading, wired with
  `aria-labelledby`).
- **Colour is never the only encoder**: series are also distinguished by
  position, direct labels, or a legend the reader can map by order.
- A **data table alternative** is available for any chart that carries a number
  the user must act on. Drill-through (backlog item 22) satisfies this — the
  chart becomes a route into the real rows.
- Tooltips are reachable by keyboard where the chart is interactive; if they
  are not, the underlying numbers must be reachable some other way.
- Do not rely on hover for anything essential — hover does not exist on touch.

**Current gap:** the gallery does not yet ship table alternatives. Any chart
promoted from `/demo/charts` into a real feature must add one.

---

## Composition

Charts live in a `ChartCard`: heading, optional subtitle, the plot, and — when
relevant — one summary figure. The figure is `tnum`.

Do not stack more than two charts in a viewport without a heading between them.
A wall of plots is a screenshot, not a dashboard.

---

## Checklist

- [ ] ≤3 categorical series, from `SERIES`, in order
- [ ] More than three categories → ranked with `RAMP` or split
- [ ] `STATIC_MARK` spread on every mark
- [ ] One y-scale
- [ ] Bars start at zero
- [ ] Numbers formatted through the shared `Intl` formatters
- [ ] Accessible name wired; colour is not the only encoder
- [ ] A path to the underlying rows exists
- [ ] Verified in both themes — the card ground differs, and so does contrast
