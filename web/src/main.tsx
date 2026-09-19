import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { ErrorBoundary } from '@/components/common/ErrorBoundary'
import App from './App'
import './index.css'

/**
 * No backend exists yet, so MSW stands in at the network boundary for every
 * environment (`npm run dev`, `npm run build` + `preview`) unless explicitly
 * turned off — see `VITE_API_MOCKING` in `.env.example`.
 */
async function enableMocking() {
  if (import.meta.env.VITE_API_MOCKING === 'disabled') return
  const { worker } = await import('@/mocks/browser')
  return worker.start({ onUnhandledRequest: 'bypass' })
}

enableMocking().then(() => {
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <ErrorBoundary>
        <App />
      </ErrorBoundary>
    </StrictMode>,
  )
})
