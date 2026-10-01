import { QueryClient } from '@tanstack/react-query'
import { fireEvent, screen, waitFor } from '@testing-library/react'
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

const definition = (fieldName: string, label: string, fieldType: string, options?: { key: string; label: string; isDeprecated?: boolean }[]) => ({
  id: fieldName.length, fieldName, label, fieldType, isRequired: false, status: 'Active', sortOrder: 1, rowVersion: 1,
  config: options ? { options: options.map((option) => ({ isDeprecated: false, ...option })) } : {},
})

const definitions = [
  definition('region', 'Region', 'select', [{ key: 'north', label: 'North' }, { key: 'old', label: 'Old', isDeprecated: true }]),
  definition('vip', 'VIP', 'boolean'),
  definition('note', 'Note', 'text'),
]

/** Every `cf` value of each list request, in order. */
let requests: string[][]

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated())
  requests = []
  server.use(
    http.get(url(endpoints.references.parties), () => HttpResponse.json([])),
    http.get(url(endpoints.pipelines.stages(3)), () => HttpResponse.json([])),
    http.get(url(endpoints.crmSettings.customFields), () => HttpResponse.json(definitions)),
    http.get(url(endpoints.opportunities.list), ({ request }) => {
      requests.push(new URL(request.url).searchParams.getAll('cf'))
      return HttpResponse.json([wireOpportunity({ id: 1 })])
    }),
  )
})

const render = (initialEntry = '/crm/opportunities') =>
  renderRoutes([{ path: '/crm/opportunities', element: <OpportunitiesPage /> }], initialEntry, new QueryClient({ defaultOptions: { queries: { retryDelay: 0, staleTime: 0 } } }))

describe('opportunities list — custom field filters', () => {
  it('offers a pill per select and boolean field and sends the chosen option to the server', async () => {
    render()

    const region = await screen.findByRole('button', { name: `Region: ${t('list.filters.all')}` })
    expect(screen.getByRole('button', { name: `VIP: ${t('list.filters.all')}` })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: new RegExp(`^Note:`) })).not.toBeInTheDocument()

    fireEvent.click(region)
    expect(screen.queryByRole('menuitemradio', { name: 'Old' })).not.toBeInTheDocument()
    fireEvent.click(await screen.findByRole('menuitemradio', { name: 'North' }))

    await waitFor(() => expect(requests.at(-1)).toEqual(['region:north']))
    expect(await screen.findByRole('button', { name: 'Region: North' })).toBeInTheDocument()
  })

  it('reads the filters from the URL and clears them with the other filters', async () => {
    render('/crm/opportunities?cf.region=north&cf.vip=true')

    expect(await screen.findByRole('button', { name: `VIP: ${t('customFields.yes')}` })).toBeInTheDocument()
    expect(requests[0]).toEqual(['region:north', 'vip:true'])

    fireEvent.click(screen.getByRole('button', { name: t('list.filters.clear') }))
    await waitFor(() => expect(requests.at(-1)).toEqual([]))
  })
})
