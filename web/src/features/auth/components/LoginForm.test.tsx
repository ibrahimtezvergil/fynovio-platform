import { fireEvent, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, problem, url } from '@/test/authHandlers'
import { renderPage, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import { LoginForm } from './LoginForm'

beforeEach(resetSession)

const emailField = () => screen.getByLabelText(tr('loginForm.emailLabel'))
const passwordField = () => screen.getByLabelText(tr('loginForm.passwordLabel', {}), { selector: 'input' })
const submit = () => screen.getByRole('button', { name: tr('loginForm.submit') })

function type(field: HTMLElement, value: string) {
  fireEvent.change(field, { target: { value } })
}

function fill(email = 'ada@example.com', password = 'correct horse') {
  type(emailField(), email)
  type(passwordField(), password)
}

describe('LoginForm', () => {
  it('starts empty — no pre-filled credentials', () => {
    renderPage(<LoginForm />)
    expect(emailField()).toHaveValue('')
    expect(passwordField()).toHaveValue('')
  })

  it('has labelled fields with the right autocomplete hints', () => {
    renderPage(<LoginForm />)
    expect(emailField()).toHaveAttribute('autocomplete', 'username')
    expect(emailField()).toHaveAttribute('type', 'email')
    expect(passwordField()).toHaveAttribute('autocomplete', 'current-password')
    expect(passwordField()).toHaveAttribute('type', 'password')
  })

  it('validates locally without a request: bad email, empty password', async () => {
    let requests = 0
    server.use(http.post(url(endpoints.auth.login), () => { requests++; return HttpResponse.json(authenticated()) }))
    renderPage(<LoginForm />)
    type(emailField(), 'not-an-email')
    fireEvent.click(submit())

    expect(await screen.findByText(tr('schema.emailInvalid'))).toBeInTheDocument()
    expect(await screen.findByText(tr('schema.passwordRequired'))).toBeInTheDocument()
    expect(requests).toBe(0)
  })

  it('submits the credentials and signs the session in', async () => {
    let body: unknown
    server.use(http.post(url(endpoints.auth.login), async ({ request }) => { body = await request.json(); return HttpResponse.json(authenticated()) }))
    renderPage(<LoginForm />)

    fill()
    fireEvent.click(submit())

    await waitFor(() => expect(useSessionStore.getState().status).toBe('authenticated'))
    expect(body).toEqual({ email: 'ada@example.com', password: 'correct horse' })
  })

  it('shows a pending state and prevents a duplicate submit while the request is in flight', async () => {
    let requests = 0
    let release!: () => void
    const gate = new Promise<void>((r) => { release = r })
    server.use(http.post(url(endpoints.auth.login), async () => { requests++; await gate; return HttpResponse.json(authenticated()) }))
    renderPage(<LoginForm />)

    fill()
    fireEvent.click(submit())
    const busy = await screen.findByRole('button', { name: tr('loginForm.submitting') })
    expect(busy).toBeDisabled()
    fireEvent.click(busy)
    fireEvent.submit(busy.closest('form')!) // Enter in a field submits the form too

    release()
    await waitFor(() => expect(useSessionStore.getState().status).toBe('authenticated'))
    expect(requests).toBe(1)
  })

  it('401: one generic message that never says which part was wrong; email kept, password cleared', async () => {
    server.use(http.post(url(endpoints.auth.login), () => problem(401, 'invalid_credentials', { detail: 'Server says: no such user' })))
    renderPage(<LoginForm />)

    fill('ada@example.com', 'wrong password')
    fireEvent.click(submit())

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(tr('loginForm.invalidCredentials'))
    expect(alert).not.toHaveTextContent('no such user') // server wording is not echoed
    expect(emailField()).toHaveValue('ada@example.com')
    expect(passwordField()).toHaveValue('')
    expect(screen.getByRole('button', { name: tr('loginForm.submit') })).toBeEnabled()
  })

  it('429: tells the user how long to wait', async () => {
    server.use(http.post(url(endpoints.auth.login), () => problem(429, 'rate_limited', {}, { 'Retry-After': '42' })))
    renderPage(<LoginForm />)

    fill()
    fireEvent.click(submit())

    expect(await screen.findByRole('alert')).toHaveTextContent(tr('loginForm.rateLimited', { seconds: 42 }))
  })

  it('an unexpected failure shows the generic error, not raw server text', async () => {
    server.use(http.post(url(endpoints.auth.login), () => new HttpResponse('boom stack trace', { status: 500 })))
    renderPage(<LoginForm />)

    fill()
    fireEvent.click(submit())

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(tr('loginForm.genericError'))
    expect(alert).not.toHaveTextContent('stack trace')
  })
})
