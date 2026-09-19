import { OTPField } from '@base-ui/react/otp-field'
import { cn } from '@/lib/utils'
import type { FieldControlProps } from './types'

export interface OtpInputProps extends FieldControlProps {
  value: string
  onValueChange: (value: string) => void
  length?: number
  /** Fires once every slot is filled — submit the code instead of waiting. */
  onComplete?: (value: string) => void
  disabled?: boolean
  className?: string
}

/**
 * One box per character for a verification code. A single input under the
 * hood, so paste, autofill and the OS's SMS suggestion all still work.
 */
export function OtpInput({
  value,
  onValueChange,
  length = 6,
  onComplete,
  disabled,
  className,
  id,
  ...aria
}: OtpInputProps) {
  return (
    <OTPField.Root
      id={id}
      value={value}
      onValueChange={onValueChange}
      onValueComplete={onComplete}
      length={length}
      disabled={disabled}
      className={cn('flex gap-2', className)}
      {...aria}
    >
      {Array.from({ length }, (_, index) => (
        <OTPField.Input
          key={index}
          aria-label={index === 0 ? undefined : `${index + 1}. karakter`}
          className={cn(
            'tnum h-11 w-11 rounded-md border border-[var(--nx-hairline)] bg-[var(--nx-fill)]',
            'text-center text-[17px] font-[590] text-foreground outline-none',
            'transition-[background,border-color,box-shadow] duration-[250ms] ease-fluid',
            'hover:border-[var(--nx-hairline-strong)]',
            'focus:border-ring focus:bg-[var(--nx-surface)] focus:shadow-[0_0_0_4px_var(--nx-tint-fill)]',
            'disabled:pointer-events-none disabled:opacity-45',
          )}
        />
      ))}
    </OTPField.Root>
  )
}
