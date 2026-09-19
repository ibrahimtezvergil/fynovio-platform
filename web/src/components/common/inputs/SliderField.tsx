import { Slider } from '@base-ui/react/slider'
import { cn } from '@/lib/utils'
import type { FieldControlProps } from './types'

const THUMB = [
  'size-[18px] rounded-full border border-[var(--nx-hairline-strong)] bg-white select-none',
  'shadow-[var(--nx-knob-shadow)] transition-transform duration-[150ms] ease-fluid',
  'hover:scale-110 has-[:focus-visible]:ring-3 has-[:focus-visible]:ring-ring/40',
].join(' ')

export interface SliderFieldProps<V extends number | number[]> extends FieldControlProps {
  value: V
  onValueChange: (value: V) => void
  min?: number
  max?: number
  step?: number
  /** Formats the readout above the track — currency, `km`, a plain count. */
  format?: Intl.NumberFormatOptions
  /** Names each thumb when the field has no visible label of its own. */
  thumbLabels?: readonly string[]
  disabled?: boolean
  className?: string
}

/**
 * A bounded number set by dragging: a search radius, a budget band, a discount
 * ceiling. It trades precision for speed, so it belongs on filters — never on
 * the figure that ends up on an invoice.
 */
export function SliderField<V extends number | number[]>({
  value,
  onValueChange,
  min = 0,
  max = 100,
  step = 1,
  format,
  thumbLabels,
  disabled,
  className,
  ...aria
}: SliderFieldProps<V>) {
  const thumbs = Array.isArray(value) ? value.length : 1

  return (
    <Slider.Root
      value={value}
      onValueChange={(next) => onValueChange(next as V)}
      min={min}
      max={max}
      step={step}
      format={format}
      locale="tr-TR"
      disabled={disabled}
      className={cn('flex w-full flex-col gap-1', className)}
      {...aria}
    >
      <Slider.Value className="tnum self-end text-[12.5px] font-[550]" />
      <Slider.Control className="flex w-full touch-none items-center py-2 select-none">
        <Slider.Track className="h-1.5 w-full rounded-full bg-[var(--nx-track)] select-none">
          <Slider.Indicator className="rounded-full bg-[image:var(--nx-accent-grad)] select-none" />
          {Array.from({ length: thumbs }, (_, index) => (
            <Slider.Thumb
              key={index}
              index={index}
              aria-label={thumbLabels?.[index]}
              className={THUMB}
            />
          ))}
        </Slider.Track>
      </Slider.Control>
    </Slider.Root>
  )
}
