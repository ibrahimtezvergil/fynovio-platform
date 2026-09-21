import { QueryClient } from '@tanstack/react-query'
import { fireEvent, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { useSessionStore } from '@/lib/auth'
import { paths } from '@/routes/paths'
import { authenticated } from '@/test/authHandlers'
import { mockDetailApi, mockParties, recordRequests } from '@/test/opportunities'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import OpportunityDetailPage from './OpportunityDetailPage'

const t = (key: string) => tr(key, undefined, 'opportunities')

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated())
})

describe('"Add to calendar" on the opportunity detail page', () => {
  it('is a real link that hands the opportunity to the calendar as a URL parameter (features never import each other)', async () => {
    mockParties()
    mockDetailApi(12, recordRequests())
    const { router } = renderRoutes(
      [
        { path: '/crm/opportunities/:id', element: <OpportunityDetailPage /> },
        { path: '/calendar', element: <p>CALENDAR PAGE</p> },
      ],
      '/crm/opportunities/12',
      new QueryClient({ defaultOptions: { queries: { retryDelay: 0 } } }),
    )

    const link = await screen.findByRole('link', { name: t('detail.addToCalendar') })
    expect(link).toHaveAttribute('href', paths.calendarWithLink('crm', 'opportunity', 12))
    expect(link).toHaveAttribute('href', '/calendar?link=crm%2Fopportunity%2F12')

    fireEvent.click(link)
    expect(await screen.findByText('CALENDAR PAGE')).toBeInTheDocument()
    expect(router.state.location.pathname + router.state.location.search).toBe('/calendar?link=crm%2Fopportunity%2F12')
  })
})
