/**
 * Series colours for the gallery.
 *
 * `--color-chart-1…5` in `src/index.css` are the app's canonical chart hues,
 * but they are not five *categorical* hues: 1 and 3 are both violet and 4 and 5
 * are green/teal. Run through a colour-vision check the closest pairs land
 * around ΔE 5–9, well under the 15 a full-colour reader needs to tell two
 * fills apart. Only three of the five separate cleanly in both themes, so a
 * chart that encodes identity by colour uses these, in this fixed order, and
 * never more than three series.
 *
 * More than three categories is not a colour problem — it is the wrong form.
 * Rank them and use one hue (see `RAMP`), or split the chart.
 */
export const SERIES = [
  'var(--color-chart-1)', // violet — the app accent
  'var(--color-chart-2)', // amber
  'var(--color-chart-5)', // teal
] as const

/**
 * Sequential ramp for ranked magnitudes: one hue, stepped by how much of it
 * covers the card. Opacity rather than a second colour keeps it monotonic on
 * the white card and on the dark one alike.
 */
export const RAMP = [1, 0.76, 0.54, 0.36, 0.22] as const

/** Pipeline stages keep the tokens the board and the badges already use. */
export const STAGE_COLOR = {
  new: 'var(--color-stage-new)',
  contacted: 'var(--color-stage-contacted)',
  quoted: 'var(--color-stage-quoted)',
  meeting: 'var(--color-stage-meeting)',
  ready: 'var(--color-stage-ready)',
} as const

/** Outcome tones — reserved status colours, never reused as a series hue. */
export const STATUS_COLOR = {
  positive: 'var(--color-success)',
  negative: 'var(--destructive)',
} as const

/** Gap punched between stacked segments and neighbouring bars. */
export const SURFACE_GAP = { stroke: 'var(--card)', strokeWidth: 2 } as const
