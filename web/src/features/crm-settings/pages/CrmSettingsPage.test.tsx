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

const settings = {
  defaultPipelineDefinitionId: 10, opportunityCreationMode: 'Form', defaultOpportunityTypeId: null,
  requireLostReason: true, requireWonLine: true, defaultAssignmentMode: 'Manual', assignmentPolicy: 'AnyAssignablePrincipal',
  defaultPrincipal: null, defaultTeamId: null, defaultTerritoryId: null, rowVersion: 4,
  pipelines: [{ id: 10, name: 'Sales', rowVersion: 2, isActive: true, isArchived: false, versions: [{ id: 21, versionNumber: 1, status: 'Published', enforceAllowedTransitions: false, publishedAt: '2026-09-24T10:00:00Z', stages: [{ id: 31, name: 'Qualified', sortOrder: 1, isActive: true, isEntry: true, isArchived: false }], allowedTransitions: [] }] }],
  opportunityTypes: [], lostReasons: [], customerNeeds: [],
}
const renderSettings = (initialEntry = '/crm/settings') => renderRoutes([
  { path: '/crm/settings', element: <CrmSettingsPage /> },
  { path: '/crm/settings/:section', element: <CrmSettingsPage /> },
], initialEntry)

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated({ activeTenant: { tenantId: 1 } }))
})

