import { describe, expect, it } from 'vitest'
import { handlers } from '@/mocks/handlers'

/**
 * Guard: MSW must never answer the Opportunity API (Phase 2.5B spec section 20).
 * The real Opportunity API endpoints are only available after Phase 2.5B implementation.
 * This test ensures that no handlers in the mock setup accidentally mock `/opportunities` or `/pipelines` endpoints.
 */
describe('MSW handlers — no opportunity or pipeline mocks', () => {
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
})
