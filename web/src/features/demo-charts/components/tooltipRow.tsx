import type { ReactNode } from 'react'

/**
 * Row renderer for `ChartTooltipContent`, so a tooltip prints its value in the
 * unit the axis uses (₺, %, gün) instead of the raw number. Keeps the swatch,
 * the series name and the right-aligned figure the default row draws.
 */
export function tooltipRow(format: (value: number) => string) {
  return (value: unknown, name: unknown, item: { color?: string }): ReactNode => (
    <>
      <span
        aria-hidden
        className="size-2.5 shrink-0 self-center rounded-[2px]"
        style={{ background: item.color }}
      />
      <div className="flex flex-1 items-center justify-between gap-5 leading-none">
        <span className="text-muted-foreground">{String(name)}</span>
        <span className="tnum text-foreground font-medium">{format(Number(value))}</span>
      </div>
    </>
  )
}
