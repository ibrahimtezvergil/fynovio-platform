import { Checkbox } from '@base-ui/react/checkbox'
import { CheckboxGroup } from '@base-ui/react/checkbox-group'
import { Radio } from '@base-ui/react/radio'
import { RadioGroup } from '@base-ui/react/radio-group'
import { ToggleGroup } from '@base-ui/react/toggle-group'
import { Toggle } from '@base-ui/react/toggle'
import { Check } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { FieldControlProps, SelectOption } from './types'

/* ---- radio --------------------------------------------------------------- */

const RADIO_DOT = [
  'relative flex size-[18px] shrink-0 cursor-pointer items-center justify-center rounded-full',
  'border border-[var(--nx-hairline-strong)] bg-[var(--nx-fill)] outline-none',
  'transition-[background,border-color] duration-[250ms] ease-fluid',
  'hover:border-[var(--nx-tint)]',
  'data-checked:border-transparent data-checked:bg-[image:var(--nx-accent-grad)]',
  'focus-visible:ring-3 focus-visible:ring-ring/40',
  'data-disabled:pointer-events-none data-disabled:opacity-45',
].join(' ')

export interface RadioGroupFieldProps<T extends string> extends FieldControlProps {
  value: T | null
  onValueChange: (value: T) => void
  options: readonly SelectOption<T>[]
  /** Horizontal only when every label is short — otherwise it wraps badly. */
  orientation?: 'vertical' | 'horizontal'
  disabled?: boolean
  className?: string
}

/** One choice from a handful, all of them visible at once. */
export function RadioGroupField<T extends string>({
  value,
  onValueChange,
  options,
  orientation = 'vertical',
  disabled,
  className,
  ...aria
}: RadioGroupFieldProps<T>) {
  return (
    <RadioGroup
      value={value}
      onValueChange={(next) => onValueChange(next as T)}
      disabled={disabled}
      className={cn(
        'flex gap-x-5 gap-y-2.5',
        orientation === 'vertical' ? 'flex-col' : 'flex-wrap items-center',
        className,
      )}
      {...aria}
    >
      {options.map((option) => (
        <label
          key={option.value}
          className="flex cursor-pointer items-start gap-2.5 text-[13.5px] has-data-disabled:cursor-not-allowed has-data-disabled:opacity-45"
        >
          <Radio.Root value={option.value} disabled={option.disabled} className={cn(RADIO_DOT, 'mt-px')}>
            <Radio.Indicator className="size-[7px] rounded-full bg-white" />
          </Radio.Root>
          <span className="flex min-w-0 flex-col">
            <span className="leading-[18px]">{option.label}</span>
            {option.description && (
              <span className="text-muted-foreground text-[11.5px]">{option.description}</span>
            )}
          </span>
        </label>
      ))}
    </RadioGroup>
  )
}

/** The same choice as tiles — for the two or three that decide a whole form. */
export function RadioCards<T extends string>({
  value,
  onValueChange,
  options,
  disabled,
  className,
  ...aria
}: Omit<RadioGroupFieldProps<T>, 'orientation'>) {
  return (
    <RadioGroup
      value={value}
      onValueChange={(next) => onValueChange(next as T)}
      disabled={disabled}
      className={cn('grid gap-2.5 sm:grid-cols-2 lg:grid-cols-3', className)}
      {...aria}
    >
      {options.map((option) => {
        const selected = option.value === value
        return (
          <label
            key={option.value}
            className={cn(
              'flex cursor-pointer items-start gap-3 rounded-lg border p-3',
              'transition-[background,border-color,box-shadow] duration-[250ms] ease-fluid',
              selected
                ? 'border-ring bg-accent shadow-[inset_0_1px_0_var(--nx-specular)]'
                : 'border-[var(--nx-hairline)] bg-[var(--nx-fill)] hover:border-[var(--nx-hairline-strong)]',
              'has-data-disabled:pointer-events-none has-data-disabled:opacity-45',
            )}
          >
            <Radio.Root value={option.value} disabled={option.disabled} className={cn(RADIO_DOT, 'mt-0.5')}>
              <Radio.Indicator className="size-[7px] rounded-full bg-white" />
            </Radio.Root>
            <span className="flex min-w-0 flex-col gap-0.5">
              <span className="flex items-center gap-1.5 text-[13.5px] font-[550]">
                {option.icon && (
                  <option.icon aria-hidden className="text-brand-graphic size-4" strokeWidth={1.75} />
                )}
                {option.label}
              </span>
              {option.description && (
                <span className="text-muted-foreground text-[11.5px] leading-4">
                  {option.description}
                </span>
              )}
            </span>
          </label>
        )
      })}
    </RadioGroup>
  )
}

