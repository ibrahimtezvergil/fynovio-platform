import { fireEvent, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { server } from '@/mocks/server'
import { problem, url } from '@/test/authHandlers'
import { authConfig, inputByLabel, typeInto } from '@/test/authForms'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import ResetPasswordPage from './ResetPasswordPage'

beforeEach(resetSession)

const routes = () => [
  { path: '/login', element: <p>LOGIN PAGE</p> },
  { path: '/forgot-password', element: <p>FORGOT PAGE</p> },
  { path: '/reset-password', element: <ResetPasswordPage /> },
]

describe('ResetPasswordPage', () => {
  it('reads the token from the link fragment, scrubs it from the address, and submits it', async () => {
    let body: unknown
    server.use(authConfig(), http.post(url(endpoints.auth.resetPassword), async ({ request }) => { body = await request.json(); return new HttpResponse(null, { status: 204 }) }))
    const { router } = renderRoutes(routes(), '/reset-password#token=abc.def')

    expect(router.state.location.hash).toBe('')
    typeInto(inputByLabel(tr('passwordFields.newPassword')), 'a-long-new-passphrase')
    typeInto(inputByLabel(tr('passwordFields.confirmPassword')), 'a-long-new-passphrase')
    fireEvent.click(screen.getByRole('button', { name: tr('resetPassword.submit') }))

    expect(await screen.findByText('LOGIN PAGE')).toBeInTheDocument()
    expect(body).toEqual({ token: 'abc.def', newPassword: 'a-long-new-passphrase' })
  })

  it('a link without a token shows the invalid-link screen and never a form', () => {
    renderRoutes(routes(), '/reset-password')

    expect(screen.getByText(tr('linkInvalid.title'))).toBeInTheDocument()
    expect(screen.queryByLabelText(tr('passwordFields.newPassword'), { selector: 'input' })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: tr('linkInvalid.requestNew') })).toHaveAttribute('href', '/forgot-password')
  })

  it('an expired or used link replaces the form with the invalid-link screen', async () => {
    server.use(authConfig(), http.post(url(endpoints.auth.resetPassword), () => problem(400, 'invalid_or_expired_token')))
    renderRoutes(routes(), '/reset-password#token=old.link')

    typeInto(inputByLabel(tr('passwordFields.newPassword')), 'a-long-new-passphrase')
    typeInto(inputByLabel(tr('passwordFields.confirmPassword')), 'a-long-new-passphrase')
    fireEvent.click(screen.getByRole('button', { name: tr('resetPassword.submit') }))

    expect(await screen.findByText(tr('linkInvalid.title'))).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByRole('button', { name: tr('resetPassword.submit') })).not.toBeInTheDocument())
  })
})
