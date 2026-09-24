import { QueryClient } from '@tanstack/react-query'
import { fireEvent, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { OverlayHost } from '@/components/common/OverlayHost'
import { useSessionStore } from '@/lib/auth'
import { useOverlayStore } from '@/lib/overlay'
import { authenticated } from '@/test/authHandlers'
import { mockDetailApi, mockParties, recordRequests } from '@/test/opportunities'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import OpportunityDetailPage from './OpportunityDetailPage'

const t = (key: string) => tr(key, undefined, 'opportunities')

beforeEach(() => {
  resetSession()
  useOverlayStore.setState({ entries: [] })
  useSessionStore.getState().applyAuthResult(authenticated())
})

describe('"Add to calendar" on the opportunity detail page', () => {
  it('opens the linked create dialog without leaving the opportunity page', async () => {
    mockParties()
    mockDetailApi(12, recordRequests())
    const { router } = renderRoutes(
      [
        { path: '/crm/opportunities/:id', element: <><OpportunityDetailPage /><OverlayHost /></> },
        { path: '/calendar', element: <p>CALENDAR PAGE</p> },
      ],
      '/crm/opportunities/12',
      new QueryClient({ defaultOptions: { queries: { retryDelay: 0 } } }),
    )

    fireEvent.click(await screen.findByRole('button', { name: t('detail.addToCalendar') }))
    expect(await screen.findByRole('dialog', { name: tr('form.createTitle', undefined, 'calendar') })).toBeInTheDocument()
    expect(screen.getByText(tr('link.fallback.opportunity', { id: 12 }, 'calendar'))).toBeInTheDocument()
    expect(router.state.location.pathname + router.state.location.search).toBe('/crm/opportunities/12')
  })
})
