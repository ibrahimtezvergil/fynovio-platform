import { QueryClient } from '@tanstack/react-query'
import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, url } from '@/test/authHandlers'
import {
  chooseFromCombobox,
  mockAssignable,
  mockDetailApi,
  mockParties,
  noActions,
  problemResponse,
  recordRequests,
  typeInCombobox,
  wirePath,
  wirePrincipal,
} from '@/test/opportunities'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import OpportunityDetailPage from './OpportunityDetailPage'

const t = (key: string, options?: Record<string, unknown>) => tr(key, options, 'opportunities')

const ADA = wirePrincipal('account-9', 'Ada Yılmaz')
const BERK = wirePrincipal('account-10', 'Berk Demir')

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated())
  mockParties()
})

const render = () =>
  renderRoutes(
    [
      { path: '/crm/opportunities', element: <p>LIST</p> },
      { path: '/crm/opportunities/:id', element: <OpportunityDetailPage /> },
    ],
    '/crm/opportunities/12',
    new QueryClient({ defaultOptions: { queries: { retryDelay: 0, staleTime: 0 } } }),
  )

const canReassign = () => ({ actions: () => HttpResponse.json({ ...noActions, canReassign: true }) })
const assigneeLabel = () => t('reassign.assignee.label')

async function openDialog() {
  fireEvent.click(await screen.findByRole('button', { name: t('summary.reassign') }))
  return screen.findByRole('dialog')
}

const submitButton = () => screen.getByRole('button', { name: t('reassign.submit') })

describe('reassign dialog — candidates are the server’s, and only the server’s', () => {
  it('lists exactly what the Assignable Principals query returned, even when it does not match the typed text', async () => {
    const recorder = recordRequests()
    mockDetailApi(12, recorder, canReassign())
    // The server answers "Berk" for the search text "zzz": a client-side filter would hide him.
    mockAssignable(12, (search) => HttpResponse.json(search === 'zzz' ? [BERK] : [ADA, BERK]), recorder)
    render()
    await openDialog()

    await typeInCombobox(assigneeLabel(), 'zzz')
    expect(await screen.findByRole('option', { name: /Berk Demir/ })).toBeInTheDocument()
    expect(screen.queryByRole('option', { name: /Ada Yılmaz/ })).not.toBeInTheDocument()
    const searches = recorder.seen.filter((entry) => entry.path === wirePath(endpoints.opportunities.assignablePrincipals(12)))
    expect(searches.some((entry) => entry.search.includes('search=zzz'))).toBe(true)
  })

  it('says so when the server offers nobody', async () => {
    mockDetailApi(12, recordRequests(), canReassign())
    mockAssignable(12, () => HttpResponse.json([]))
    render()
    await openDialog()

    await typeInCombobox(assigneeLabel(), 'a')
    expect(await screen.findByText(t('reassign.assignee.empty'))).toBeInTheDocument()
  })

  it('fails closed when the server refuses the query: a forbidden message, no way to type a principal, submit is blocked', async () => {
    const recorder = recordRequests()
    mockDetailApi(12, recorder, canReassign())
    mockAssignable(12, () => problemResponse(403, 'forbidden'))
    render()
    const dialog = await openDialog()

    await typeInCombobox(assigneeLabel(), 'a')
    expect(await screen.findByText(t('reassign.assignee.forbidden'))).toBeInTheDocument()
    // The open list makes the rest of the dialog inert; a person would click away first.
    fireEvent.click(within(dialog).getByRole('button', { name: t('reassign.submit'), hidden: true }))
    expect(await within(dialog).findByText(t('reassign.assignee.required'))).toBeInTheDocument()
    expect(recorder.commands()).toHaveLength(0)
  })
})

