import { QueryClient } from '@tanstack/react-query'
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { OverlayHost } from '@/components/common/OverlayHost'
import { endpoints } from '@/api/endpoints'
import { useSessionStore } from '@/lib/auth'
import { openDialog, useOverlayStore } from '@/lib/overlay'
import { server } from '@/mocks/server'
import { authenticated, problem, url } from '@/test/authHandlers'
import { accessibleLink, unavailableLink, wireAllDayEntry, wireEntry } from '@/test/calendar'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import { useCalendarEntries } from '../api'
import { calendarEntrySchema, type CalendarEntry } from '../schema'
import { EntryDialog } from './EntryDialog'
import type { EntryPrefill } from './EntryForm'

const t = (key: string, options?: Record<string, unknown>) => tr(key, options, 'calendar')
const RANGE = { from: '2026-08-31T21:00:00.000Z', to: '2026-10-12T21:00:00.000Z' }

const parse = (wire: Record<string, unknown>): CalendarEntry => calendarEntrySchema.parse(wire)

/** Timed, Monday 21 Sept 2026 09:00–10:00 (Istanbul), so the expected offsets in the assertions are fixed. */
const PREFILL: EntryPrefill = {
  fields: { allDay: false, startDate: '2026-09-21', startTime: '09:00', endDate: '2026-09-21', endTime: '10:00' },
}

/** The page's role in the real app: it keeps the list query active and mounts the one overlay host. */
function Host() {
  useCalendarEntries(RANGE)
  return <OverlayHost />
}

function renderHost(store: { entries: unknown[] } = { entries: [] }) {
  server.use(http.get(url(endpoints.calendar.entries), () => HttpResponse.json({ items: store.entries })))
  return renderRoutes(
    [
      { path: '/crm/opportunities/:id', element: <p>OPPORTUNITY PAGE</p> },
      { path: '*', element: <Host /> },
    ],
    '/calendar',
    new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 0 } } }),
  )
}

const open = (entry?: CalendarEntry, prefill?: EntryPrefill) =>
  act(() => {
    void openDialog({ content: <EntryDialog entry={entry} prefill={prefill} />, className: 'sm:max-w-[560px]' })
  })

const dialog = () => screen.findByRole('dialog')
const field = (key: string) => screen.getByLabelText(t(key))
const type = (key: string, value: string) => fireEvent.change(field(key), { target: { value } })
const save = () => fireEvent.click(screen.getByRole('button', { name: t('form.save') }))

interface Sent {
  method: string
  path: string
  key: string | null
  body: Record<string, unknown> | null
  expected: string | null
}

/** Records every write; `respond` answers them (default: the happy 201/200/204). */
function mockWrites(respond?: (sent: Sent, index: number) => Response | Promise<Response>) {
  const sent: Sent[] = []
  const handle = (method: string, status: number, body: unknown) => async ({ request }: { request: Request }) => {
    const text = await request.text()
    const record: Sent = {
      method,
      path: new URL(request.url).pathname,
      key: request.headers.get('Idempotency-Key'),
      body: text ? (JSON.parse(text) as Record<string, unknown>) : null,
      expected: new URL(request.url).searchParams.get('expectedVersion'),
    }
    sent.push(record)
    return respond ? respond(record, sent.length - 1) : status === 204 ? new HttpResponse(null, { status }) : HttpResponse.json(body as object, { status })
  }
  server.use(
    http.post(url(endpoints.calendar.entries), handle('POST', 201, { id: 42, rowVersion: 1, replayed: false })),
    http.put(url(`${endpoints.calendar.entries}/:id`), handle('PUT', 200, { id: 42, rowVersion: 4, replayed: false })),
    http.delete(url(`${endpoints.calendar.entries}/:id`), handle('DELETE', 204, null)),
  )
  return sent
}

beforeEach(() => {
  vi.stubEnv('TZ', 'Europe/Istanbul')
  resetSession()
  useOverlayStore.setState({ entries: [] })
  useSessionStore.getState().applyAuthResult(authenticated({ activeTenant: { tenantId: 1 } }))
})
afterEach(() => vi.unstubAllEnvs())