/* ---- checkbox group ------------------------------------------------------ */

export interface CheckboxGroupFieldProps<T extends string> extends FieldControlProps {
  value: readonly T[]
  onValueChange: (value: T[]) => void
  options: readonly SelectOption<T>[]
  orientation?: 'vertical' | 'horizontal'
  disabled?: boolean
  className?: string
}

/** Any number of the options, including none — permissions, channels, filters. */
export function CheckboxGroupField<T extends string>({
  value,
  onValueChange,
  options,
  orientation = 'vertical',
  disabled,
  className,
  ...aria
}: CheckboxGroupFieldProps<T>) {
  return (
    <CheckboxGroup
      value={[...value]}
      onValueChange={(next) => onValueChange(next as T[])}
      disabled={disabled}
      className={cn(
        'flex gap-x-5 gap-y-2.5',
        orientation === 'vertical' ? 'flex-col' : 'flex-wrap items-center',
        className,
      )}
      {...aria}
    >
      {options.map((option) => (
        <label
          key={option.value}
          className="flex cursor-pointer items-start gap-2.5 text-[13.5px] has-data-disabled:cursor-not-allowed has-data-disabled:opacity-45"
        >
          <Checkbox.Root
            value={option.value}
            disabled={option.disabled}
            className={cn(
              'mt-px flex size-[18px] shrink-0 cursor-pointer items-center justify-center rounded-[6px]',
              'border border-[var(--nx-hairline-strong)] bg-[var(--nx-fill)] outline-none',
              'transition-[background,border-color] duration-[250ms] ease-fluid hover:border-[var(--nx-tint)]',
              'data-checked:border-transparent data-checked:bg-[image:var(--nx-accent-grad)]',
              'focus-visible:ring-3 focus-visible:ring-ring/40',
              'data-disabled:pointer-events-none data-disabled:opacity-45',
            )}
          >
            <Checkbox.Indicator className="flex text-white">
              <Check aria-hidden className="size-3" strokeWidth={3} />
            </Checkbox.Indicator>
          </Checkbox.Root>
          <span className="flex min-w-0 flex-col">
            <span className="leading-[18px]">{option.label}</span>
            {option.description && (
              <span className="text-muted-foreground text-[11.5px]">{option.description}</span>
            )}
          </span>
        </label>
      ))}
    </CheckboxGroup>
  )
}

/* ---- toggle group -------------------------------------------------------- */

export interface ToggleGroupFieldProps<T extends string> extends FieldControlProps {
  value: readonly T[]
  onValueChange: (value: T[]) => void
  options: readonly SelectOption<T>[]
  /** `false` makes it a single-choice bar — the weekday picker wants `true`. */
  multiple?: boolean
  disabled?: boolean
  className?: string
}

/**
 * A row of pressable buttons. Same semantics as a checkbox group, but sized
 * for values short enough to read as a bar: weekdays, warehouses, day parts.
 */
export function ToggleGroupField<T extends string>({
  value,
  onValueChange,
  options,
  multiple = true,
  disabled,
  className,
  ...aria
}: ToggleGroupFieldProps<T>) {
  return (
    <ToggleGroup
      multiple={multiple}
      value={[...value]}
      onValueChange={(next) => onValueChange(next as T[])}
      disabled={disabled}
      className={cn('flex flex-wrap gap-1.5', className)}
      {...aria}
    >
      {options.map((option) => (
        <Toggle
          key={option.value}
          value={option.value}
          disabled={option.disabled}
          aria-label={option.description ?? option.label}
          className={cn(
            'flex h-9 min-w-9 cursor-pointer items-center justify-center gap-1.5 rounded-sm px-3',
            'border border-[var(--nx-hairline)] bg-[var(--nx-fill)] text-[12.5px] font-[550]',
            'text-muted-foreground outline-none transition-[background,border-color,color] duration-[250ms] ease-fluid',
            'hover:border-[var(--nx-hairline-strong)] hover:text-foreground',
            'data-pressed:border-ring data-pressed:bg-accent data-pressed:text-accent-foreground',
            'focus-visible:ring-3 focus-visible:ring-ring/40',
            'data-disabled:pointer-events-none data-disabled:opacity-45',
          )}
        >
          {option.icon && <option.icon aria-hidden className="size-4" strokeWidth={1.75} />}
          {option.label}
        </Toggle>
      ))}
    </ToggleGroup>
  )
}
