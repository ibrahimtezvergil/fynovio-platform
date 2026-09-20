import { fireEvent, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { ACCESS_TOKEN, authenticated, problem, url } from '@/test/authHandlers'
import { authConfig, gate, inputByLabel, typeInto } from '@/test/authForms'
import { renderPage, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import { ChangePasswordForm } from './ChangePasswordForm'

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated())
})

const current = () => inputByLabel(tr('passwordFields.currentPassword'))
const next = () => inputByLabel(tr('passwordFields.newPassword'))
const confirm = () => inputByLabel(tr('passwordFields.confirmPassword'))
const submit = () => screen.getByRole('button', { name: tr('accountSecurity.submit') })

function render(minLength = 12) {
  server.use(authConfig({ minLength }))
  return renderPage(<ChangePasswordForm />)
}

function fill(currentPassword = 'the-old-passphrase', newPassword = 'a-long-new-passphrase', repeat = newPassword) {
  typeInto(current(), currentPassword)
  typeInto(next(), newPassword)
  typeInto(confirm(), repeat)
}

describe('ChangePasswordForm', () => {
  it('has labelled fields with the right autocomplete hints', () => {
    render()
    expect(current()).toHaveAttribute('autocomplete', 'current-password')
    expect(next()).toHaveAttribute('autocomplete', 'new-password')
    expect(confirm()).toHaveAttribute('autocomplete', 'new-password')
  })

  it('validates locally without a request: missing current password, mismatched confirmation', async () => {
    let requests = 0
    server.use(http.post(url(endpoints.auth.changePassword), () => { requests++; return new HttpResponse(null, { status: 204 }) }))
    render()

    typeInto(next(), 'a-long-new-passphrase')
    typeInto(confirm(), 'something-else')
    fireEvent.click(submit())

    expect(await screen.findByText(tr('schema.passwordRequired'))).toBeInTheDocument()
    expect(screen.getByText(tr('schema.confirmMismatch'))).toBeInTheDocument()
    expect(requests).toBe(0)
  })

  it('sends both passwords with the bearer token, confirms, and empties the form', async () => {
    let body: unknown
    let authorization: string | null = null
    server.use(http.post(url(endpoints.auth.changePassword), async ({ request }) => {
      body = await request.json()
      authorization = request.headers.get('authorization')
      return new HttpResponse(null, { status: 204 })
    }))
    render()

    fill()
    fireEvent.click(submit())

    expect(await screen.findByText(tr('accountSecurity.changed'))).toBeInTheDocument()
    expect(body).toEqual({ currentPassword: 'the-old-passphrase', newPassword: 'a-long-new-passphrase' })
    expect(authorization).toBe(`Bearer ${ACCESS_TOKEN}`)
    expect(current()).toHaveValue('')
    expect(next()).toHaveValue('')
    expect(confirm()).toHaveValue('')
    expect(useSessionStore.getState().status).toBe('authenticated') // this session survives; the server ends the others
  })

  it('a wrong current password is an inline error and does NOT sign the user out', async () => {
    server.use(http.post(url(endpoints.auth.changePassword), () => problem(400, 'invalid_current_password')))
    render()

    fill('not-the-password')
    fireEvent.click(submit())

    expect(await screen.findByText(tr('accountSecurity.currentWrong'))).toBeInTheDocument()
    expect(useSessionStore.getState().status).toBe('authenticated')
    expect(screen.queryByText(tr('accountSecurity.changed'))).not.toBeInTheDocument()
  })

  it("a policy violation is worded with the server's limits", async () => {
    server.use(http.post(url(endpoints.auth.changePassword), () => problem(400, 'password_policy_violation', { violations: ['too_short', 'equals_email'] })))
    render(14)
    await screen.findByText(tr('passwordPolicy.hint', { min: 14 }))

    fill('the-old-passphrase', 'short-one')
    fireEvent.click(submit())

    expect(await screen.findByText(new RegExp(tr('passwordPolicy.too_short', { min: 14 })))).toBeInTheDocument()
  })

  it('429 tells the user how long to wait', async () => {
    server.use(http.post(url(endpoints.auth.changePassword), () => problem(429, 'rate_limited', {}, { 'Retry-After': '90' })))
    render()

    fill()
    fireEvent.click(submit())

    expect(await screen.findByRole('alert')).toHaveTextContent(tr('errors.rateLimited', { seconds: 90 }))
  })

  it('shows a pending state and prevents a duplicate submit', async () => {
    let requests = 0
    const held = gate()
    server.use(http.post(url(endpoints.auth.changePassword), async () => { requests++; await held.promise; return new HttpResponse(null, { status: 204 }) }))
    render()

    fill()
    fireEvent.click(submit())
    const busy = await screen.findByRole('button', { name: tr('accountSecurity.submitting') })
    expect(busy).toBeDisabled()
    fireEvent.submit(busy.closest('form')!)

    held.release()
    await waitFor(() => expect(screen.getByText(tr('accountSecurity.changed'))).toBeInTheDocument())
    expect(requests).toBe(1)
  })
})
