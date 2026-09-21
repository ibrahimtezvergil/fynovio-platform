import { Check } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { FieldControlProps } from './types'

/** The tag palette: distinguishable at 16px, legible in both themes. */
export const COLOR_SWATCHES = [
  '#6355C7',
  '#3C8CF0',
  '#3CB4CD',
  '#28B478',
  '#F0AF3C',
  '#E4693C',
  '#C0243A',
  '#8A90A6',
] as const

export interface ColorInputProps extends FieldControlProps {
  /** A `#rrggbb` string — the one form every `<input type="color">` agrees on. */
  value: string
  onValueChange: (value: string) => void
  swatches?: readonly string[]
  disabled?: boolean
  className?: string
}

/**
 * Preset swatches first, a full picker second. Category and tag colours want
 * to stay inside a known palette; the eyedropper is the escape hatch, not the
 * default path.
 */
export function ColorInput({
  value,
  onValueChange,
  swatches = COLOR_SWATCHES,
  disabled,
  className,
  id,
  ...aria
}: ColorInputProps) {
  const customColorSelected = !swatches.some((swatch) => swatch.toLowerCase() === value.toLowerCase())

  return (
    <div
      role="group"
      className={cn('flex flex-wrap items-center gap-1.5', className)}
      {...aria}
    >
      {swatches.map((swatch) => {
        const selected = swatch.toLowerCase() === value.toLowerCase()
        return (
          <button
            key={swatch}
            type="button"
            aria-label={swatch}
            aria-pressed={selected}
            disabled={disabled}
            onClick={() => onValueChange(swatch)}
            style={{ background: swatch }}
            className={cn(
              'flex size-7 cursor-pointer items-center justify-center rounded-sm border-0 text-white',
              'shadow-[inset_0_1px_0_rgb(255_255_255/0.25)] outline-none',
              'transition-transform duration-[150ms] ease-fluid hover:scale-108',
              'focus-visible:ring-3 focus-visible:ring-ring/40',
              selected && 'ring-2 ring-ring ring-offset-2 ring-offset-[var(--nx-canvas)]',
              'disabled:pointer-events-none disabled:opacity-45',
            )}
          >
            {selected && <Check aria-hidden className="size-3.5" strokeWidth={3} />}
          </button>
        )
      })}
      <label
        className={cn(
          'ml-1 flex h-7 cursor-pointer items-center gap-2 rounded-sm border border-[var(--nx-hairline)]',
          'bg-[var(--nx-fill)] px-2.5 text-[12px] font-[550] text-muted-foreground',
          'transition-colors duration-[250ms] ease-fluid hover:border-[var(--nx-hairline-strong)]',
          customColorSelected && 'ring-2 ring-ring ring-offset-2 ring-offset-[var(--nx-canvas)]',
        )}
      >
        <span
          aria-hidden
          className="size-3.5 rounded-full border border-[var(--nx-hairline-strong)]"
          style={{ background: value }}
        />
        {customColorSelected && <Check aria-hidden className="size-3.5" strokeWidth={3} />}
        <span className="tnum uppercase">{value}</span>
        <input
          id={id}
          type="color"
          value={value}
          disabled={disabled}
          onChange={(event) => onValueChange(event.target.value)}
          className="size-0 opacity-0"
        />
      </label>
    </div>
  )
}
