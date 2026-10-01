import { QueryClient } from '@tanstack/react-query'
import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, url } from '@/test/authHandlers'
import { wireOpportunity } from '@/test/opportunities'
import { renderRoutes, tr } from '@/test/render'
import { resetSession } from '@/test/session'
import OpportunitiesPage from './OpportunitiesPage'

const t = (key: string, options?: Record<string, unknown>) => tr(key, options, 'opportunities')

const field = (id: number, fieldName: string, label: string, status = 'Active') => ({
  id, fieldName, label, fieldType: 'text', isRequired: false, status, sortOrder: id, rowVersion: 1, config: {},
})
const definitions = [field(1, 'region', 'Region'), field(2, 'tier', 'Tier'), field(3, 'retired', 'Retired', 'Deprecated')]

const view = (id: number, key: string, name: string, columns: { kind: string; key: string }[], status = 'Active') =>
  ({ id, key, name, kind: 'table', columns, status, sortOrder: id, rowVersion: 1 })

let views: ReturnType<typeof view>[]

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated())
  views = [
    view(1, 'regional', 'Regional review', [{ kind: 'field', key: 'region' }, { kind: 'builtin', key: 'party' }, { kind: 'builtin', key: 'id' }]),
    view(2, 'stale', 'Uses a retired field', [{ kind: 'builtin', key: 'id' }, { kind: 'field', key: 'retired' }, { kind: 'builtin', key: 'status' }]),
    view(3, 'old', 'Old view', [{ kind: 'builtin', key: 'id' }], 'Deprecated'),
  ]
  server.use(
    http.get(url(endpoints.references.parties), () => HttpResponse.json([])),
    http.get(url(endpoints.pipelines.stages(3)), () => HttpResponse.json([])),
    http.get(url(endpoints.crmSettings.customFields), () => HttpResponse.json(definitions)),
    http.get(url(endpoints.crmSettings.views), () => HttpResponse.json(views)),
    http.get(url(endpoints.opportunities.list), () => HttpResponse.json([wireOpportunity({ id: 1, customFields: { region: 'North', tier: 'Gold' } })])),
  )
})

const render = (initialEntry = '/crm/opportunities') =>
  renderRoutes([{ path: '/crm/opportunities', element: <OpportunitiesPage /> }], initialEntry, new QueryClient({ defaultOptions: { queries: { retryDelay: 0, staleTime: 0 } } }))

/** The table's labelled column headers, in order (the selection column has none; the row actions column is the table's own). */
const headers = async () => {
  const table = await screen.findByRole('table')
  await within(table).findAllByTestId('opportunity-row')
  return within(table).getAllByRole('columnheader').map((header) => header.textContent?.trim()).filter(Boolean)
}

describe('opportunities list — shared table views', () => {
  it('shows every column until a view is chosen, and offers only the active views', async () => {
    render()

    const picker = await screen.findByRole('combobox', { name: t('views.picker.label') })
    expect(within(picker).getAllByRole('option').map((option) => option.textContent)).toEqual([t('views.picker.all'), 'Regional review', 'Uses a retired field'])
    const all = await headers()
    expect(all).toEqual(expect.arrayContaining([t('list.columns.id'), t('list.columns.status'), t('list.columns.amount'), 'Region', 'Tier']))
  })

  it('shows exactly the view\'s columns in its order when one is chosen, and puts the choice in the URL', async () => {
    const { router } = render()

    fireEvent.change(await screen.findByRole('combobox', { name: t('views.picker.label') }), { target: { value: 'regional' } })

    await waitFor(() => expect(router.state.location.search).toContain('tv=regional'))
    await waitFor(async () => expect(await headers()).toEqual(['Region', t('list.columns.party'), t('list.columns.id'), t('list.columns.actions')]))
  })

  it('opens the same columns from a shared link', async () => {
    render('/crm/opportunities?tv=regional')

    expect(await headers()).toEqual(['Region', t('list.columns.party'), t('list.columns.id'), t('list.columns.actions')])
    expect(screen.getByRole('combobox', { name: t('views.picker.label') })).toHaveValue('regional')
  })

  it('keeps working with the columns that remain when a field the view uses has been retired', async () => {
    render('/crm/opportunities?tv=stale')

    expect(await headers()).toEqual([t('list.columns.id'), t('list.columns.status'), t('list.columns.actions')])
  })

  it('treats a link to a retired or unknown view as the default instead of an error', async () => {
    render('/crm/opportunities?tv=old')

    const all = await headers()
    expect(all).toEqual(expect.arrayContaining([t('list.columns.amount'), 'Region', 'Tier']))
    expect(screen.getByRole('combobox', { name: t('views.picker.label') })).toHaveValue('')
  })

  it('offers no picker when the tenant has no active view', async () => {
    views = [view(3, 'old', 'Old view', [{ kind: 'builtin', key: 'id' }], 'Deprecated')]
    render()

    await headers()
    expect(screen.queryByRole('combobox', { name: t('views.picker.label') })).not.toBeInTheDocument()
  })
})
