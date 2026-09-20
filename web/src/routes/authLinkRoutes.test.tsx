import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { I18nextProvider } from 'react-i18next'
import { RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { i18n } from '@/lib/i18n'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, url } from '@/test/authHandlers'
import { authConfig } from '@/test/authForms'
import { tr } from '@/test/render'
import { resetSession } from '@/test/session'
import { router } from '@/routes'

beforeEach(resetSession)

function open(path: string) {
  return router.navigate(path).then(() =>
    render(
      <I18nextProvider i18n={i18n}>
        <QueryClientProvider client={new QueryClient()}>
          <RouterProvider router={router} />
        </QueryClientProvider>
      </I18nextProvider>,
    ),
  )
}

describe('the real route tree', () => {
  it('an e-mailed reset link opens for a signed-in browser too (the link is the credential; it must not be bounced away with its token)', async () => {
    useSessionStore.getState().applyAuthResult(authenticated())
    server.use(authConfig())

    await open('/reset-password#token=abc.def')

    expect(await screen.findByText(tr('resetPassword.title'))).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/reset-password')
  })

  it('an e-mailed invitation link opens for a signed-in browser too', async () => {
    useSessionStore.getState().applyAuthResult(authenticated())
    server.use(authConfig(), http.post(url(endpoints.auth.validateInvitation), () => HttpResponse.json({ valid: true, email: 'a***@example.com', tenantId: 1, accountHasCredential: false })))

    await open('/accept-invite#token=inv.token')

    expect(await screen.findByLabelText(tr('acceptInvite.nameLabel'))).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/accept-invite')
  })

  it('the account-security page is protected: signed out, it goes to /login carrying a safe returnUrl', async () => {
    useSessionStore.getState().endSession('unauthenticated')

    await open('/account/security')

    await screen.findByText(tr('loginPage.title'))
    expect(router.state.location.pathname).toBe('/login')
    expect(router.state.location.search).toContain(encodeURIComponent('/account/security'))
  })
})
