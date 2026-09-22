import { fireEvent, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, problem, url } from '@/test/authHandlers'
import { renderPage, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import CompanySettingsPage from './CompanySettingsPage'

const t = (key: string, options?: Record<string, unknown>) => tr(key, options, 'company-settings')
const settings = {
  displayName: 'Acme Logistics', legalName: null, taxNumber: null, taxOffice: null, email: 'hello@acme.test', phone: null,
  address: null, timezone: 'Europe/Istanbul', currencyCode: 'TRY', rowVersion: 3,
} as const

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated({ activeTenant: { tenantId: 1 } }))
})

describe('CompanySettingsPage', () => {
  it('renders values loaded from the real company settings contract', async () => {
    server.use(http.get(url(endpoints.companySettings.profile), () => HttpResponse.json(settings)))
    renderPage(<CompanySettingsPage />)
    expect(await screen.findByDisplayValue('Acme Logistics')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: t('page.title') })).toBeInTheDocument()
    expect(screen.queryByText(/profile settings|profil ayarları/i)).not.toBeInTheDocument()
  })

  it('submits the full replacement with the server version and resets its dirty state', async () => {
    let body: Record<string, unknown> | undefined
    server.use(
      http.get(url(endpoints.companySettings.profile), () => HttpResponse.json(settings)),
      http.put(url(endpoints.companySettings.profile), async ({ request }) => {
        body = await request.json() as Record<string, unknown>
        return HttpResponse.json({ settings: { ...settings, displayName: 'Acme Europe', rowVersion: 4 }, replayed: false })
      }),
    )
    renderPage(<CompanySettingsPage />)
    fireEvent.change(await screen.findByDisplayValue('Acme Logistics'), { target: { value: 'Acme Europe' } })
    fireEvent.click(screen.getByRole('button', { name: t('saveBar.save') }))
    await waitFor(() => expect(body).toMatchObject({ displayName: 'Acme Europe', expectedVersion: 3 }))
    await waitFor(() => expect(screen.getByText(t('saveBar.allSaved'))).toBeInTheDocument())
  })

  it('shows an access explanation instead of a blank page on a denied read', async () => {
    server.use(http.get(url(endpoints.companySettings.profile), () => problem(403, 'authorization_denied')))
    renderPage(<CompanySettingsPage />)
    expect(await screen.findByText(t('problem.forbiddenTitle'))).toBeInTheDocument()
  })
})
