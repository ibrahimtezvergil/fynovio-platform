import { describe, expect, it } from 'vitest'
import { calendarHeaderPalette, contrastRatio, readableTextColor, relativeLuminance } from './contrast'

describe('relativeLuminance', () => {
  it('is 0 for black and 1 for white', () => {
    expect(relativeLuminance('#000000')).toBe(0)
    expect(relativeLuminance('#ffffff')).toBeCloseTo(1, 10)
  })

  it('weights green above red above blue', () => {
    expect(relativeLuminance('#00ff00')).toBeGreaterThan(relativeLuminance('#ff0000'))
    expect(relativeLuminance('#ff0000')).toBeGreaterThan(relativeLuminance('#0000ff'))
  })

  it('accepts upper-case input and rejects anything that is not #rrggbb', () => {
    expect(relativeLuminance('#AABBCC')).toBeCloseTo(relativeLuminance('#aabbcc'), 10)
    for (const bad of ['', 'red', '#fff', '#12345', '#1234567', '123456', 'var(--background)']) {
      expect(() => relativeLuminance(bad)).toThrow()
    }
  })
})

describe('contrastRatio', () => {
  it('is 21:1 for black on white, in either order', () => {
    expect(contrastRatio('#000000', '#ffffff')).toBeCloseTo(21, 5)
    expect(contrastRatio('#ffffff', '#000000')).toBeCloseTo(21, 5)
  })

  it('is 1:1 for identical colours', () => {
    expect(contrastRatio('#3b82f6', '#3b82f6')).toBeCloseTo(1, 10)
  })

  it('matches the published ratio for #767676 on white (4.54:1, the AA boundary grey)', () => {
    expect(contrastRatio('#767676', '#ffffff')).toBeCloseTo(4.54, 2)
  })
})

describe('readableTextColor', () => {
  it.each([
    ['#ffffff', '#000000'],
    ['#000000', '#ffffff'],
    ['#ffff00', '#000000'],
    ['#f0af3c', '#000000'],
    ['#3cb4cd', '#000000'],
    ['#0000ff', '#ffffff'],
    ['#6355c7', '#ffffff'],
    ['#c0243a', '#ffffff'],
    ['#8a90a6', '#000000'],
  ])('%s → %s', (background, expected) => {
    expect(readableTextColor(background)).toBe(expected)
  })

  it('always clears 4.5:1 on the palette the colour picker offers, and never picks the weaker option', () => {
    for (const swatch of ['#6355c7', '#3c8cf0', '#3cb4cd', '#28b478', '#f0af3c', '#e4693c', '#c0243a', '#8a90a6']) {
      const chosen = readableTextColor(swatch)
      const other = chosen === '#000000' ? '#ffffff' : '#000000'
      expect(contrastRatio(swatch, chosen)).toBeGreaterThanOrEqual(contrastRatio(swatch, other))
      expect(contrastRatio(swatch, chosen)).toBeGreaterThanOrEqual(4.5)
    }
  })
})

describe('calendarHeaderPalette', () => {
  it('uses white text on every coloured header and keeps it AA-readable throughout the tonal ramp', () => {
    for (const color of ['#6355c7', '#3c8cf0', '#3cb4cd', '#28b478', '#f0af3c', '#e4693c', '#c0243a', '#8a90a6']) {
      const palette = calendarHeaderPalette(color)

      expect(palette.textColor).toBe('#ffffff')
      for (const stop of [palette.start, palette.middle, palette.end]) {
        expect(contrastRatio(stop, palette.textColor)).toBeGreaterThanOrEqual(4.5)
      }
    }
  })

  it('normalises upper-case input before producing CSS-ready stops', () => {
    expect(calendarHeaderPalette('#3C8CF0')).toEqual(calendarHeaderPalette('#3c8cf0'))
  })
})
