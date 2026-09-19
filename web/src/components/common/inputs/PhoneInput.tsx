import {
  AsYouType,
  getCountryCallingCode,
  isValidPhoneNumber,
  parsePhoneNumberFromString,
  type CountryCode,
} from 'libphonenumber-js'
import type { TFunction } from 'i18next'
import { ChevronDown } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { CONTROL_INPUT, CONTROL_SHELL } from './styles'
import type { FieldControlProps } from './types'

const PHONE_COUNTRY_CODES: readonly CountryCode[] = [
  'TR',
  'DE',
  'GB',
  'US',
  'NL',
  'FR',
  'IT',
  'ES',
  'AE',
  'SA',
  'AZ',
  'RU',
]

/** The dial codes a Turkish desk keeps in reach, in the order it needs them. */
export function phoneCountries(t: TFunction<'common'>): { code: CountryCode; name: string }[] {
  const names = t('phoneInput.countries', { returnObjects: true }) as Record<CountryCode, string>
  return PHONE_COUNTRY_CODES.map((code) => ({ code, name: names[code] }))
}

/** ISO 3166 letters map one-to-one onto the regional indicator block. */
function flagOf(country: CountryCode): string {
  return String.fromCodePoint(
    ...country.split('').map((letter) => 0x1f1a5 + letter.charCodeAt(0)),
  )
}

/** True only for numbers that could actually be dialled — for zod refinements. */
export function isPhoneComplete(value: string): boolean {
  return value.length > 0 && isValidPhoneNumber(value)
}

function toNational(value: string, country: CountryCode): string {
  const parsed = parsePhoneNumberFromString(value)
  if (!parsed || parsed.country !== country) return ''
  return parsed.formatNational()
}

export interface PhoneInputProps extends FieldControlProps {
  /** E.164 (`+905321234567`) once the number parses, `''` when empty. */
  value: string
  onValueChange: (value: string) => void
  country?: CountryCode
  onCountryChange?: (country: CountryCode) => void
  countries?: readonly { code: CountryCode; name: string }[]
  placeholder?: string
  disabled?: boolean
  className?: string
}

/**
 * Dial code plus national number, formatted as it is typed.
 *
 * The field shows the number the way its own country writes it — `0532 123 45 67`
 * in Turkey, `(212) 555-0134` in the US — while emitting E.164, which is the
 * only form worth storing. Pasting a full `+…` number switches the country
 * picker to match instead of mangling the digits.
 */
export function PhoneInput({
  value,
  onValueChange,
  country = 'TR',
  onCountryChange,
  countries,
  placeholder,
  disabled,
  className,
  id,
  ...aria
}: PhoneInputProps) {
  const { t } = useTranslation('common')
  const resolvedCountries = countries ?? phoneCountries(t)
  const resolvedPlaceholder = placeholder ?? t('phoneInput.placeholder')
  // The display text is the user's, not a round-trip of the emitted value: a
  // half-typed number has no E.164 form to derive it back from.
  const [text, setText] = useState(() => toNational(value, country))

  const emit = (national: string, forCountry: CountryCode) => {
    const digits = national.replace(/\D/g, '')
    if (digits.length === 0) return onValueChange('')
    const parsed = parsePhoneNumberFromString(national, forCountry)
    onValueChange(parsed?.number ?? `+${getCountryCallingCode(forCountry)}${digits}`)
  }

  const handleInput = (raw: string) => {
    // A pasted `+…` number carries its own country; adopt it and keep the digits.
    if (raw.trimStart().startsWith('+')) {
      const parsed = parsePhoneNumberFromString(raw)
      if (parsed?.country && parsed.country !== country) {
        onCountryChange?.(parsed.country)
        setText(parsed.formatNational())
        onValueChange(parsed.number)
        return
      }
    }
    const formatted = new AsYouType(country).input(raw)
    setText(formatted)
    emit(formatted, country)
  }

  const handleCountry = (next: CountryCode) => {
    onCountryChange?.(next)
    const reformatted = new AsYouType(next).input(text)
    setText(reformatted)
    emit(reformatted, next)
  }

  return (
    <div className={cn(CONTROL_SHELL, className)}>
      {/* The picker shows the flag and dial code; the option list carries the
          country names, which is why it is an overlaid native select rather
          than a styled one — a native select can only show its option's text. */}
      <span className="relative flex shrink-0 items-center border-r border-[var(--nx-hairline)]">
        <span
          aria-hidden
          className="tnum flex items-center gap-1.5 pr-6 pl-3 text-[13.5px] font-[550]"
        >
          {flagOf(country)} +{getCountryCallingCode(country)}
        </span>
        <ChevronDown
          aria-hidden
          strokeWidth={1.7}
          className="text-muted-foreground pointer-events-none absolute right-2.5 size-3.5"
        />
        <select
          aria-label={t('phoneInput.countryCode')}
          value={country}
          disabled={disabled || !onCountryChange}
          onChange={(event) => handleCountry(event.target.value as CountryCode)}
          className="absolute inset-0 cursor-pointer opacity-0 disabled:cursor-not-allowed"
        >
          {resolvedCountries.map(({ code, name }) => (
            <option key={code} value={code}>
              {flagOf(code)} {name} (+{getCountryCallingCode(code)})
            </option>
          ))}
        </select>
      </span>
      <input
        id={id}
        type="tel"
        inputMode="tel"
        autoComplete="tel-national"
        value={text}
        placeholder={resolvedPlaceholder}
        disabled={disabled}
        onChange={(event) => handleInput(event.target.value)}
        className={cn(CONTROL_INPUT, 'tnum')}
        {...aria}
      />
    </div>
  )
}