describe('create', () => {
  it('sends offset-bearing times, the default colour, the link ref and an Idempotency-Key — and closes on success', async () => {
    const sent = mockWrites()
    renderHost()
    open(undefined, { ...PREFILL, link: { boundedContext: 'crm', entityType: 'opportunity', id: 17 } })
    await dialog()

    expect(screen.getByText(t('link.fallback.opportunity', { id: 17 }))).toBeInTheDocument() // the chip, until the server names it
    type('form.title.label', '  Vendor call  ')
    save()

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(sent).toHaveLength(1)
    expect(sent[0]).toMatchObject({ method: 'POST', path: '/api/calendar/entries' })
    expect(sent[0].body).toEqual({
      title: 'Vendor call', // trimmed
      notes: null,
      color: '#3c8cf0',
      allDay: false,
      startAt: '2026-09-21T09:00:00+03:00',
      endAt: '2026-09-21T10:00:00+03:00',
      startDate: null,
      endDate: null,
      link: { boundedContext: 'crm', entityType: 'opportunity', id: 17 },
    })
    expect(sent[0].key).toMatch(/^.{1,128}$/)
  })

  it('an all-day entry sends dates with an exclusive end', async () => {
    const sent = mockWrites()
    renderHost()
    open(undefined, PREFILL)
    await dialog()

    fireEvent.click(screen.getByRole('switch', { name: t('form.allDay') }))
    expect(screen.queryByLabelText(t('form.startTime'))).not.toBeInTheDocument() // times do not apply
    type('form.title.label', 'Conference')
    save()

    await waitFor(() => expect(sent).toHaveLength(1))
    expect(sent[0].body).toMatchObject({ allDay: true, startAt: null, endAt: null, startDate: '2026-09-21', endDate: '2026-09-22' })
  })

  it('an empty end time stores a point in time; notes and a picked swatch are sent as typed (colour lower-cased)', async () => {
    const sent = mockWrites()
    renderHost()
    open(undefined, PREFILL)
    await dialog()

    type('form.title.label', 'Reminder')
    type('form.endTime', '')
    type('form.notes.label', 'Bring the price list')
    fireEvent.click(screen.getByRole('button', { name: '#28B478' }))
    save()

    await waitFor(() => expect(sent).toHaveLength(1))
    expect(sent[0].body).toMatchObject({ endAt: null, notes: 'Bring the price list', color: '#28b478' })
  })

  it('shows field errors and sends nothing while the form is invalid', async () => {
    const sent = mockWrites()
    renderHost()
    open(undefined, PREFILL)
    await dialog()

    save()
    expect(await screen.findByText(t('form.title.required'))).toBeInTheDocument()

    type('form.title.label', 'x')
    type('form.endTime', '08:00')
    save()
    expect(await screen.findByText(t('form.end.beforeStart'))).toBeInTheDocument()
    expect(field('form.endTime')).toHaveAttribute('aria-invalid', 'true')
    expect(sent).toHaveLength(0)
  })

  it('keeps one idempotency key across an unknown outcome (5xx), and mints a new one after a definitive answer', async () => {
    const sent = mockWrites((_, index) => (index === 0 ? problem(500, 'internal') : index === 1 ? problem(400, 'validation_error') : HttpResponse.json({ id: 42, rowVersion: 1, replayed: false }, { status: 201 })))
    renderHost()
    open(undefined, PREFILL)
    await dialog()
    type('form.title.label', 'Call')

    save() // 1: 500 — the server may or may not have committed
    expect(await screen.findByText(t('problem.unavailable.title'))).toBeInTheDocument()
    save() // 2: the retry replays the SAME key
    await waitFor(() => expect(sent).toHaveLength(2))
    expect(sent[1].key).toBe(sent[0].key)

    expect(await screen.findByText(t('problem.validation.title'))).toBeInTheDocument() // a definitive 400 releases the key
    save() // 3: a new logical attempt
    await waitFor(() => expect(sent).toHaveLength(3))
    expect(sent[2].key).not.toBe(sent[0].key)
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('a different payload never reuses the key, even after an unknown outcome', async () => {
    const sent = mockWrites((_, index) => (index === 0 ? problem(500, 'internal') : HttpResponse.json({ id: 42, rowVersion: 1, replayed: false }, { status: 201 })))
    renderHost()
    open(undefined, PREFILL)
    await dialog()
    type('form.title.label', 'Call')
    save()
    await screen.findByText(t('problem.unavailable.title'))

    type('form.title.label', 'Call (changed)')
    save()
    await waitFor(() => expect(sent).toHaveLength(2))
    expect(sent[1].key).not.toBe(sent[0].key)
  })

  it('a link the server refuses (422 link_target_unavailable) is explained inline, and removing the link clears it from the request', async () => {
    const sent = mockWrites((record) => (record.body?.link ? problem(422, 'link_target_unavailable') : HttpResponse.json({ id: 42, rowVersion: 1, replayed: false }, { status: 201 })))
    renderHost()
    open(undefined, { ...PREFILL, link: { boundedContext: 'crm', entityType: 'opportunity', id: 17 } })
    await dialog()
    type('form.title.label', 'Call')

    save()
    expect(await screen.findByText(t('problem.linkUnavailable.title'))).toBeInTheDocument()
    expect(screen.getByText(t('problem.linkUnavailable.description'))).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: t('form.link.remove') }))
    expect(screen.queryByText(t('link.fallback.opportunity', { id: 17 }))).not.toBeInTheDocument()
    save()
    await waitFor(() => expect(sent).toHaveLength(2))
    expect(sent[1].body?.link).toBeNull()
  })

  it('never shows the server’s own error text', async () => {
    mockWrites(() => HttpResponse.json({ type: 'validation_error', title: 'INTERNAL: null reference at Foo.Bar', status: 400 }, { status: 400 }))
    renderHost()
    open(undefined, PREFILL)
    await dialog()
    type('form.title.label', 'Call')
    save()
    expect(await screen.findByText(t('problem.validation.description'))).toBeInTheDocument()
    expect(screen.queryByText(/INTERNAL/)).not.toBeInTheDocument()
  })
})

