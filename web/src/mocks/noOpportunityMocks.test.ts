import { describe, expect, it } from 'vitest'
import { handlers } from '@/mocks/handlers'

/**
 * Guard: MSW must never answer the Opportunity API (Phase 2.5B spec section 20) or the Calendar API (real
 * Collaboration module). This test ensures that no handler in the mock setup mocks `/opportunities`, `/pipelines`
 * or `/calendar` endpoints; feature tests define their own per-test handlers instead.
 */
describe('MSW handlers — no opportunity, pipeline or calendar mocks', () => {
  it('handlers array is not empty', () => {
    expect(handlers.length).toBeGreaterThan(0)
  })

  it('no handler mocks /opportunities endpoints', () => {
    handlers.forEach((handler) => {
      const path = handler.info.path
      const pathStr = typeof path === 'string' ? path : path instanceof RegExp ? path.source : String(path)
      expect(pathStr).not.toContain('/opportunities')
    })
  })

  it('no handler mocks /pipelines endpoints', () => {
    handlers.forEach((handler) => {
      const path = handler.info.path
      const pathStr = typeof path === 'string' ? path : path instanceof RegExp ? path.source : String(path)
      expect(pathStr).not.toContain('/pipelines')
    })
  })

  it('no handler mocks /calendar endpoints', () => {
    handlers.forEach((handler) => {
      const path = handler.info.path
      const pathStr = typeof path === 'string' ? path : path instanceof RegExp ? path.source : String(path)
      expect(pathStr).not.toContain('/calendar')
    })
  })
})
