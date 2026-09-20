import { fireEvent, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { queryClient } from '@/api/queryClient'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, problem, url } from '@/test/authHandlers'
import { authConfig, gate, inputByLabel, typeInto } from '@/test/authForms'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import { ResetPasswordForm } from './ResetPasswordForm'

beforeEach(resetSession)

const newPassword = () => inputByLabel(tr('passwordFields.newPassword'))
const confirm = () => inputByLabel(tr('passwordFields.confirmPassword'))
const submit = () => screen.getByRole('button', { name: tr('resetPassword.submit') })

function renderForm(onLinkInvalid = () => {}) {
  server.use(authConfig({ minLength: 14 }))
  return renderRoutes(
    [
      { path: '/login', element: <p>LOGIN PAGE</p> },
      { path: '*', element: <ResetPasswordForm token="id.secret" onLinkInvalid={onLinkInvalid} /> },
    ],
    '/reset-password',
  )
}

function fill(password = 'a-long-new-passphrase', repeat = password) {
  typeInto(newPassword(), password)
  typeInto(confirm(), repeat)
}

describe('ResetPasswordForm', () => {
  it('has labelled new-password fields and shows the server-provided minimum as a hint', async () => {
    renderForm()

    expect(newPassword()).toHaveAttribute('autocomplete', 'new-password')
    expect(confirm()).toHaveAttribute('autocomplete', 'new-password')
    expect(newPassword()).toHaveAttribute('type', 'password')
    expect(await screen.findByText(tr('passwordPolicy.hint', { min: 14 }))).toBeInTheDocument()
  })

  it('validates locally without a request: empty password, mismatched confirmation', async () => {
    let requests = 0
    server.use(http.post(url(endpoints.auth.resetPassword), () => { requests++; return new HttpResponse(null, { status: 204 }) }))
    renderForm()

    fireEvent.click(submit())
    expect(await screen.findByText(tr('schema.newPasswordRequired'))).toBeInTheDocument()

    fill('a-long-new-passphrase', 'something-else-entirely')
    fireEvent.click(submit())
    expect(await screen.findByText(tr('schema.confirmMismatch'))).toBeInTheDocument()
    expect(requests).toBe(0)
  })

  it('sends the token and the new password only, then ends the local session and goes to /login with a notice', async () => {
    useSessionStore.getState().applyAuthResult(authenticated())
    const clear = vi.spyOn(queryClient, 'clear')
    let body: unknown
    server.use(http.post(url(endpoints.auth.resetPassword), async ({ request }) => { body = await request.json(); return new HttpResponse(null, { status: 204 }) }))
    const { router } = renderForm()

    fill()
    fireEvent.click(submit())

    expect(await screen.findByText('LOGIN PAGE')).toBeInTheDocument()
    expect(body).toEqual({ token: 'id.secret', newPassword: 'a-long-new-passphrase' }) // no confirmation field on the wire
    expect(router.state.location.pathname).toBe('/login')
    expect((router.state.location.state as { notice: string }).notice).toBe('passwordUpdated')
    // the server revoked every session; the browser must forget its own too
    expect(useSessionStore.getState().status).toBe('unauthenticated')
    expect(useSessionStore.getState().accessToken).toBeNull()
    expect(clear).toHaveBeenCalled()
  })

  it('an invalid or used link hands control back to the page and does not retry', async () => {
    const onLinkInvalid = vi.fn()
    server.use(http.post(url(endpoints.auth.resetPassword), () => problem(400, 'invalid_or_expired_token')))
    renderForm(onLinkInvalid)

    fill()
    fireEvent.click(submit())

    await waitFor(() => expect(onLinkInvalid).toHaveBeenCalledTimes(1))
  })

  it("a policy violation is worded with the server's limits and the passwords are cleared", async () => {
    server.use(http.post(url(endpoints.auth.resetPassword), () => problem(400, 'password_policy_violation', { violations: ['too_short'] })))
    renderForm()
    await screen.findByText(tr('passwordPolicy.hint', { min: 14 })) // the server's policy has arrived

    fill('short-one')
    fireEvent.click(submit())

    expect(await screen.findByText(tr('passwordPolicy.too_short', { min: 14 }))).toBeInTheDocument()
    expect(newPassword()).toHaveValue('')
    expect(confirm()).toHaveValue('')
  })

  it('429 tells the user how long to wait; a server failure shows only the generic error', async () => {
    server.use(http.post(url(endpoints.auth.resetPassword), () => problem(429, 'rate_limited', {}, { 'Retry-After': '20' })))
    renderForm()
    fill()
    fireEvent.click(submit())
    expect(await screen.findByRole('alert')).toHaveTextContent(tr('errors.rateLimited', { seconds: 20 }))

    server.use(http.post(url(endpoints.auth.resetPassword), () => new HttpResponse('boom stack trace', { status: 500 })))
    fill()
    fireEvent.click(submit())
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(tr('errors.generic')))
    expect(screen.getByRole('alert')).not.toHaveTextContent('stack trace')
  })

  it('shows a pending state and prevents a duplicate submit', async () => {
    let requests = 0
    const held = gate()
    server.use(http.post(url(endpoints.auth.resetPassword), async () => { requests++; await held.promise; return new HttpResponse(null, { status: 204 }) }))
    renderForm()

    fill()
    fireEvent.click(submit())
    const busy = await screen.findByRole('button', { name: tr('resetPassword.submitting') })
    expect(busy).toBeDisabled()
    fireEvent.submit(busy.closest('form')!)

    held.release()
    await screen.findByText('LOGIN PAGE')
    expect(requests).toBe(1)
  })
})
