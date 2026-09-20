import { fireEvent, screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { server } from '@/mocks/server'
import { problem, url } from '@/test/authHandlers'
import { authConfig, inputByLabel, typeInto } from '@/test/authForms'
import { renderPage, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import RegisterPage from './RegisterPage'

beforeEach(resetSession)

const submit = () => screen.getByRole('button', { name: tr('register.submit') })

describe('RegisterPage', () => {
  it('while sign-up is off it shows the invite-only screen, never a form that could only fail', async () => {
    server.use(authConfig({ selfRegistrationEnabled: false }))
    renderPage(<RegisterPage />)

    expect(await screen.findByText(tr('register.inviteOnlyTitle'))).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: tr('register.submit') })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: tr('register.backToLogin') })).toHaveAttribute('href', '/login')
  })

  it('when the server cannot say, it fails closed to the invite-only screen', async () => {
    server.use(http.get(url(endpoints.auth.config), () => new HttpResponse('boom', { status: 500 })))
    renderPage(<RegisterPage />)

    expect(await screen.findByText(tr('register.inviteOnlyTitle'))).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: tr('register.submit') })).not.toBeInTheDocument()
  })

  it('when the server enables sign-up it shows the form, validating locally', async () => {
    let requests = 0
    server.use(authConfig({ selfRegistrationEnabled: true }), http.post(url(endpoints.auth.register), () => { requests++; return HttpResponse.json({}, { status: 202 }) }))
    renderPage(<RegisterPage />)

    await screen.findByLabelText(tr('register.nameLabel'))
    typeInto(screen.getByLabelText(tr('register.emailLabel')), 'not-an-email')
    fireEvent.click(submit())

    expect(await screen.findByText(tr('schema.emailInvalid'))).toBeInTheDocument()
    expect(screen.getByText(tr('schema.nameRequired'))).toBeInTheDocument()
    expect(requests).toBe(0)
  })

  it('sends the registration and shows the neutral confirmation', async () => {
    let body: unknown
    server.use(authConfig({ selfRegistrationEnabled: true }), http.post(url(endpoints.auth.register), async ({ request }) => { body = await request.json(); return HttpResponse.json({ status: 'accepted' }, { status: 202 }) }))
    renderPage(<RegisterPage />)

    typeInto(await screen.findByLabelText(tr('register.nameLabel')), ' Ada Lovelace ')
    typeInto(screen.getByLabelText(tr('register.emailLabel')), 'ada@example.com')
    typeInto(inputByLabel(tr('passwordFields.newPassword')), 'a-long-new-passphrase')
    typeInto(inputByLabel(tr('passwordFields.confirmPassword')), 'a-long-new-passphrase')
    fireEvent.click(submit())

    expect(await screen.findByText(tr('register.sentDescription'))).toBeInTheDocument()
    expect(body).toEqual({ email: 'ada@example.com', displayName: 'Ada Lovelace', password: 'a-long-new-passphrase' })
  })

  it('a policy violation is shown on the password field and the passwords are cleared', async () => {
    server.use(authConfig({ selfRegistrationEnabled: true, minLength: 14 }), http.post(url(endpoints.auth.register), () => problem(400, 'password_policy_violation', { violations: ['too_short'] })))
    renderPage(<RegisterPage />)

    typeInto(await screen.findByLabelText(tr('register.nameLabel')), 'Ada')
    await screen.findByText(tr('passwordPolicy.hint', { min: 14 }))
    typeInto(screen.getByLabelText(tr('register.emailLabel')), 'ada@example.com')
    typeInto(inputByLabel(tr('passwordFields.newPassword')), 'short-one')
    typeInto(inputByLabel(tr('passwordFields.confirmPassword')), 'short-one')
    fireEvent.click(submit())

    expect(await screen.findByText(tr('passwordPolicy.too_short', { min: 14 }))).toBeInTheDocument()
    expect(inputByLabel(tr('passwordFields.newPassword'))).toHaveValue('')
  })
})
