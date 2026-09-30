import { fireEvent, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, url } from '@/test/authHandlers'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import CrmSettingsPage from './CrmSettingsPage'

const t = (key: string, options?: Record<string, unknown>) => tr(key, options, 'opportunities')

const settings = {
  defaultPipelineDefinitionId: null, opportunityCreationMode: 'Form', defaultOpportunityTypeId: null,
  requireLostReason: false, requireWonLine: true, defaultAssignmentMode: 'Manual', assignmentPolicy: 'AnyAssignablePrincipal',
  defaultPrincipal: null, defaultTeamId: null, defaultTerritoryId: null, rowVersion: 1,
  pipelines: [], opportunityTypes: [], lostReasons: [], customerNeeds: [],
}
const region = { id: 7, fieldName: 'region', label: 'Region', fieldType: 'select', isRequired: false, status: 'Active', sortOrder: 10, rowVersion: 3,
  config: { options: [{ key: 'north', label: 'North', isDeprecated: false }] } }

const render = () => renderRoutes([{ path: '/crm/settings/:section', element: <CrmSettingsPage /> }], '/crm/settings/fields')

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated({ activeTenant: { tenantId: 1 } }))
  server.use(http.get(url(endpoints.crmSettings.root), () => HttpResponse.json(settings)))
})

describe('CRM settings — opportunity fields', () => {
  it('creates a field with a key derived from its Turkish label and only the config its type uses', async () => {
    let body: Record<string, unknown> | undefined
    server.use(
      http.get(url(endpoints.crmSettings.customFields), () => HttpResponse.json([])),
      http.post(url(endpoints.crmSettings.customFields), async ({ request }) => {
        body = await request.json() as Record<string, unknown>
        return HttpResponse.json({ definitionId: 8, rowVersion: 1, replayed: false }, { status: 201 })
      }),
    )
    render()

    fireEvent.click(await screen.findByRole('button', { name: t('customFields.settings.add') }))
    fireEvent.change(screen.getByRole('textbox', { name: t('customFields.settings.label') }), { target: { value: 'Bütçe Kodu' } })
    fireEvent.change(screen.getByRole('combobox', { name: t('customFields.settings.type') }), { target: { value: 'number' } })
    fireEvent.change(screen.getByRole('textbox', { name: t('customFields.settings.max') }), { target: { value: '50' } })
    fireEvent.click(screen.getByRole('button', { name: t('customFields.save') }))

    await waitFor(() => expect(body).toBeDefined())
    expect(body).toEqual({ key: 'butce_kodu', label: 'Bütçe Kodu', type: 'number', isRequired: false, sortOrder: 10, config: { min: null, max: 50 } })
  })

  it('shows the data impact before deprecating and sends the definition row version', async () => {
    let transition: { path: string; body: unknown } | undefined
    server.use(
      http.get(url(endpoints.crmSettings.customFields), () => HttpResponse.json([region])),
      http.get(url(endpoints.crmSettings.customFieldImpact(7)), () => HttpResponse.json({ definitionId: 7, fieldName: 'region', opportunitiesWithValue: 4 })),
      http.post(url(endpoints.crmSettings.customFieldTransition(7, 'deprecate')), async ({ request }) => {
        transition = { path: new URL(request.url).pathname, body: await request.json() }
        return HttpResponse.json({ definitionId: 7, rowVersion: 4, replayed: false })
      }),
    )
    render()

    fireEvent.click(await screen.findByRole('button', { name: t('customFields.settings.more', { name: 'Region' }) }))
    fireEvent.click(await screen.findByRole('menuitem', { name: t('customFields.settings.deprecate') }))

    expect(await screen.findByText(t('customFields.settings.impact', { count: 4 }))).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: t('customFields.settings.deprecate') }))

    await waitFor(() => expect(transition).toBeDefined())
    expect(transition?.body).toEqual({ expectedRowVersion: 3 })
  })

  it('never offers to remove a saved option, only to deprecate it', async () => {
    server.use(http.get(url(endpoints.crmSettings.customFields), () => HttpResponse.json([region])))
    render()

    fireEvent.click(await screen.findByRole('button', { name: t('customFields.settings.edit') }))
    expect(screen.getByRole('button', { name: t('customFields.settings.optionDeprecate') })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: t('customFields.settings.optionRemove') })).not.toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: t('customFields.settings.type') })).toBeDisabled()
  })
})