describe('edit', () => {
  const entry = () => parse(wireEntry({ link: accessibleLink() }))

  it('opens in view mode, edits into a prefilled form, and PUTs the full entry with expectedVersion', async () => {
    const sent = mockWrites()
    renderHost({ entries: [wireEntry({ link: accessibleLink() })] })
    open(entry())
    await dialog()
    expect(screen.getByRole('heading', { name: 'Call with vendor' })).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: t('edit') }))
    expect(field('form.title.label')).toHaveValue('Call with vendor')
    expect(field('form.notes.label')).toHaveValue('Bring the price list')
    expect(field('form.startTime')).toHaveValue('09:00') // 06:00Z shown in the browser's zone
    expect(screen.getByText('OPP-17 — Acme')).toBeInTheDocument()

    type('form.title.label', 'Call with vendor (moved)')
    save()

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(sent).toHaveLength(1)
    expect(sent[0]).toMatchObject({ method: 'PUT', path: '/api/calendar/entries/42' })
    expect(sent[0].body).toEqual({
      title: 'Call with vendor (moved)',
      notes: 'Bring the price list',
      color: '#3b82f6',
      allDay: false,
      startAt: '2026-09-21T09:00:00+03:00',
      endAt: '2026-09-21T10:00:00+03:00',
      startDate: null,
      endDate: null,
      link: { boundedContext: 'crm', entityType: 'opportunity', id: 17 }, // kept: a replace with no link would clear it
      expectedVersion: 3,
    })
  })

  it('removing the link chip sends `link: null` (a replace without a link clears it)', async () => {
    const sent = mockWrites()
    renderHost({ entries: [wireEntry({ link: accessibleLink() })] })
    open(entry())
    await dialog()
    fireEvent.click(screen.getByRole('button', { name: t('edit') }))
    fireEvent.click(screen.getByRole('button', { name: t('form.link.remove') }))
    save()
    await waitFor(() => expect(sent).toHaveLength(1))
    expect(sent[0].body?.link).toBeNull()
  })

  it('an unavailable link shows a neutral chip (never its name) and can be removed', async () => {
    const sent = mockWrites()
    renderHost({ entries: [] })
    open(parse(wireEntry({ link: { ...unavailableLink(), label: 'Secret deal' } })))
    await dialog()
    fireEvent.click(screen.getByRole('button', { name: t('edit') }))
    expect(screen.getByText(t('link.unavailable'))).toBeInTheDocument()
    expect(screen.queryByText('Secret deal')).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: t('form.link.remove') }))
    save()
    await waitFor(() => expect(sent).toHaveLength(1))
    expect(sent[0].body?.link).toBeNull()
  })

  it('an all-day entry edits with an inclusive last day and saves the exclusive end again', async () => {
    const sent = mockWrites()
    renderHost()
    open(parse(wireAllDayEntry({ startDate: '2026-09-21', endDate: '2026-09-24' })))
    await dialog()
    fireEvent.click(screen.getByRole('button', { name: t('edit') }))
    expect(screen.getByRole('switch', { name: t('form.allDay') })).toBeChecked()
    save()
    await waitFor(() => expect(sent).toHaveLength(1))
    expect(sent[0].body).toMatchObject({ allDay: true, startDate: '2026-09-21', endDate: '2026-09-24' }) // unchanged round trip
  })

  it('a stale version (409 concurrency_conflict) is explained inline and "Load latest" shows what the server now holds', async () => {
    const store = { entries: [wireEntry({ link: accessibleLink() })] as unknown[] }
    mockWrites(() => {
      store.entries = [wireEntry({ rowVersion: 7, title: 'Renamed by someone else' })]
      return problem(409, 'concurrency_conflict')
    })
    renderHost(store)
    open(entry())
    await dialog()
    await waitFor(() => expect(screen.getByRole('button', { name: t('edit') })).toBeInTheDocument())
    fireEvent.click(screen.getByRole('button', { name: t('edit') }))
    type('form.title.label', 'My edit')
    save()

    expect(await screen.findByText(t('problem.concurrency.title'))).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: t('problem.reload') }))
    expect(await screen.findByRole('heading', { name: 'Renamed by someone else' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: t('edit') })).toBeInTheDocument() // back in view mode, with the fresh version
  })

  it('cancel from the form returns to the details; cancel on a new entry closes the dialog', async () => {
    mockWrites()
    renderHost()
    open(entry())
    await dialog()
    fireEvent.click(screen.getByRole('button', { name: t('edit') }))
    fireEvent.click(screen.getByRole('button', { name: t('cancel') }))
    expect(screen.getByRole('heading', { name: 'Call with vendor' })).toBeInTheDocument()

    // The dialog offers Close twice (its corner control and the footer button); either dismisses it.
    fireEvent.click(screen.getAllByRole('button', { name: t('close') }).at(-1) as HTMLElement)
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    open(undefined, PREFILL)
    await dialog()
    fireEvent.click(screen.getByRole('button', { name: t('cancel') }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })
})

