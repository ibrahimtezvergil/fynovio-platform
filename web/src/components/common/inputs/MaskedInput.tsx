import { Check } from 'lucide-react'
import { useEffect, useRef, type ChangeEvent, type ReactNode } from 'react'
import { cn } from '@/lib/utils'
import {
  CARD_MAX_LENGTH,
  IBAN_MAX_LENGTH,
  cardBrand,
  displayCard,
  displayExpiry,
  displayIban,
  isValidCardNumber,
  isValidCvc,
  isValidExpiry,
  isValidIban,
  isValidTaxId,
  sanitizeDigits,
  sanitizeIban,
} from './masks'
import { CONTROL_ADORNMENT, CONTROL_INPUT, CONTROL_SHELL } from './styles'
import type { FieldControlProps } from './types'

export interface MaskedInputProps extends FieldControlProps {
  /** The unmasked value — the only thing worth storing or validating. */
  value: string
  onValueChange: (value: string) => void
  /** Drops every character the mask does not accept. Applied per keystroke. */
  sanitize: (raw: string) => string
  /** Groups the sanitized value for display. */
  display: (clean: string) => string
  maxLength?: number
  placeholder?: string
  inputMode?: 'text' | 'numeric'
  autoComplete?: string
  disabled?: boolean
  className?: string
  trailing?: ReactNode
}

/**
 * Where the caret belongs in `formatted` once `cleanIndex` accepted characters
 * have gone past it — so grouping an IBAN mid-edit doesn't throw the caret to
 * the end of the field.
 */
function caretAfter(formatted: string, cleanIndex: number, sanitize: (raw: string) => string) {
  let seen = 0
  for (let index = 0; index < formatted.length; index += 1) {
    if (seen >= cleanIndex) return index
    if (sanitize(formatted[index]).length > 0) seen += 1
  }
  return formatted.length
}

/**
 * A text field that stores one value and shows another: digits in, grouped
 * digits on screen. Every identifier a CRM records — IBAN, tax number, card —
 * is this same control with a different `sanitize`/`display` pair.
 */
export function MaskedInput({
  value,
  onValueChange,
  sanitize,
  display,
  maxLength,
  placeholder,
  inputMode = 'text',
  autoComplete,
  disabled,
  className,
  trailing,
  id,
  ...aria
}: MaskedInputProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const caretRef = useRef<number | null>(null)

  // Restoring after the controlled re-render, not inside the handler: the DOM
  // still holds the pre-format text while the event is being processed.
  useEffect(() => {
    if (caretRef.current === null || !inputRef.current) return
    inputRef.current.setSelectionRange(caretRef.current, caretRef.current)
    caretRef.current = null
  })

  const handleChange = (event: ChangeEvent<HTMLInputElement>) => {
    const raw = event.target.value
    const typedCaret = event.target.selectionStart ?? raw.length
    const clean = sanitize(raw).slice(0, maxLength)
    caretRef.current = caretAfter(
      display(clean),
      Math.min(sanitize(raw.slice(0, typedCaret)).length, clean.length),
      sanitize,
    )
    onValueChange(clean)
  }

  return (
    <div className={cn(CONTROL_SHELL, className)}>
      <input
        ref={inputRef}
        id={id}
        type="text"
        inputMode={inputMode}
        autoComplete={autoComplete}
        spellCheck={false}
        value={display(value)}
        placeholder={placeholder}
        disabled={disabled}
        onChange={handleChange}
        className={cn(CONTROL_INPUT, 'tnum tracking-[0.02em]')}
        {...aria}
      />
      {trailing}
    </div>
  )
}

/** A green tick once the check digits agree — quiet until then. */
function ValidMark({ valid }: { valid: boolean }) {
  if (!valid) return null
  return (
    <span className={cn(CONTROL_ADORNMENT, 'text-[var(--nx-pos)]')} aria-hidden>
      <Check className="size-4" strokeWidth={2.25} />
    </span>
  )
}

type PresetProps = Omit<
  MaskedInputProps,
  'sanitize' | 'display' | 'maxLength' | 'inputMode' | 'trailing'
>

/** IBAN, grouped in fours, with a live mod-97 check. */
export function IbanInput({ value, ...props }: PresetProps) {
  return (
    <MaskedInput
      value={value}
      sanitize={sanitizeIban}
      display={displayIban}
      maxLength={IBAN_MAX_LENGTH}
      placeholder="TR00 0000 0000 0000 0000 0000 00"
      trailing={<ValidMark valid={isValidIban(value)} />}
      {...props}
    />
  )
}

/** VKN (10 digits) or TCKN (11) — whichever the counterparty has. */
export function TaxIdInput({ value, ...props }: PresetProps) {
  return (
    <MaskedInput
      value={value}
      sanitize={sanitizeDigits}
      display={(clean) => clean}
      maxLength={11}
      inputMode="numeric"
      placeholder="VKN (10) veya TCKN (11)"
      trailing={<ValidMark valid={isValidTaxId(value)} />}
      {...props}
    />
  )
}

/** Card number in groups of four, checked with Luhn. */
export function CardNumberInput({ value, ...props }: PresetProps) {
  return (
    <MaskedInput
      value={value}
      sanitize={sanitizeDigits}
      display={displayCard}
      maxLength={CARD_MAX_LENGTH}
      inputMode="numeric"
      autoComplete="cc-number"
      placeholder="0000 0000 0000 0000"
      trailing={<ValidMark valid={isValidCardNumber(value)} />}
      {...props}
    />
  )
}

/** Expiry as `MM/YY`, rejected once the month is behind us. */
export function CardExpiryInput({ value, ...props }: PresetProps) {
  return (
    <MaskedInput
      value={value}
      sanitize={sanitizeDigits}
      display={displayExpiry}
      maxLength={4}
      inputMode="numeric"
      autoComplete="cc-exp"
      placeholder="AA/YY"
      trailing={<ValidMark valid={isValidExpiry(value)} />}
      {...props}
    />
  )
}

/** Security code: three digits, four on an Amex — hence the card number. */
export function CardCvcInput({
  value,
  cardNumber = '',
  ...props
}: PresetProps & { cardNumber?: string }) {
  const amex = cardBrand(cardNumber) === 'amex'
  return (
    <MaskedInput
      value={value}
      sanitize={sanitizeDigits}
      display={(clean) => clean}
      maxLength={amex ? 4 : 3}
      inputMode="numeric"
      autoComplete="cc-csc"
      placeholder={amex ? '0000' : '000'}
      trailing={<ValidMark valid={isValidCvc(value, amex)} />}
      {...props}
    />
  )
}
