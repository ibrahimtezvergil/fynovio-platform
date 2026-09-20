import { fireEvent, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { server } from '@/mocks/server'
import { problem, url } from '@/test/authHandlers'
import { gate, typeInto } from '@/test/authForms'
import { renderPage, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import { ForgotPasswordForm } from './ForgotPasswordForm'

beforeEach(resetSession)

const email = () => screen.getByLabelText(tr('forgotPassword.emailLabel'))
const submit = () => screen.getByRole('button', { name: tr('forgotPassword.submit') })

describe('ForgotPasswordForm', () => {
  it('validates the address locally, without a request', async () => {
    let requests = 0
    server.use(http.post(url(endpoints.auth.forgotPassword), () => { requests++; return HttpResponse.json({}, { status: 202 }) }))
    renderPage(<ForgotPasswordForm />)

    typeInto(email(), 'not-an-email')
    fireEvent.click(submit())

    expect(await screen.findByText(tr('schema.emailInvalid'))).toBeInTheDocument()
    expect(requests).toBe(0)
  })

  it('sends the address and shows the neutral confirmation (it never says whether an account exists)', async () => {
    let body: unknown
    let headers: Headers | undefined
    server.use(http.post(url(endpoints.auth.forgotPassword), async ({ request }) => {
      body = await request.json()
      headers = request.headers
      return HttpResponse.json({ status: 'accepted' }, { status: 202 })
    }))
    renderPage(<ForgotPasswordForm />)

    typeInto(email(), 'nobody@example.com')
    fireEvent.click(submit())

    expect(await screen.findByText(tr('forgotPassword.sentDescription'))).toBeInTheDocument()
    expect(body).toEqual({ email: 'nobody@example.com' })
    expect(headers?.get('x-requested-with')).toBe('fynovio') // the CSRF header
    expect(screen.queryByLabelText(tr('forgotPassword.emailLabel'))).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: tr('forgotPassword.backToLogin') })).toHaveAttribute('href', '/login')
  })

  it('shows a pending state and prevents a duplicate submit while the request is in flight', async () => {
    let requests = 0
    const held = gate()
    server.use(http.post(url(endpoints.auth.forgotPassword), async () => { requests++; await held.promise; return HttpResponse.json({}, { status: 202 }) }))
    renderPage(<ForgotPasswordForm />)

    typeInto(email(), 'ada@example.com')
    fireEvent.click(submit())
    const busy = await screen.findByRole('button', { name: tr('forgotPassword.submitting') })
    expect(busy).toBeDisabled()
    fireEvent.submit(busy.closest('form')!)

    held.release()
    await screen.findByText(tr('forgotPassword.sentDescription'))
    expect(requests).toBe(1)
  })

  it('429 tells the user how long to wait and does NOT show the confirmation', async () => {
    server.use(http.post(url(endpoints.auth.forgotPassword), () => problem(429, 'rate_limited', {}, { 'Retry-After': '30' })))
    renderPage(<ForgotPasswordForm />)

    typeInto(email(), 'ada@example.com')
    fireEvent.click(submit())

    expect(await screen.findByRole('alert')).toHaveTextContent(tr('errors.rateLimited', { seconds: 30 }))
    expect(screen.queryByText(tr('forgotPassword.sentDescription'))).not.toBeInTheDocument()
    expect(email()).toHaveValue('ada@example.com')
  })

  it('a server failure shows the generic error, not raw text, and no confirmation', async () => {
    server.use(http.post(url(endpoints.auth.forgotPassword), () => new HttpResponse('stack trace', { status: 500 })))
    renderPage(<ForgotPasswordForm />)

    typeInto(email(), 'ada@example.com')
    fireEvent.click(submit())

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(tr('errors.generic'))
    expect(alert).not.toHaveTextContent('stack trace')
    await waitFor(() => expect(submit()).toBeEnabled())
  })
})
