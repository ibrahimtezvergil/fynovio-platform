import { setupServer } from 'msw/node'
import { handlers } from '@/mocks/handlers'

/** Node-side interception for Vitest — started from `src/test/setup.ts`. */
export const server = setupServer(...handlers)
