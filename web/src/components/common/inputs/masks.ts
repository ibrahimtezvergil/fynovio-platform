/**
 * Sanitise / display pairs for the identifiers a Turkish CRM or ERP records,
 * plus the check digits that say whether one was typed correctly. Validation
 * lives here rather than in a zod schema so the same rule can light up the
 * control while typing and gate the submit.
 */

/** Inserts `separator` every `size` characters — the shape of an IBAN or a card. */
export function groupEvery(value: string, size: number, separator = ' '): string {
  return value.replace(new RegExp(`(.{${size}})`, 'g'), `$1${separator}`).trim()
}

/* ---- IBAN ---------------------------------------------------------------- */

export const IBAN_MAX_LENGTH = 26 // TR: 2 country + 2 check + 22 BBAN

export const sanitizeIban = (raw: string) => raw.toUpperCase().replace(/[^A-Z0-9]/g, '')
export const displayIban = (clean: string) => groupEvery(clean, 4)

/**
 * ISO 13616 mod-97: move the first four characters to the end, expand letters
 * to their alphabet position, and the remainder against 97 must be 1.
 */
export function isValidIban(clean: string): boolean {
  if (clean.length < 15 || clean.length > 34) return false
  const rearranged = clean.slice(4) + clean.slice(0, 4)
  const expanded = rearranged.replace(/[A-Z]/g, (letter) =>
    String(letter.charCodeAt(0) - 55),
  )
  // The number outruns Number.MAX_SAFE_INTEGER, so fold it nine digits at a time.
  let remainder = 0
  for (const digit of expanded) {
    remainder = (remainder * 10 + Number(digit)) % 97
  }
  return remainder === 1
}

/* ---- tax and citizen identifiers ----------------------------------------- */

export const sanitizeDigits = (raw: string) => raw.replace(/\D/g, '')

/** TCKN — 11 digits with two trailing check digits. */
export function isValidTckn(value: string): boolean {
  if (!/^[1-9]\d{10}$/.test(value)) return false
  const digits = [...value].map(Number)
  const odd = digits[0] + digits[2] + digits[4] + digits[6] + digits[8]
  const even = digits[1] + digits[3] + digits[5] + digits[7]
  // `odd * 7 - even` can go negative, and JS `%` keeps the sign.
  if (((((odd * 7 - even) % 10) + 10) % 10) !== digits[9]) return false
  return digits.slice(0, 10).reduce((sum, digit) => sum + digit, 0) % 10 === digits[10]
}

/** VKN — 10 digits, weighted so the last digit closes the sum to a multiple of ten. */
export function isValidVkn(value: string): boolean {
  if (!/^\d{10}$/.test(value)) return false
  const digits = [...value].map(Number)
  let total = 0
  for (let index = 0; index < 9; index += 1) {
    const term = (digits[index] + 9 - index) % 10
    total += term === 9 ? term : (term * 2 ** (9 - index)) % 9
  }
  return (10 - (total % 10)) % 10 === digits[9]
}

/** A counterparty is a company (10) or a person (11); accept whichever fits. */
export function isValidTaxId(value: string): boolean {
  return value.length === 10 ? isValidVkn(value) : isValidTckn(value)
}

/* ---- payment card -------------------------------------------------------- */

export const CARD_MAX_LENGTH = 19

export const displayCard = (clean: string) => groupEvery(clean, 4)

/** Luhn — catches a mistyped digit or a transposition, nothing more. */
export function isValidCardNumber(clean: string): boolean {
  if (clean.length < 13) return false
  let sum = 0
  let double = false
  for (let index = clean.length - 1; index >= 0; index -= 1) {
    let digit = Number(clean[index])
    if (double) {
      digit *= 2
      if (digit > 9) digit -= 9
    }
    sum += digit
    double = !double
  }
  return sum % 10 === 0
}

/** Enough to pick the right brand mark; not a validation. */
export function cardBrand(clean: string): 'visa' | 'mastercard' | 'amex' | 'troy' | null {
  if (/^4/.test(clean)) return 'visa'
  if (/^(5[1-5]|2[2-7])/.test(clean)) return 'mastercard'
  if (/^3[47]/.test(clean)) return 'amex'
  if (/^9792/.test(clean)) return 'troy'
  return null
}

/* ---- card expiry and security code --------------------------------------- */

export const displayExpiry = (clean: string) =>
  clean.length > 2 ? `${clean.slice(0, 2)}/${clean.slice(2)}` : clean

/** `MMYY`, a real month, and not already past. */
export function isValidExpiry(clean: string, now = new Date()): boolean {
  if (!/^\d{4}$/.test(clean)) return false
  const month = Number(clean.slice(0, 2))
  if (month < 1 || month > 12) return false
  const year = 2000 + Number(clean.slice(2))
  // A card is good through the last day of its month.
  return new Date(year, month, 1) > now
}

/** Three digits, four on Amex. */
export const isValidCvc = (clean: string, amex = false) =>
  new RegExp(`^\\d{${amex ? 4 : 3}}$`).test(clean)
