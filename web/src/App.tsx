import { QueryClientProvider } from '@tanstack/react-query'
import { lazy, Suspense, useEffect } from 'react'
import { I18nextProvider } from 'react-i18next'
import { RouterProvider } from 'react-router-dom'
import { queryClient } from '@/api/queryClient'
import { Toaster } from '@/components/ui/sonner'
import { TooltipProvider } from '@/components/ui/tooltip'
import { SessionGate } from '@/lib/auth'
import { CapabilityProvider } from '@/lib/capabilities'
import { i18n } from '@/lib/i18n'
import { router } from '@/routes'
import { useResolvedTheme } from '@/store/useAppStore'

const ReactQueryDevtools = import.meta.env.DEV
  ? lazy(() =>
      import('@tanstack/react-query-devtools').then((module) => ({
        default: module.ReactQueryDevtools,
      })),
    )
  : null

export default function App() {
  const resolvedTheme = useResolvedTheme()

  // Keeps the class honest for both paths: an explicit choice, and "system"
  // flipping under the tab while it is open.
  useEffect(() => {
    document.documentElement.classList.toggle('dark', resolvedTheme === 'dark')
  }, [resolvedTheme])

  return (
    <I18nextProvider i18n={i18n}>
      <CapabilityProvider>
        <QueryClientProvider client={queryClient}>
          {/* One provider at the root: it owns the shared open/close delay, so
              moving between neighbouring triggers skips the re-open wait. */}
          <TooltipProvider delay={350} closeDelay={80}>
            <SessionGate>
              <RouterProvider router={router} />
            </SessionGate>
          </TooltipProvider>
          <Toaster position="top-left" />
          {ReactQueryDevtools && (
            <Suspense fallback={null}>
              <ReactQueryDevtools />
            </Suspense>
          )}
        </QueryClientProvider>
      </CapabilityProvider>
    </I18nextProvider>
  )
}