describe('reassign dialog — submit', () => {
  it('sends the chosen (issuer, subject) with the version the user saw and an idempotency key, then closes and refetches', async () => {
    const recorder = recordRequests()
    mockDetailApi(12, recorder, canReassign())
    mockAssignable(12, () => HttpResponse.json([ADA, BERK]))
    server.use(
      http.post(url(endpoints.opportunities.reassign(12)), async ({ request }) => {
        await recorder.record(request)
        return HttpResponse.json({ opportunityId: 12, replayed: false })
      }),
    )
    render()
    await openDialog()

    await chooseFromCombobox(assigneeLabel(), 'Ada', /Ada Yılmaz/)
    fireEvent.click(submitButton())

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    const [command] = recorder.commands()
    expect(command.path).toBe(wirePath(endpoints.opportunities.reassign(12)))
    expect(command.body).toEqual({ expectedVersion: 5, newPrincipalIssuer: 'https://platform.example', newPrincipalSubject: 'account-9' })
    expect(command.idempotencyKey).toMatch(/\S{8,}/)
    await waitFor(() => expect(recorder.seen.filter((entry) => entry.path === wirePath(endpoints.opportunities.detail(12))).length).toBeGreaterThan(1))
  })

  it('requires a choice: no request is sent without one', async () => {
    const recorder = recordRequests()
    mockDetailApi(12, recorder, canReassign())
    mockAssignable(12, () => HttpResponse.json([ADA]))
    render()
    const dialog = await openDialog()

    fireEvent.click(submitButton())
    expect(await within(dialog).findByText(t('reassign.assignee.required'))).toBeInTheDocument()
    expect(recorder.commands()).toHaveLength(0)
  })

  it('fires one request on a double click', async () => {
    const recorder = recordRequests()
    mockDetailApi(12, recorder, canReassign())
    mockAssignable(12, () => HttpResponse.json([ADA]))
    server.use(
      http.post(url(endpoints.opportunities.reassign(12)), async ({ request }) => {
        await recorder.record(request)
        await new Promise((resolve) => setTimeout(resolve, 150))
        return HttpResponse.json({ opportunityId: 12 })
      }),
    )
    render()
    await openDialog()
    await chooseFromCombobox(assigneeLabel(), 'Ada', /Ada Yılmaz/)

    fireEvent.click(submitButton())
    fireEvent.click(submitButton())

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(recorder.commands()).toHaveLength(1)
  })

  it('on 422 principal_not_assignable: explains, clears the choice, re-asks the server, and refuses to resend the stale person', async () => {
    const recorder = recordRequests()
    mockDetailApi(12, recorder, canReassign())
    let adaStillEligible = true
    mockAssignable(12, () => HttpResponse.json(adaStillEligible ? [ADA, BERK] : [BERK]), recorder)
    server.use(
      http.post(url(endpoints.opportunities.reassign(12)), async ({ request }) => {
        await recorder.record(request)
        adaStillEligible = false
        return problemResponse(422, 'principal_not_assignable')
      }),
    )
    render()
    const dialog = await openDialog()
    await chooseFromCombobox(assigneeLabel(), 'Ada', /Ada Yılmaz/)
    const listReads = () => recorder.seen.filter((entry) => entry.path === wirePath(endpoints.opportunities.assignablePrincipals(12))).length
    const before = listReads()

    fireEvent.click(submitButton())

    expect(await within(dialog).findByText(t('problem.notAssignable.title'))).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: assigneeLabel() })).toHaveValue('')
    await waitFor(() => expect(listReads()).toBeGreaterThan(before))

    // The stale choice is gone: resubmitting without choosing again sends nothing.
    fireEvent.click(submitButton())
    expect(await within(dialog).findByText(t('reassign.assignee.required'))).toBeInTheDocument()
    expect(recorder.commands()).toHaveLength(1)

    await typeInCombobox(assigneeLabel(), 'a')
    expect(await screen.findByRole('option', { name: /Berk Demir/ })).toBeInTheDocument()
    expect(screen.queryByRole('option', { name: /Ada Yılmaz/ })).not.toBeInTheDocument()
  })

  it('on a stale-version conflict: shows the reload notice and keeps the choice', async () => {
    const recorder = recordRequests()
    mockDetailApi(12, recorder, canReassign())
    mockAssignable(12, () => HttpResponse.json([ADA]))
    server.use(http.post(url(endpoints.opportunities.reassign(12)), () => problemResponse(409, 'concurrency_conflict')))
    render()
    const dialog = await openDialog()
    await chooseFromCombobox(assigneeLabel(), 'Ada', /Ada Yılmaz/)

    fireEvent.click(submitButton())

    expect(await within(dialog).findByText(t('problem.concurrency.title'))).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: t('problem.reload') })).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: assigneeLabel() })).toHaveValue('Ada Yılmaz')
  })

  it('on 403 the server refused the change: a forbidden notice, nothing else changes', async () => {
    mockDetailApi(12, recordRequests(), canReassign())
    mockAssignable(12, () => HttpResponse.json([ADA]))
    server.use(http.post(url(endpoints.opportunities.reassign(12)), () => problemResponse(403, 'forbidden')))
    render()
    const dialog = await openDialog()
    await chooseFromCombobox(assigneeLabel(), 'Ada', /Ada Yılmaz/)

    fireEvent.click(submitButton())

    expect(await within(dialog).findByText(t('problem.forbidden.title'))).toBeInTheDocument()
  })
})
