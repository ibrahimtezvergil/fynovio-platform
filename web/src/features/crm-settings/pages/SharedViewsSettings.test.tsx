import { fireEvent, screen, waitFor, within } from '@testing-library/react'
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
const field = (id: number, fieldName: string, label: string, status = 'Active') => ({ id, fieldName, label, fieldType: 'text', isRequired: false, status, sortOrder: id, rowVersion: 1, config: {} })
const regional = { id: 4, key: 'regional', name: 'Regional review', kind: 'table', status: 'Active', sortOrder: 10, rowVersion: 3,
  columns: [{ kind: 'builtin', key: 'id' }, { kind: 'field', key: 'region' }, { kind: 'field', key: 'retired' }] }

const render = () => renderRoutes([{ path: '/crm/settings/:section', element: <CrmSettingsPage /> }], '/crm/settings/views')

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated({ activeTenant: { tenantId: 1 } }))
  server.use(
    http.get(url(endpoints.crmSettings.root), () => HttpResponse.json(settings)),
    http.get(url(endpoints.crmSettings.customFields), () => HttpResponse.json([field(1, 'region', 'Region'), field(2, 'tier', 'Tier'), field(3, 'retired', 'Retired', 'Deprecated')])),
  )
})

describe('CRM settings — list views', () => {
  it('creates a view with a key derived from its name and the columns in the order the person arranged them', async () => {
    let body: Record<string, unknown> | undefined
    server.use(
      http.get(url(endpoints.crmSettings.views), () => HttpResponse.json([])),
      http.post(url(endpoints.crmSettings.views), async ({ request }) => {
        body = await request.json() as Record<string, unknown>
        return HttpResponse.json({ definitionId: 9, rowVersion: 1, replayed: false, changeSetId: 12 }, { status: 201 })
      }),
    )
    render()

    fireEvent.click(await screen.findByRole('button', { name: t('views.settings.add') }))
    fireEvent.change(screen.getByRole('textbox', { name: t('views.settings.name') }), { target: { value: 'Bölge Özeti' } })
    expect(screen.getByRole('button', { name: t('customFields.save') })).toBeDisabled()   // a view needs a column

    const add = () => screen.getByRole('combobox', { name: t('views.settings.addColumn') })
    fireEvent.change(add(), { target: { value: 'builtin:status' } })
    fireEvent.change(add(), { target: { value: 'field:region' } })
    fireEvent.change(add(), { target: { value: 'builtin:id' } })
    fireEvent.click(screen.getByRole('button', { name: t('views.settings.moveUp', { name: t('views.builtIn.id') }) }))   // id above Region
    fireEvent.click(screen.getByRole('button', { name: t('customFields.save') }))

    await waitFor(() => expect(body).toBeDefined())
    expect(body).toEqual({
      key: 'bolge_ozeti', name: 'Bölge Özeti', sortOrder: 10,
      columns: [{ kind: 'builtin', key: 'status' }, { kind: 'builtin', key: 'id' }, { kind: 'field', key: 'region' }],
    })
  })

  it('offers each column once, only active fields, and stops at the column limit', async () => {
    server.use(http.get(url(endpoints.crmSettings.views), () => HttpResponse.json([])))
    render()

    fireEvent.click(await screen.findByRole('button', { name: t('views.settings.add') }))
    const add = screen.getByRole('combobox', { name: t('views.settings.addColumn') })
    const optionValues = () => within(add).getAllByRole('option').map((option) => (option as HTMLOptionElement).value).filter(Boolean)
    expect(optionValues()).toContain('field:region')
    expect(optionValues()).not.toContain('field:retired')
    expect(optionValues()).not.toContain('builtin:serviceDuration')

    fireEvent.change(add, { target: { value: 'field:region' } })
    expect(optionValues()).not.toContain('field:region')
  })

  it('edits a saved view: keeps its key, shows a retired field as retired, and sends the row version', async () => {
    let sent: { path: string; body: Record<string, unknown> } | undefined
    server.use(
      http.get(url(endpoints.crmSettings.views), () => HttpResponse.json([regional])),
      http.put(url(endpoints.crmSettings.view(4)), async ({ request }) => {
        sent = { path: new URL(request.url).pathname, body: await request.json() as Record<string, unknown> }
        return HttpResponse.json({ definitionId: 4, rowVersion: 4, replayed: false })
      }),
    )
    render()

    fireEvent.click(await screen.findByRole('button', { name: t('views.settings.edit') }))
    expect(screen.getByRole('textbox', { name: t('views.settings.key') })).toBeDisabled()
    expect(screen.getByText(t('views.settings.fieldRetired'))).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: t('views.settings.removeColumn', { name: 'Retired' }) }))
    fireEvent.click(screen.getByRole('button', { name: t('customFields.save') }))

    await waitFor(() => expect(sent).toBeDefined())
    expect(sent?.body).toEqual({ name: 'Regional review', sortOrder: 10, columns: [{ kind: 'builtin', key: 'id' }, { kind: 'field', key: 'region' }], expectedRowVersion: 3 })
  })

  it('deprecates and reactivates with the view\'s row version', async () => {
    const transitions: { path: string; body: unknown }[] = []
    server.use(
      http.get(url(endpoints.crmSettings.views), () => HttpResponse.json([regional, { ...regional, id: 5, key: 'old', name: 'Old view', status: 'Deprecated', rowVersion: 7 }])),
      http.post(url(endpoints.crmSettings.viewTransition(4, 'deprecate')), async ({ request }) => {
        transitions.push({ path: new URL(request.url).pathname, body: await request.json() })
        return HttpResponse.json({ definitionId: 4, rowVersion: 4, replayed: false })
      }),
      http.post(url(endpoints.crmSettings.viewTransition(5, 'reactivate')), async ({ request }) => {
        transitions.push({ path: new URL(request.url).pathname, body: await request.json() })
        return HttpResponse.json({ definitionId: 5, rowVersion: 8, replayed: false })
      }),
    )
    render()

    fireEvent.click(await screen.findByRole('button', { name: t('views.settings.more', { name: 'Regional review' }) }))
    fireEvent.click(await screen.findByRole('menuitem', { name: t('views.settings.deprecate') }))
    await waitFor(() => expect(transitions).toHaveLength(1))
    fireEvent.click(screen.getByRole('button', { name: t('views.settings.more', { name: 'Old view' }) }))
    fireEvent.click(await screen.findByRole('menuitem', { name: t('views.settings.reactivate') }))
    await waitFor(() => expect(transitions).toHaveLength(2))

    expect(transitions.map((entry) => entry.body)).toEqual([{ expectedRowVersion: 3 }, { expectedRowVersion: 7 }])
  })

  it('words a refusal by its code', async () => {
    server.use(
      http.get(url(endpoints.crmSettings.views), () => HttpResponse.json([])),
      http.post(url(endpoints.crmSettings.views), () => HttpResponse.json({ status: 409, type: 'view_key_conflict', title: 'dup' }, { status: 409 })),
    )
    render()

    fireEvent.click(await screen.findByRole('button', { name: t('views.settings.add') }))
    fireEvent.change(screen.getByRole('textbox', { name: t('views.settings.name') }), { target: { value: 'Again' } })
    fireEvent.change(screen.getByRole('combobox', { name: t('views.settings.addColumn') }), { target: { value: 'builtin:id' } })
    fireEvent.click(screen.getByRole('button', { name: t('customFields.save') }))

    expect(await screen.findByText(t('views.settings.keyConflict'))).toBeInTheDocument()
  })
})
