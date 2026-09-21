import { QueryClient } from '@tanstack/react-query'
import { fireEvent, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { OverlayHost } from '@/components/common/OverlayHost'
import { endpoints } from '@/api/endpoints'
import { useSessionStore } from '@/lib/auth'
import { useOverlayStore } from '@/lib/overlay'
import { server } from '@/mocks/server'
import { authenticated, problem, url } from '@/test/authHandlers'
import { accessibleLink, wireAllDayEntry, wireEntry } from '@/test/calendar'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import CalendarPage from './CalendarPage'

const t = (key: string, options?: Record<string, unknown>) => tr(key, options, 'calendar')

// jsdom has no layout or media queries; the theme hook and FullCalendar's sizing read them.
beforeAll(() => {
  window.matchMedia ??= ((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener: () => {},
    removeEventListener: () => {},
    addListener: () => {},
    removeListener: () => {},
    dispatchEvent: () => false,
  })) as typeof window.matchMedia
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
})

beforeEach(() => {
  vi.stubEnv('TZ', 'Europe/Istanbul')
  vi.useFakeTimers({ toFake: ['Date'], now: new Date('2026-09-21T10:00:00+03:00') })
  resetSession()
  useOverlayStore.setState({ entries: [] })
  useSessionStore.getState().applyAuthResult(authenticated({ activeTenant: { tenantId: 1 } }))
})
afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllEnvs()
})

const render = (initialEntry = '/calendar') =>
  renderRoutes(
    [
      { path: '/crm/opportunities/:id', element: <p>OPPORTUNITY PAGE</p> },
      {
        path: '/calendar',
        element: (
          <>
            <CalendarPage />
            <OverlayHost />
          </>
        ),
      },
    ],
    initialEntry,
    new QueryClient({ defaultOptions: { queries: { retryDelay: 0, staleTime: 0 } } }),
  )

describe('CalendarPage against the real API contract', () => {
  it('requests the visible window as UTC from/to once the grid reports it, and renders the entries', async () => {
    const seen: URLSearchParams[] = []
    server.use(
      http.get(url(endpoints.calendar.entries), ({ request }) => {
        seen.push(new URL(request.url).searchParams)
        return HttpResponse.json({ items: [wireEntry({ startAt: '2026-09-22T06:00:00+00:00', endAt: '2026-09-22T07:00:00+00:00' }), wireAllDayEntry({ id: 43, title: 'Conference', startDate: '2026-09-23', endDate: '2026-09-25' })] })
      }),
    )
    render()

    expect(await screen.findByText('Call with vendor')).toBeInTheDocument()
    expect(screen.getByText('Conference')).toBeInTheDocument()
    expect(seen.length).toBeGreaterThanOrEqual(1)
    const { from, to } = Object.fromEntries(seen[0])
    expect(from).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/)
    expect(to).toMatch(/Z$/)
    expect(Date.parse(to) - Date.parse(from)).toBeLessThanOrEqual(100 * 86_400_000) // within the API's 100-day cap
    expect(new Date(from) < new Date('2026-09-21T00:00:00+03:00') && new Date(to) > new Date('2026-09-21T00:00:00+03:00')).toBe(true)
    expect(screen.getByText(t('page.recordCount', { count: 2 }))).toBeInTheDocument()
  })

  it('paints each coloured entry with white text, darkening a light user-selected hue only when needed', async () => {
    server.use(http.get(url(endpoints.calendar.entries), () => HttpResponse.json({ items: [wireEntry({ startAt: '2026-09-22T06:00:00+00:00', endAt: '2026-09-22T07:00:00+00:00', color: '#ffff00' })] })))
    render()
    const title = await screen.findByText('Call with vendor')
    const styled = title.closest<HTMLElement>('[style]')
    expect(styled?.style.getPropertyValue('--fc-event-contrast-color')).toBe('#ffffff')
    expect(styled?.outerHTML).not.toMatch(/#ffff00|rgb\(255, 255, 0\)/i)
  })

  it('a rejected range is an inline, translated notice — not a crashed page', async () => {
    server.use(http.get(url(endpoints.calendar.entries), () => problem(422, 'range_too_large')))
    render()
    expect(await screen.findByText(t('problem.rangeTooLarge.title'))).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: t('page.title') })).toBeInTheDocument()
  })

  it('a transient failure survives the one automatic retry, then offers Try again', async () => {
    let calls = 0
    server.use(http.get(url(endpoints.calendar.entries), () => (++calls <= 2 ? problem(500, 'internal') : HttpResponse.json({ items: [wireEntry({ startAt: '2026-09-22T06:00:00+00:00', endAt: '2026-09-22T07:00:00+00:00' })] }))))
    render()
    fireEvent.click(await screen.findByRole('button', { name: t('problem.retry') }))
    expect(await screen.findByText('Call with vendor')).toBeInTheDocument()
  })

  it('the Event button opens the create dialog', async () => {
    server.use(http.get(url(endpoints.calendar.entries), () => HttpResponse.json({ items: [] })))
    render()
    fireEvent.click(await screen.findByRole('button', { name: t('createEvent') }))
    expect(await screen.findByRole('dialog', { name: t('form.createTitle') })).toBeInTheDocument()
  })

  it('clicking an entry opens its details', async () => {
    server.use(http.get(url(endpoints.calendar.entries), () => HttpResponse.json({ items: [wireEntry({ startAt: '2026-09-22T06:00:00+00:00', endAt: '2026-09-22T07:00:00+00:00', link: accessibleLink() })] })))
    render()
    fireEvent.click(await screen.findByText('Call with vendor'))
    const dialog = await screen.findByRole('dialog', { name: 'Call with vendor' })
    expect(dialog).toHaveTextContent('OPP-17 — Acme')
  })

  it('has no legend of event kinds any more', async () => {
    server.use(http.get(url(endpoints.calendar.entries), () => HttpResponse.json({ items: [] })))
    render()
    await screen.findByRole('button', { name: t('createEvent') })
    expect(screen.queryByText(/Etkinlik türleri|Event types/)).not.toBeInTheDocument()
  })
})

describe('"Add to calendar" hand-off (?link=)', () => {
  it('opens the create dialog with the record linked, then removes the parameter', async () => {
    server.use(http.get(url(endpoints.calendar.entries), () => HttpResponse.json({ items: [] })))
    const { router } = render('/calendar?link=crm%2Fopportunity%2F17')

    const dialog = await screen.findByRole('dialog', { name: t('form.createTitle') })
    expect(dialog).toHaveTextContent(t('link.fallback.opportunity', { id: 17 }))
    await waitFor(() => expect(router.state.location.search).toBe(''))
    expect(useOverlayStore.getState().entries).toHaveLength(1) // exactly one dialog (StrictMode-safe)
  })

  it.each(['crm/opportunity/abc', 'crm/quote/1', 'x'])('ignores a malformed or unsupported link %j and still drops it from the URL', async (value) => {
    server.use(http.get(url(endpoints.calendar.entries), () => HttpResponse.json({ items: [] })))
    const { router } = render(`/calendar?link=${encodeURIComponent(value)}`)
    await waitFor(() => expect(router.state.location.search).toBe(''))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
})
