import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import { I18nextProvider } from 'react-i18next'
import { createMemoryRouter, RouterProvider, type RouteObject } from 'react-router-dom'
import { i18n } from '@/lib/i18n'

/** Text as the UI renders it (default language), so tests don't hard-code copy. */
export const tr = (key: string, options?: Record<string, unknown>, ns = 'auth') => i18n.t(key, { ns, ...options }) as string

export function renderRoutes(routes: RouteObject[], initialEntry: string, queryClient = new QueryClient()) {
  const router = createMemoryRouter(routes, { initialEntries: [initialEntry] })
  const view = render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </I18nextProvider>,
  )
  return { router, queryClient, ...view }
}

/** One page inside a memory router (plus a `/next` landing route so navigation is observable). */
export function renderPage(element: ReactElement, initialEntry = '/', queryClient?: QueryClient) {
  return renderRoutes(
    [
      { path: '/next', element: <p>LANDED</p> },
      { path: '*', element },
    ],
    initialEntry,
    queryClient,
  )
}
