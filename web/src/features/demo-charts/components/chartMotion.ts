/**
 * Recharts draws its entry animation on `requestAnimationFrame`, which a
 * background tab throttles to a stop: a chart that mounts while the tab is
 * hidden freezes at frame zero and never finishes drawing — an empty card when
 * the reader finally switches to it. These are reports, not motion pieces, so
 * every mark is spread with this and paints in its final position.
 */
export const STATIC_MARK = { isAnimationActive: false } as const