describe('delete', () => {
  it('asks first, then DELETEs with expectedVersion as a query parameter and an Idempotency-Key, and closes', async () => {
    const sent = mockWrites()
    renderHost()
    open(parse(wireEntry()))
    await dialog()

    fireEvent.click(screen.getByRole('button', { name: t('delete.action') }))
    expect(screen.getByRole('heading', { name: t('delete.title') })).toBeInTheDocument()
    expect(sent).toHaveLength(0) // nothing is deleted by the first click
    fireEvent.click(screen.getByRole('button', { name: t('delete.confirm') }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(sent).toHaveLength(1)
    expect(sent[0]).toMatchObject({ method: 'DELETE', path: '/api/calendar/entries/42', expected: '3', body: null })
    expect(sent[0].key).toMatch(/^.{1,128}$/)
  })

  it('cancelling the confirmation goes back to the details without a request', async () => {
    const sent = mockWrites()
    renderHost()
    open(parse(wireEntry()))
    await dialog()
    fireEvent.click(screen.getByRole('button', { name: t('delete.action') }))
    fireEvent.click(screen.getByRole('button', { name: t('cancel') }))
    expect(screen.getByRole('heading', { name: 'Call with vendor' })).toBeInTheDocument()
    expect(sent).toHaveLength(0)
  })

  it.each([
    [404, 'not_found', 'notFound'],
    [409, 'concurrency_conflict', 'concurrency'],
    [403, 'forbidden', 'forbidden'],
  ])('a %i %s stays open with an inline explanation', async (status, type, kind) => {
    mockWrites(() => problem(status, type))
    renderHost()
    open(parse(wireEntry()))
    await dialog()
    fireEvent.click(screen.getByRole('button', { name: t('delete.action') }))
    fireEvent.click(screen.getByRole('button', { name: t('delete.confirm') }))
    expect(await screen.findByText(t(`problem.${kind}.title`))).toBeInTheDocument()
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })

  it('a network failure keeps the key, so pressing Delete again replays instead of double-deleting', async () => {
    const sent = mockWrites((_, index) => (index === 0 ? HttpResponse.error() : new HttpResponse(null, { status: 204 })))
    renderHost()
    open(parse(wireEntry()))
    await dialog()
    fireEvent.click(screen.getByRole('button', { name: t('delete.action') }))
    fireEvent.click(screen.getByRole('button', { name: t('delete.confirm') }))
    await screen.findByText(t('problem.unavailable.title'))
    fireEvent.click(screen.getByRole('button', { name: t('delete.confirm') }))
    await waitFor(() => expect(sent).toHaveLength(2))
    expect(sent[1].key).toBe(sent[0].key)
  })
})

describe('link rendering states', () => {
  it('accessible crm/opportunity: a real link to /crm/opportunities/:id that navigates and closes the dialog', async () => {
    renderHost()
    open(parse(wireEntry({ link: accessibleLink() })))
    const link = await within(await dialog()).findByRole('link', { name: new RegExp('OPP-17 — Acme') })
    expect(link).toHaveAttribute('href', '/crm/opportunities/17')
    expect(link).toHaveTextContent('Proposal')

    fireEvent.click(link)
    expect(await screen.findByText('OPPORTUNITY PAGE')).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('accessible type with no route (party in v1): the label, but no link', async () => {
    renderHost()
    open(parse(wireEntry({ link: accessibleLink({ ref: { boundedContext: 'masterdata', entityType: 'party', id: 5 }, label: 'Acme Ltd', subtitle: undefined }) })))
    const view = within(await dialog())
    expect(view.getByText(/Acme Ltd/)).toBeInTheDocument()
    expect(view.queryByRole('link')).not.toBeInTheDocument()
  })

  it('unavailable: nothing — no link, no label, no "linked record" row — even if a label leaked into the payload', async () => {
    renderHost()
    open(parse(wireEntry({ link: { ...unavailableLink(), label: 'Secret deal', subtitle: 'Confidential' } })))
    const view = within(await dialog())
    expect(view.queryByRole('link')).not.toBeInTheDocument()
    expect(view.queryByText(/Secret deal/)).not.toBeInTheDocument()
    expect(view.queryByText(/Confidential/)).not.toBeInTheDocument()
    expect(view.queryByText(t('link.linked'), { exact: false })).not.toBeInTheDocument()
  })

  it('no link: no link row at all', async () => {
    renderHost()
    open(parse(wireEntry()))
    const view = within(await dialog())
    expect(view.queryByRole('link')).not.toBeInTheDocument()
    expect(view.queryByText(t('link.linked'), { exact: false })).not.toBeInTheDocument()
  })
})

describe('accessibility', () => {
  it('the dialog is named by its title, moves focus inside, and Escape closes it', async () => {
    renderHost()
    open(undefined, PREFILL)
    const form = await screen.findByRole('dialog', { name: t('form.createTitle') })
    await waitFor(() => expect(form).toContainElement(document.activeElement as HTMLElement))

    fireEvent.keyDown(form, { key: 'Escape' })
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('every control has an accessible name; the colour choices are named by value and their selection is not colour-only', async () => {
    renderHost()
    open(undefined, PREFILL)
    await dialog()
    for (const key of ['form.title.label', 'form.startTime', 'form.endTime', 'form.notes.label']) expect(field(key)).toBeInTheDocument()
    expect(screen.getByRole('switch', { name: t('form.allDay') })).toBeInTheDocument()
    expect(screen.getAllByLabelText(t('form.startDate')).length).toBeGreaterThan(0)
    expect(screen.getAllByLabelText(t('form.endDate')).length).toBeGreaterThan(0)

    const selected = screen.getByRole('button', { name: '#3C8CF0' })
    expect(selected).toHaveAttribute('aria-pressed', 'true') // state exposed programmatically, and drawn as a check mark
    expect(screen.getByRole('button', { name: '#28B478' })).toHaveAttribute('aria-pressed', 'false')
  })

  it('a field error is announced and tied to its control', async () => {
    renderHost()
    open(undefined, PREFILL)
    await dialog()
    save()
    const message = await screen.findByText(t('form.title.required'))
    expect(message).toHaveAttribute('role', 'alert')
    expect(field('form.title.label')).toHaveAttribute('aria-invalid', 'true')
    expect(field('form.title.label').getAttribute('aria-describedby')).toBe(message.id)
  })

  it('a failed save is announced as an alert', async () => {
    mockWrites(() => problem(500, 'internal'))
    renderHost()
    open(undefined, PREFILL)
    await dialog()
    type('form.title.label', 'Call')
    save()
    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(t('problem.unavailable.title'))
  })

  it('the colour of an entry is also printed as text in the details', async () => {
    renderHost()
    open(parse(wireEntry()))
    expect(await within(await dialog()).findByText('#3b82f6')).toBeInTheDocument()
  })
})
