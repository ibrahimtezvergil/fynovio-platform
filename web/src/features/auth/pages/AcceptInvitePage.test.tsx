import { fireEvent, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, problem, selectionRequired, url } from '@/test/authHandlers'
import { authConfig, gate, inputByLabel, typeInto } from '@/test/authForms'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import AcceptInvitePage from './AcceptInvitePage'

beforeEach(resetSession)

const preview = (overrides: Record<string, unknown> = {}) =>
  http.post(url(endpoints.auth.validateInvitation), () =>
    HttpResponse.json({ valid: true, email: 'a***@example.com', tenantId: 3, accountHasCredential: false, expiresAt: '2030-01-01T00:00:00Z', displayName: 'Ada L.', ...overrides }),
  )

const routes = () => [
  { path: '/login', element: <p>LOGIN PAGE</p> },
  { path: '/accept-invite', element: <AcceptInvitePage /> },
]

const submit = () => screen.getByRole('button', { name: tr('acceptInvite.submit') })

describe('AcceptInvitePage', () => {
  it('a link without a token shows the invalid-link screen and makes no request', () => {
    renderRoutes(routes(), '/accept-invite')
    expect(screen.getByText(tr('linkInvalid.title'))).toBeInTheDocument()
  })

  it('previews the invitation (masked address), scrubbing the token from the address bar', async () => {
    let previewBody: unknown
    server.use(authConfig(), http.post(url(endpoints.auth.validateInvitation), async ({ request }) => {
      previewBody = await request.json()
      return HttpResponse.json({ valid: true, email: 'a***@example.com', tenantId: 3, accountHasCredential: false, displayName: 'Ada L.' })
    }))
    const { router } = renderRoutes(routes(), '/accept-invite#token=inv.token')

    expect(await screen.findByText(tr('acceptInvite.forAddress', { email: 'a***@example.com' }))).toBeInTheDocument()
    expect(previewBody).toEqual({ token: 'inv.token' })
    expect(router.state.location.hash).toBe('')
  })

  it('shows a checking state while the preview loads', async () => {
    const held = gate()
    server.use(authConfig(), http.post(url(endpoints.auth.validateInvitation), async () => { await held.promise; return HttpResponse.json({ valid: true, email: 'a***@example.com', tenantId: 3, accountHasCredential: false }) }))
    renderRoutes(routes(), '/accept-invite#token=inv.token')

    expect(await screen.findByText(tr('acceptInvite.checking'))).toBeInTheDocument()
    held.release()
    expect(await screen.findByLabelText(tr('acceptInvite.nameLabel'))).toBeInTheDocument()
  })

  it('an invalid, expired or used token shows the invalid-link screen', async () => {
    server.use(authConfig(), http.post(url(endpoints.auth.validateInvitation), () => problem(400, 'invalid_or_expired_token')))
    renderRoutes(routes(), '/accept-invite#token=old.token')

    expect(await screen.findByText(tr('linkInvalid.title'))).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: tr('acceptInvite.submit') })).not.toBeInTheDocument()
  })

  it('a server hiccup is not "invalid link": it offers a retry', async () => {
    let calls = 0
    server.use(authConfig(), http.post(url(endpoints.auth.validateInvitation), () => {
      calls++
      return calls === 1 ? new HttpResponse('boom', { status: 503 }) : HttpResponse.json({ valid: true, email: 'a***@example.com', tenantId: 3, accountHasCredential: false })
    }))
    renderRoutes(routes(), '/accept-invite#token=inv.token')

    expect(await screen.findByText(tr('acceptInvite.unreachable'))).toBeInTheDocument()
    expect(screen.queryByText(tr('linkInvalid.title'))).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: tr('errors.retry') }))
    expect(await screen.findByLabelText(tr('acceptInvite.nameLabel'))).toBeInTheDocument()
  })

  describe('a new address', () => {
    it('asks for a name (pre-filled) and a password entered twice, validating locally', async () => {
      let requests = 0
      server.use(authConfig(), preview(), http.post(url(endpoints.auth.acceptInvitation), () => { requests++; return HttpResponse.json(authenticated()) }))
      renderRoutes(routes(), '/accept-invite#token=inv.token')

      const name = await screen.findByLabelText(tr('acceptInvite.nameLabel'))
      expect(name).toHaveValue('Ada L.')
      expect(inputByLabel(tr('passwordFields.newPassword'))).toHaveAttribute('autocomplete', 'new-password')

      typeInto(name, '   ')
      typeInto(inputByLabel(tr('passwordFields.newPassword')), 'a-long-new-passphrase')
      typeInto(inputByLabel(tr('passwordFields.confirmPassword')), 'different')
      fireEvent.click(submit())

      expect(await screen.findByText(tr('schema.nameRequired'))).toBeInTheDocument()
      expect(screen.getByText(tr('schema.confirmMismatch'))).toBeInTheDocument()
      expect(requests).toBe(0)
    })

    it('accepts: sends token, password and name, signs the session in and hands over to the login route', async () => {
      let body: unknown
      server.use(authConfig(), preview(), http.post(url(endpoints.auth.acceptInvitation), async ({ request }) => { body = await request.json(); return HttpResponse.json(authenticated()) }))
      const { router } = renderRoutes(routes(), '/accept-invite#token=inv.token')

      typeInto(await screen.findByLabelText(tr('acceptInvite.nameLabel')), '  Ada Lovelace ')
      typeInto(inputByLabel(tr('passwordFields.newPassword')), 'a-long-new-passphrase')
      typeInto(inputByLabel(tr('passwordFields.confirmPassword')), 'a-long-new-passphrase')
      fireEvent.click(submit())

      expect(await screen.findByText('LOGIN PAGE')).toBeInTheDocument() // PublicOnlyRoute then routes the fresh session
      expect(body).toEqual({ token: 'inv.token', password: 'a-long-new-passphrase', displayName: 'Ada Lovelace' })
      expect(router.state.location.pathname).toBe('/login')
      expect(useSessionStore.getState().status).toBe('authenticated')
      expect(useSessionStore.getState().accessToken).not.toBeNull()
      for (const store of [localStorage, sessionStorage]) {
        for (let i = 0; i < store.length; i++) expect(store.getItem(store.key(i)!)).not.toContain('access-token-1')
      }
    })

    it('a membership across several tenants ends in the tenant-selection state', async () => {
      server.use(authConfig(), preview(), http.post(url(endpoints.auth.acceptInvitation), () => HttpResponse.json(selectionRequired())))
      renderRoutes(routes(), '/accept-invite#token=inv.token')

      typeInto(await screen.findByLabelText(tr('acceptInvite.nameLabel')), 'Ada')
      typeInto(inputByLabel(tr('passwordFields.newPassword')), 'a-long-new-passphrase')
      typeInto(inputByLabel(tr('passwordFields.confirmPassword')), 'a-long-new-passphrase')
      fireEvent.click(submit())

      await waitFor(() => expect(useSessionStore.getState().status).toBe('tenant_unresolved'))
    })

    it('a policy violation is shown on the password field and the passwords are cleared; the link stays usable', async () => {
      server.use(authConfig({ minLength: 14 }), preview(), http.post(url(endpoints.auth.acceptInvitation), () => problem(400, 'password_policy_violation', { violations: ['too_short'] })))
      renderRoutes(routes(), '/accept-invite#token=inv.token')

      typeInto(await screen.findByLabelText(tr('acceptInvite.nameLabel')), 'Ada')
      await screen.findByText(tr('passwordPolicy.hint', { min: 14 })) // the server's policy has arrived
      typeInto(inputByLabel(tr('passwordFields.newPassword')), 'short-one')
      typeInto(inputByLabel(tr('passwordFields.confirmPassword')), 'short-one')
      fireEvent.click(submit())

      expect(await screen.findByText(tr('passwordPolicy.too_short', { min: 14 }))).toBeInTheDocument()
      expect(inputByLabel(tr('passwordFields.newPassword'))).toHaveValue('')
      expect(screen.getByLabelText(tr('acceptInvite.nameLabel'))).toHaveValue('Ada') // non-sensitive input is kept
      expect(submit()).toBeEnabled()
    })

    it('a token that was used in the meantime replaces the form with the invalid-link screen', async () => {
      server.use(authConfig(), preview(), http.post(url(endpoints.auth.acceptInvitation), () => problem(400, 'invalid_or_expired_token')))
      renderRoutes(routes(), '/accept-invite#token=inv.token')

      typeInto(await screen.findByLabelText(tr('acceptInvite.nameLabel')), 'Ada')
      typeInto(inputByLabel(tr('passwordFields.newPassword')), 'a-long-new-passphrase')
      typeInto(inputByLabel(tr('passwordFields.confirmPassword')), 'a-long-new-passphrase')
      fireEvent.click(submit())

      expect(await screen.findByText(tr('linkInvalid.title'))).toBeInTheDocument()
    })

    it('prevents a duplicate submit while the request is in flight', async () => {
      let requests = 0
      const held = gate()
      server.use(authConfig(), preview(), http.post(url(endpoints.auth.acceptInvitation), async () => { requests++; await held.promise; return HttpResponse.json(authenticated()) }))
      renderRoutes(routes(), '/accept-invite#token=inv.token')

      typeInto(await screen.findByLabelText(tr('acceptInvite.nameLabel')), 'Ada')
      typeInto(inputByLabel(tr('passwordFields.newPassword')), 'a-long-new-passphrase')
      typeInto(inputByLabel(tr('passwordFields.confirmPassword')), 'a-long-new-passphrase')
      fireEvent.click(submit())
      const busy = await screen.findByRole('button', { name: tr('acceptInvite.submitting') })
      expect(busy).toBeDisabled()
      fireEvent.submit(busy.closest('form')!)

      held.release()
      await screen.findByText('LOGIN PAGE')
      expect(requests).toBe(1)
    })
  })

  describe('an address that already has an account', () => {
    it('only asks for the existing password — no name, no confirmation', async () => {
      server.use(authConfig(), preview({ accountHasCredential: true }))
      renderRoutes(routes(), '/accept-invite#token=inv.token')

      expect(await screen.findByLabelText(tr('acceptInvite.existingPasswordLabel'), { selector: 'input' })).toHaveAttribute('autocomplete', 'current-password')
      expect(screen.queryByLabelText(tr('acceptInvite.nameLabel'))).not.toBeInTheDocument()
      expect(screen.queryByLabelText(tr('passwordFields.confirmPassword'), { selector: 'input' })).not.toBeInTheDocument()
    })

    it('a wrong password is an inline error on the field, the password is cleared and the form stays usable', async () => {
      let body: unknown
      server.use(authConfig(), preview({ accountHasCredential: true }), http.post(url(endpoints.auth.acceptInvitation), async ({ request }) => { body = await request.json(); return problem(401, 'invalid_credentials') }))
      renderRoutes(routes(), '/accept-invite#token=inv.token')

      typeInto(await screen.findByLabelText(tr('acceptInvite.existingPasswordLabel'), { selector: 'input' }), 'wrong')
      fireEvent.click(submit())

      expect(await screen.findByText(tr('acceptInvite.wrongPassword'))).toBeInTheDocument()
      expect(body).toEqual({ token: 'inv.token', password: 'wrong' }) // no displayName for an existing account
      expect(screen.getByLabelText(tr('acceptInvite.existingPasswordLabel'), { selector: 'input' })).toHaveValue('')
      expect(useSessionStore.getState().status).toBe('unknown') // nothing was signed in
    })
  })
})