describe('CrmSettingsPage', () => {
  it('loads tenant settings and persists replacement settings through the real API contract', async () => {
    let body: Record<string, unknown> | undefined
    let idempotencyKey: string | null = null
    server.use(
      http.get(url(endpoints.crmSettings.root), () => HttpResponse.json(settings)),
      http.put(url(endpoints.crmSettings.root), async ({ request }) => {
        body = await request.json() as Record<string, unknown>
        idempotencyKey = request.headers.get('Idempotency-Key')
        return HttpResponse.json({ settings: { ...settings, rowVersion: 5 }, replayed: false })
      }),
    )
    renderSettings()
    expect(await screen.findByRole('heading', { name: tr('settings.title', undefined, 'opportunities') })).toBeInTheDocument()
    const closureRules = screen.getAllByRole('checkbox')
    fireEvent.click(closureRules[0])
    fireEvent.click(closureRules[1])
    fireEvent.change(screen.getByLabelText(tr('settings.defaultPipeline', undefined, 'opportunities')), { target: { value: '' } })
    fireEvent.click(screen.getByRole('button', { name: tr('settings.save', undefined, 'opportunities') }))
    await waitFor(() => expect(body).toMatchObject({ expectedVersion: 4, defaultPipelineDefinitionId: null, requireLostReason: false, requireWonLine: false, opportunityCreationMode: 'Form', assignmentPolicy: 'AnyAssignablePrincipal' }))
    expect(idempotencyKey).toBeTruthy()
  })

  it('creates a lost reason using the tenant catalog API and an idempotency key', async () => {
    let body: Record<string, unknown> | undefined
    let requestKey: string | null = null
    server.use(
      http.get(url(endpoints.crmSettings.root), () => HttpResponse.json(settings)),
      http.post(url(endpoints.crmSettings.catalog('lost-reasons')), async ({ request }) => {
        body = await request.json() as Record<string, unknown>
        requestKey = request.headers.get('Idempotency-Key')
        return HttpResponse.json({ item: { id: 52, key: 'renewal', name: 'Renewal', status: 'Active', rowVersion: 1 }, replayed: false })
      }),
    )
    renderSettings('/crm/settings/reasons')
    fireEvent.click((await screen.findAllByRole('button', { name: tr('settings.add', undefined, 'opportunities') }))[0])
    fireEvent.change(screen.getByLabelText(tr('settings.name', undefined, 'opportunities')), { target: { value: 'Renewal' } })
    fireEvent.click(screen.getAllByRole('button', { name: tr('settings.save', undefined, 'opportunities') }).at(-1)!)
    await waitFor(() => expect(body).toMatchObject({ name: 'Renewal', key: 'renewal', expectedVersion: 0, status: 'Active' }))
    expect(requestKey).toBeTruthy()
  })

  it('shows the same section navigation pattern and leaves each catalog on its own route', async () => {
    server.use(http.get(url(endpoints.crmSettings.root), () => HttpResponse.json(settings)))
    const { router } = renderSettings()
    expect(await screen.findByRole('heading', { name: tr('settings.opportunity.title', undefined, 'opportunities') })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /fırsat türleri|opportunity types/i })).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('link', { name: tr('settings.sections.reasons', undefined, 'opportunities') }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/crm/settings/reasons'))
    expect(screen.getByRole('heading', { name: tr('settings.reasonsTitle', undefined, 'opportunities') })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: tr('settings.opportunity.title', undefined, 'opportunities') })).not.toBeInTheDocument()
  })

  it('distinguishes an authorization failure from a temporary load failure', async () => {
    server.use(http.get(url(endpoints.crmSettings.root), () => HttpResponse.json({ title: 'Forbidden' }, { status: 403 })))
    renderSettings()
    expect(await screen.findByText(tr('settings.forbiddenTitle', undefined, 'opportunities'))).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: tr('settings.retry', undefined, 'opportunities') })).not.toBeInTheDocument()
  })

  it('keeps unsaved pipeline edits when settings refetches and blocks publishing an older draft', async () => {
    const withDraft = { ...settings, pipelines: [{ ...settings.pipelines[0], versions: [
      { ...settings.pipelines[0].versions[0], id: 22, status: 'Draft' }, settings.pipelines[0].versions[0],
    ] }] }
    server.use(
      http.get(url(endpoints.crmSettings.root), () => HttpResponse.json(withDraft)),
      http.get(url('/crm/settings/pipelines/10/versions/22/validate'), () => HttpResponse.json({ isValid: true, errors: [], opportunitiesRetainedOnPriorVersions: 0 })),
    )
    const { queryClient } = renderSettings('/crm/settings/pipelines')
    fireEvent.change(await screen.findByLabelText(tr('settings.selectPipeline', undefined, 'opportunities')), { target: { value: '10' } })
    const name = await screen.findByLabelText(tr('settings.pipelineName', undefined, 'opportunities'))
    fireEvent.change(name, { target: { value: 'Sales revised' } })
    fireEvent.click(screen.getAllByRole('checkbox')[1])
    queryClient.invalidateQueries({ queryKey: ['crm-settings'] })
    await waitFor(() => expect(name).toHaveValue('Sales revised'))
    expect(screen.getByRole('button', { name: tr('settings.publish', undefined, 'opportunities') })).toBeDisabled()
    expect(screen.getByText(tr('settings.saveBeforePublish', undefined, 'opportunities'))).toBeInTheDocument()
    fireEvent.change(screen.getByLabelText(tr('settings.selectPipeline', undefined, 'opportunities')), { target: { value: '' } })
    expect(screen.getByRole('alertdialog')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: tr('common.cancel', undefined, 'opportunities') }))
    expect(screen.getByLabelText(tr('settings.selectPipeline', undefined, 'opportunities'))).toHaveValue('10')
    expect(name).toHaveValue('Sales revised')
  })

  describe('Won and Lost system stages', () => {
    const withSystemStages = { ...settings, pipelines: [{ ...settings.pipelines[0], versions: [{ ...settings.pipelines[0].versions[0], stages: [
      { id: 31, name: 'Qualified', sortOrder: 1, isActive: true, isEntry: true, isArchived: false, kind: 0 },
      { id: 32, name: 'Won', sortOrder: 11, isActive: true, isEntry: false, isArchived: false, kind: 1 },
      { id: 33, name: 'Lost', sortOrder: 21, isActive: true, isEntry: false, isArchived: false, kind: 2 },
    ] }] }] }
    const t = (key: string) => tr(key, undefined, 'opportunities')
    const open = async () => {
      server.use(http.get(url(endpoints.crmSettings.root), () => HttpResponse.json(withSystemStages)))
      renderSettings('/crm/settings/pipelines')
      fireEvent.change(await screen.findByLabelText(t('settings.selectPipeline')), { target: { value: '10' } })
    }

    it('shows them as pinned system rows whose only control is the label', async () => {
      await open()

      const won = await screen.findByLabelText(t('settings.systemStage.won'))
      expect(won).toHaveValue('Won')
      expect(screen.getByLabelText(t('settings.systemStage.lost'))).toHaveValue('Lost')
      // One ordinary stage: its entry + active checkboxes and the enforce-transitions checkbox — nothing for the system rows.
      expect(screen.getAllByRole('checkbox')).toHaveLength(3)
      expect(screen.getAllByText(t('settings.systemStage.badge'))).toHaveLength(2)
    })

    it('saves the renamed labels as Won/Lost-kind stages behind the ordinary ones, new stages inserted above them', async () => {
      let posted: unknown
      server.use(http.post(url(endpoints.crmSettings.pipelineDrafts), async ({ request }) => {
        posted = await request.json()
        return HttpResponse.json({ pipelineDefinitionId: 10, versionId: 23, versionNumber: 2, rowVersion: 3, replayed: false }, { status: 201 })
      }))
      await open()

      fireEvent.change(await screen.findByLabelText(t('settings.systemStage.won')), { target: { value: 'Kazanıldı' } })
      fireEvent.change(screen.getByLabelText(t('settings.systemStage.lost')), { target: { value: 'Kaybedildi' } })
      fireEvent.click(screen.getByRole('button', { name: t('settings.addStage') }))
      fireEvent.change(screen.getByLabelText(`${t('settings.stage')} 2`), { target: { value: 'Teklif' } })
      fireEvent.click(screen.getByRole('button', { name: t('settings.saveDraft') }))

      await waitFor(() => expect(posted).toBeDefined())
      expect((posted as { stages: unknown[] }).stages).toEqual([
        { name: 'Qualified', sortOrder: 1, isEntry: true, isActive: true, isArchived: false, kind: 0 },
        { name: 'Teklif', sortOrder: 2, isEntry: false, isActive: true, isArchived: false, kind: 0 },
        { name: 'Kazanıldı', sortOrder: 3, isEntry: false, isActive: true, isArchived: false, kind: 1 },
        { name: 'Kaybedildi', sortOrder: 4, isEntry: false, isActive: true, isArchived: false, kind: 2 },
      ])
    })

    it('removes an ordinary stage from the draft, and keeps the last one', async () => {
      await open()
      fireEvent.click(await screen.findByRole('button', { name: t('settings.addStage') }))
      fireEvent.change(screen.getByLabelText(`${t('settings.stage')} 2`), { target: { value: 'Extra' } })

      fireEvent.click(screen.getAllByRole('button', { name: t('settings.removeStage') })[1])

      await waitFor(() => expect(screen.queryByLabelText(`${t('settings.stage')} 2`)).not.toBeInTheDocument())
      expect(screen.getByRole('button', { name: t('settings.removeStage') })).toBeDisabled()
    })

    it('tells the person what to fix when the server rejects the pipeline, not that CRM settings failed', async () => {
      server.use(http.post(url(endpoints.crmSettings.pipelineDrafts), () => HttpResponse.json({ status: 400, type: 'validation_error', title: 'Stage names must be unique' }, { status: 400 })))
      await open()

      fireEvent.click(await screen.findByRole('button', { name: t('settings.saveDraft') }))

      expect(await screen.findByText(t('settings.pipelineInvalid'))).toBeInTheDocument()
    })
  })

  it('explains why the default pipeline cannot be archived', async () => {
    server.use(http.get(url(endpoints.crmSettings.root), () => HttpResponse.json(settings)))
    renderSettings('/crm/settings/pipelines')
    fireEvent.change(await screen.findByLabelText(tr('settings.selectPipeline', undefined, 'opportunities')), { target: { value: '10' } })
    expect(screen.getAllByRole('button', { name: tr('settings.archive', undefined, 'opportunities') }).at(-1)).toBeDisabled()
    expect(screen.getByText(tr('settings.defaultPipelineLifecycleBlock', undefined, 'opportunities'))).toBeInTheDocument()
  })

  it('archives an eligible pipeline through its lifecycle API', async () => {
    let body: Record<string, unknown> | undefined
    server.use(
      http.get(url(endpoints.crmSettings.root), () => HttpResponse.json({ ...settings, defaultPipelineDefinitionId: null })),
      http.put(url(endpoints.crmSettings.pipelineLifecycle(10)), async ({ request }) => {
        body = await request.json() as Record<string, unknown>
        return HttpResponse.json({ pipelineDefinitionId: 10, rowVersion: 3, isActive: false, isArchived: true, replayed: false })
      }),
    )
    renderSettings('/crm/settings/pipelines')
    const selector = await screen.findByLabelText(tr('settings.selectPipeline', undefined, 'opportunities'))
    fireEvent.change(selector, { target: { value: '10' } })
    fireEvent.click((await screen.findAllByRole('button', { name: tr('settings.archive', undefined, 'opportunities') })).at(-1)!)
    fireEvent.click(screen.getAllByRole('button', { name: tr('settings.archive', undefined, 'opportunities') }).at(-1)!)
    await waitFor(() => expect(body).toMatchObject({ expectedRowVersion: 2, isActive: false, archive: true }))
    await waitFor(() => expect(selector).toHaveValue(''))
  })

  it('restores archived pipelines as inactive through the lifecycle API', async () => {
    let body: Record<string, unknown> | undefined
    const archivedSettings = { ...settings, defaultPipelineDefinitionId: null, pipelines: [{ ...settings.pipelines[0], isActive: false, isArchived: true }] }
    server.use(
      http.get(url(endpoints.crmSettings.root), () => HttpResponse.json(archivedSettings)),
      http.put(url(endpoints.crmSettings.pipelineLifecycle(10)), async ({ request }) => {
        body = await request.json() as Record<string, unknown>
        return HttpResponse.json({ pipelineDefinitionId: 10, rowVersion: 3, isActive: false, isArchived: false, replayed: false })
      }),
    )
    renderSettings('/crm/settings/pipelines')
    const selector = await screen.findByLabelText(tr('settings.selectPipeline', undefined, 'opportunities'))
    fireEvent.change(selector, { target: { value: '10' } })
    fireEvent.click(await screen.findByRole('button', { name: tr('settings.restore', undefined, 'opportunities') }))
    await waitFor(() => expect(body).toMatchObject({ expectedRowVersion: 2, isActive: false, archive: false, restore: true }))
    expect(await screen.findByText(tr('settings.restoredMessage', undefined, 'opportunities'))).toBeInTheDocument()
  })
})
