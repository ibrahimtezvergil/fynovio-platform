const HEX_COLOR = /^#[0-9a-f]{6}$/i

type ReadableTextColor = '#000000' | '#ffffff'

export interface CalendarHeaderPalette {
  start: string
  middle: string
  end: string
  textColor: ReadableTextColor
}

/** sRGB channel (0-255) to linear light, per WCAG 2.x. */
function linearChannel(channel: number): number {
  const value = channel / 255
  return value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4
}

/** WCAG relative luminance of a `#rrggbb` colour (0 = black, 1 = white). */
export function relativeLuminance(hex: string): number {
  if (!HEX_COLOR.test(hex)) throw new Error(`Expected a #rrggbb colour, got "${hex}".`)
  const [red, green, blue] = [1, 3, 5].map((offset) => linearChannel(Number.parseInt(hex.slice(offset, offset + 2), 16)))
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue
}

/** WCAG contrast ratio between two `#rrggbb` colours (1 to 21). */
export function contrastRatio(first: string, second: string): number {
  const [lighter, darker] = [relativeLuminance(first), relativeLuminance(second)].sort((a, b) => b - a)
  return (lighter + 0.05) / (darker + 0.05)
}

function mixHexColors(first: string, second: string, secondWeight: number): string {
  if (!HEX_COLOR.test(first) || !HEX_COLOR.test(second)) throw new Error('Expected #rrggbb colours.')
  if (secondWeight < 0 || secondWeight > 1) throw new Error('Expected a mix weight between 0 and 1.')

  const firstWeight = 1 - secondWeight
  const channel = (offset: number) =>
    Math.round(Number.parseInt(first.slice(offset, offset + 2), 16) * firstWeight + Number.parseInt(second.slice(offset, offset + 2), 16) * secondWeight)
      .toString(16)
      .padStart(2, '0')

  return `#${channel(1)}${channel(3)}${channel(5)}`
}

/**
 * Black or white, whichever reads better on the given fill (WCAG 1.4.3). The user picks any `#rrggbb`, so the
 * text colour has to be computed from the fill rather than assumed from a theme token.
 */
export function readableTextColor(background: string): ReadableTextColor {
  return contrastRatio(background, '#000000') >= contrastRatio(background, '#ffffff') ? '#000000' : '#ffffff'
}

/**
 * A tonal ramp for the event-detail header. It only moves away from the base colour in the direction that improves
 * the selected text colour's contrast, so a user-selected colour remains both recognisable and AA-readable across
 * the whole gradient.
 */
export function calendarHeaderPalette(color: string): CalendarHeaderPalette {
  if (!HEX_COLOR.test(color)) throw new Error(`Expected a #rrggbb colour, got "${color}".`)

  const middle = color.toLowerCase()
  const textColor = readableTextColor(middle)
  const accent = textColor === '#000000' ? '#ffffff' : '#000000'

  return {
    start: mixHexColors(middle, accent, 0.18),
    middle,
    end: mixHexColors(middle, accent, 0.05),
    textColor,
  }
}
