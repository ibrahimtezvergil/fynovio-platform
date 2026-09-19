import { setupWorker } from 'msw/browser'
import { handlers } from '@/mocks/handlers'

/** Started from `main.tsx` before the app renders — see `VITE_API_MOCKING` in `.env.example`. */
export const worker = setupWorker(...handlers)
