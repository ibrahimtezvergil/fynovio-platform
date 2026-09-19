import { afterEach, describe, expect, it, vi } from 'vitest'
import { track } from '@/lib/telemetry'

describe('track', () => {
  afterEach(() => vi.restoreAllMocks())

  it('emits the event through the telemetry seam', () => {
    const debug = vi.spyOn(console, 'debug').mockImplementation(() => undefined)

    track('example_event', { source: 'test' })

    expect(debug).toHaveBeenCalledWith('[telemetry]', 'example_event', { source: 'test' })
  })
})
