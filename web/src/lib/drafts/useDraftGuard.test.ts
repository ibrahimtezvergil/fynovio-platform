import { describe, expect, it } from 'vitest'
import { reviveDates } from './useDraftGuard'

describe('reviveDates', () => {
  it('round-trips a Date through JSON.stringify/parse', () => {
    const closeDate = new Date('2026-03-05T00:00:00.000Z')
    const serialized = JSON.stringify({ closeDate, customer: 'akdeniz-lojistik' })
    const restored = JSON.parse(serialized, reviveDates) as { closeDate: Date; customer: string }

    expect(restored.closeDate).toBeInstanceOf(Date)
    expect(restored.closeDate.getTime()).toBe(closeDate.getTime())
    expect(restored.customer).toBe('akdeniz-lojistik')
  })

  it('leaves a null field and an ordinary string untouched', () => {
    const serialized = JSON.stringify({ closeDate: null, note: '2026-03-05' })
    const restored = JSON.parse(serialized, reviveDates) as { closeDate: null; note: string }

    expect(restored.closeDate).toBeNull()
    // Date-only text isn't the ISO-8601-with-time shape JSON.stringify emits for Date.
    expect(restored.note).toBe('2026-03-05')
  })
})
