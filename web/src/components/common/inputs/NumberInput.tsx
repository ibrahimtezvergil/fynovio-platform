import { NumberField } from '@base-ui/react/number-field'
import { Minus, Plus } from 'lucide-react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { CONTROL_ADORNMENT, CONTROL_DIVIDER, CONTROL_INPUT, CONTROL_SHELL } from './styles'
import type { FieldControlProps } from './types'

const STEPPER = [
  'flex w-9 shrink-0 cursor-pointer items-center justify-center border-0 bg-transparent',
  'text-muted-foreground outline-none transition-colors duration-[150ms] ease-fluid',
  'hover:bg-[var(--nx-fill-hover)] hover:text-foreground',
  'data-disabled:pointer-events-none data-disabled:opacity-40',
].join(' ')

export interface NumberInputProps extends FieldControlProps {
  value: number | null
  onValueChange: (value: number | null) => void
  min?: number
  max?: number
  /** `'any'` turns off step validation — what money and free decimals want. */
  step?: number | 'any'
  smallStep?: number
  largeStep?: number
  /** Passed straight to `Intl.NumberFormat`; the field parses in the same shape. */
  format?: Intl.NumberFormatOptions
  locale?: Intl.LocalesArgument
  /** Welded to the left edge — a currency symbol, a `#`. */
  prefix?: string
  /** Welded to the right edge — `%`, `kg`, `gün`. */
  suffix?: string
  /** An interactive tail inside the same shell: a currency or unit picker. */
  trailing?: ReactNode
  /** Minus/plus buttons on either flank. Off by default: money rarely wants them. */
  steppers?: boolean
  placeholder?: string
  disabled?: boolean
  readOnly?: boolean
  className?: string
  inputClassName?: string
  /** Figures read right by default; `'left'` is for counters that aren't money. */
  align?: 'left' | 'right'
}

/**
 * The numeric base every financial control in this kit is built on. Base UI's
 * number field owns parsing, clamping and locale formatting — it formats on
 * blur, so typing stays raw and the committed value is always a real `number`.
 */
export function NumberInput({
  value,
  onValueChange,
  min,
  max,
  step = 1,
  smallStep,
  largeStep,
  format,
  locale = 'tr-TR',
  prefix,
  suffix,
  trailing,
  steppers = false,
  placeholder,
  disabled,
  readOnly,
  className,
  inputClassName,
  align = 'right',
  id,
  ...aria
}: NumberInputProps) {
  const { t } = useTranslation('common')
  return (
    <NumberField.Root
      id={id}
      value={value}
      onValueChange={onValueChange}
      min={min}
      max={max}
      step={step}
      smallStep={smallStep}
      largeStep={largeStep}
      format={format}
      locale={locale}
      disabled={disabled}
      readOnly={readOnly}
      className={cn('w-full', className)}
    >
      <NumberField.Group className={CONTROL_SHELL}>
        {steppers && (
          <NumberField.Decrement
            className={cn(STEPPER, 'border-r border-[var(--nx-hairline)]')}
            aria-label={t('numberInput.decrement')}
          >
            <Minus aria-hidden className="size-3.5" strokeWidth={2} />
          </NumberField.Decrement>
        )}
        {prefix && <span className={cn(CONTROL_ADORNMENT, 'pr-0')}>{prefix}</span>}
        <NumberField.Input
          placeholder={placeholder}
          className={cn(
            CONTROL_INPUT,
            'tnum',
            align === 'right' ? 'text-right' : 'text-left',
            // The stepper buttons already stand in for the field's own padding,
            // and every pixel spent on it is a digit lost in a narrow column.
            steppers && 'px-1.5',
            prefix && 'pl-1.5',
            suffix && !steppers && 'pr-1.5',
            inputClassName,
          )}
          {...aria}
        />
        {steppers && (
          <NumberField.Increment
            className={cn(STEPPER, CONTROL_DIVIDER)}
            aria-label={t('numberInput.increment')}
          >
            <Plus aria-hidden className="size-3.5" strokeWidth={2} />
          </NumberField.Increment>
        )}
        {suffix && (
          <span className={cn(CONTROL_ADORNMENT, !steppers && 'pl-0')}>{suffix}</span>
        )}
        {trailing}
      </NumberField.Group>
    </NumberField.Root>
  )
}
