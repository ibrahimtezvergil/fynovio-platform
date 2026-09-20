import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { queryClient } from '@/api/queryClient'
import { useSessionStore } from '@/lib/auth'
import { selectTenant } from '@/lib/auth/sessionClient'
import { server } from '@/mocks/server'
import { authenticated, url } from '@/test/authHandlers'
import { resetSession } from '@/test/session'
import { wireOpportunity } from '@/test/opportunities'
import { opportunityKeys, referenceKeys, useOpportunityList } from './api'

beforeEach(() => {
  resetSession()
  queryClient.clear()
  useSessionStore.getState().applyAuthResult(authenticated({ activeTenant: { tenantId: 1 } }))
})

describe('Opportunity query keys — tenant isolation via key root', () => {
  it('every key variant is rooted in the active tenant id', () => {
    const tenant1 = opportunityKeys.detail(1, 5)
    const tenant2 = opportunityKeys.detail(2, 5)
    expect(tenant1).not.toEqual(tenant2)
    expect(tenant1[1]).toBe(1)
    expect(tenant2[1]).toBe(2)

    expect(opportunityKeys.list(1, { page: 0 })[1]).toBe(1)
    expect(opportunityKeys.list(2, { page: 0 })[1]).toBe(2)

    expect(opportunityKeys.actions(1, 5)[1]).toBe(1)
    expect(opportunityKeys.actions(2, 5)[1]).toBe(2)

    expect(opportunityKeys.stages(1, 3)[1]).toBe(1)
    expect(opportunityKeys.stages(2, 3)[1]).toBe(2)
  })

  it('the assignee and party reference lookups are tenant-rooted too — a switch can never show another tenant’s people or customers', () => {
    expect(opportunityKeys.assignable(1, 5, 'a')[1]).toBe(1)
    expect(opportunityKeys.assignable(2, 5, 'a')[1]).toBe(2)
    expect(opportunityKeys.assignable(1, 5, 'a')).not.toEqual(opportunityKeys.assignable(2, 5, 'a'))

    expect(referenceKeys.parties(1, 'acme')[1]).toBe(1)
    expect(referenceKeys.parties(2, 'acme')[1]).toBe(2)
    expect(referenceKeys.parties(1, 'acme')).not.toEqual(referenceKeys.parties(2, 'acme'))
    expect(referenceKeys.partyNames(1, [7])).not.toEqual(referenceKeys.partyNames(2, [7]))
  })

  it('renders tenant-1 data while authenticated in tenant 1; after switch data is GONE, tenant-2 data SHOWN, cache cleared', async () => {
    const tenant1Opportunity = wireOpportunity({ id: 101, estimatedAmount: 100 })
    const tenant2Opportunity = wireOpportunity({ id: 202, estimatedAmount: 200 })

    server.use(
      http.get(url(endpoints.opportunities.list), ({ request }) => {
        const token = request.headers.get('Authorization')
        if (token === 'Bearer access-token-1') {
          return HttpResponse.json([tenant1Opportunity])
        }
        if (token === 'Bearer tenant-2-token') {
          return HttpResponse.json([tenant2Opportunity])
        }
        return HttpResponse.json([])
      }),
      http.post(url(endpoints.auth.selectTenant), () =>
        HttpResponse.json({ accessToken: 'tenant-2-token', expiresIn: 600, activeTenant: { tenantId: 2 } }),
      ),
    )

    const TestComponent = () => {
      const { data, isLoading } = useOpportunityList({ page: 0 })
      if (isLoading) return <div>Loading...</div>
      if (!data) return <div>No data</div>
      return (
        <div>
          {data.items.map((opp) => (
            <div key={opp.id} data-testid={`opportunity-${opp.id}`}>
              {opp.id}
            </div>
          ))}
        </div>
      )
    }

    render(
      <QueryClientProvider client={queryClient}>
        <TestComponent />
      </QueryClientProvider>,
    )

    expect(await screen.findByTestId('opportunity-101')).toBeInTheDocument()
    expect(screen.queryByTestId('opportunity-202')).not.toBeInTheDocument()

    await selectTenant(2)

    // Verify tenant 1 data is gone and tenant 2 data is shown
    expect(screen.queryByTestId('opportunity-101')).not.toBeInTheDocument()
    expect(await screen.findByTestId('opportunity-202')).toBeInTheDocument()

    // Verify tenant 1's cache entries were cleared by selectTenant
    expect(queryClient.getQueryCache().findAll({ queryKey: ['opportunities', 1] })).toHaveLength(0)
  })

  it('in-flight requests from tenant 1 never land after switch to tenant 2', async () => {
    const tenant1Opportunity = wireOpportunity({ id: 101 })
    const tenant2Opportunity = wireOpportunity({ id: 202 })
    let tenant1RequestCount = 0

    server.use(
      http.get(url(endpoints.opportunities.list), async ({ request }) => {
        const token = request.headers.get('Authorization')
        if (token === 'Bearer access-token-1') {
          tenant1RequestCount++
          await new Promise((resolve) => setTimeout(resolve, 80))
          return HttpResponse.json([tenant1Opportunity])
        }
        if (token === 'Bearer tenant-2-token') {
          return HttpResponse.json([tenant2Opportunity])
        }
        return HttpResponse.json([])
      }),
      http.post(url(endpoints.auth.selectTenant), () =>
        HttpResponse.json({ accessToken: 'tenant-2-token', expiresIn: 600, activeTenant: { tenantId: 2 } }),
      ),
    )

    const TestComponent = () => {
      const { data, isLoading } = useOpportunityList({ page: 0 })
      if (isLoading) return <div>Loading...</div>
      if (!data) return <div>No data</div>
      return (
        <div>
          {data.items.map((opp) => (
            <div key={opp.id} data-testid={`opportunity-${opp.id}`}>
              {opp.id}
            </div>
          ))}
        </div>
      )
    }

    render(
      <QueryClientProvider client={queryClient}>
        <TestComponent />
      </QueryClientProvider>,
    )

    expect(await screen.findByText('Loading...')).toBeInTheDocument()
    expect(tenant1RequestCount).toBeGreaterThan(0)

    await selectTenant(2)

    expect(await screen.findByTestId('opportunity-202')).toBeInTheDocument()

    // Wait longer than the slow request delay to ensure it would have completed
    await new Promise((resolve) => setTimeout(resolve, 200))

    // Tenant 1 data never appears, even though the slow request should have landed by now
    expect(screen.queryByTestId('opportunity-101')).not.toBeInTheDocument()

    // Verify tenant 1's cache is cleared
    expect(queryClient.getQueryCache().findAll({ queryKey: ['opportunities', 1] })).toHaveLength(0)
  })

  it('key root alone isolates data: tenant-1 seeded data never renders in tenant-2 session', async () => {
    const tenant1Opportunity = wireOpportunity({ id: 101, estimatedAmount: 100 })
    const tenant2Opportunity = wireOpportunity({ id: 202, estimatedAmount: 200 })

    server.use(
      http.get(url(endpoints.opportunities.list), ({ request }) => {
        const token = request.headers.get('Authorization')
        if (token === 'Bearer access-token-1') {
          return HttpResponse.json([tenant1Opportunity])
        }
        if (token === 'Bearer tenant-2-token') {
          return HttpResponse.json([tenant2Opportunity])
        }
        return HttpResponse.json([])
      }),
    )

    const TestComponent = () => {
      const { data, isLoading } = useOpportunityList({ page: 0 })
      if (isLoading) return <div>Loading...</div>
      if (!data) return <div>No data</div>
      return (
        <div>
          {data.items.map((opp) => (
            <div key={opp.id} data-testid={`opportunity-${opp.id}`}>
              {opp.id}
            </div>
          ))}
        </div>
      )
    }

    // Seed tenant 1's cache with data
    queryClient.setQueryData(opportunityKeys.list(1, { page: 0 }), {
      items: [tenant1Opportunity],
      hasNext: false,
    })

    // Switch session to tenant 2 WITHOUT calling selectTenant (so cache is NOT cleared)
    useSessionStore.getState().applyTenantSelection({ accessToken: 'tenant-2-token', expiresIn: 600, activeTenant: { tenantId: 2 } })

    // Tenant 1 data is still in the cache, but the hook now looks for a different key
    render(
      <QueryClientProvider client={queryClient}>
        <TestComponent />
      </QueryClientProvider>,
    )

    // Tenant 1 data never renders (key root isolation works)
    expect(screen.queryByTestId('opportunity-101')).not.toBeInTheDocument()

    // Tenant 2 data loads and renders
    expect(await screen.findByTestId('opportunity-202')).toBeInTheDocument()
  })

  it('calling selectTenant invokes both cancelQueries and clear on the query client', async () => {
    server.use(
      http.post(url(endpoints.auth.selectTenant), () =>
        HttpResponse.json({ accessToken: 'tenant-2-token', expiresIn: 600, activeTenant: { tenantId: 2 } }),
      ),
    )

    const cancelSpy = vi.spyOn(queryClient, 'cancelQueries')
    const clearSpy = vi.spyOn(queryClient, 'clear')

    useSessionStore.getState().applyAuthResult(authenticated({ activeTenant: { tenantId: 1 } }))

    await selectTenant(2)

    expect(cancelSpy).toHaveBeenCalled()
    expect(clearSpy).toHaveBeenCalled()

    cancelSpy.mockRestore()
    clearSpy.mockRestore()
  })
})
