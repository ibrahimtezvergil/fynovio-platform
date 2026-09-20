import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { queryClient as sharedQueryClient } from '@/api/queryClient'
import { useSessionStore } from '@/lib/auth'
import { selectTenant } from '@/lib/auth/sessionClient'
import { server } from '@/mocks/server'
import { authenticated, url } from '@/test/authHandlers'
import { resetSession } from '@/test/session'
import { wireOpportunity } from '@/test/opportunities'
import { opportunityKeys, useOpportunityList } from './api'

beforeEach(() => {
  resetSession()
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

  it('renders tenant-1 data while authenticated in tenant 1; after switch data is GONE, tenant-2 data SHOWN', async () => {
    const tenant1Opportunity = wireOpportunity({ id: 101, estimatedAmount: 100 })
    const tenant2Opportunity = wireOpportunity({ id: 202, estimatedAmount: 200 })

    server.use(
      http.get(url(endpoints.opportunities.list), ({ request }) => {
        const token = request.headers.get('Authorization')
        // Mock: tenant 1 token returns tenant 1 data, tenant 2 token returns tenant 2 data
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

    // Render with a dedicated client to isolate the test
    const testClient = new QueryClient({ defaultOptions: { queries: { retryDelay: 0, staleTime: 0 } } })

    // Component that uses the hook
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

    const { rerender } = render(
      <QueryClientProvider client={testClient}>
        <TestComponent />
      </QueryClientProvider>,
    )

    // Verify tenant 1 data is shown
    expect(await screen.findByTestId('opportunity-101')).toBeInTheDocument()
    expect(screen.queryByTestId('opportunity-202')).not.toBeInTheDocument()

    // Switch to tenant 2
    useSessionStore.getState().applyTenantSelection({ accessToken: 'tenant-2-token', expiresIn: 600, activeTenant: { tenantId: 2 } })
    await selectTenant(2)

    // Re-render component to pick up the new tenant
    rerender(
      <QueryClientProvider client={testClient}>
        <TestComponent />
      </QueryClientProvider>,
    )

    // Verify tenant 1 data is GONE and tenant 2 data is shown
    await waitFor(() => {
      expect(screen.queryByTestId('opportunity-101')).not.toBeInTheDocument()
    })
    expect(await screen.findByTestId('opportunity-202')).toBeInTheDocument()
  })

  it('in-flight requests from tenant 1 never land after switch to tenant 2', async () => {
    const tenant1Opportunity = wireOpportunity({ id: 101 })
    const tenant2Opportunity = wireOpportunity({ id: 202 })

    server.use(
      http.get(url(endpoints.opportunities.list), async ({ request }) => {
        const token = request.headers.get('Authorization')
        // Tenant 1: slow response (80ms)
        if (token === 'Bearer access-token-1') {
          await new Promise((resolve) => setTimeout(resolve, 80))
          return HttpResponse.json([tenant1Opportunity])
        }
        // Tenant 2: immediate response
        if (token === 'Bearer tenant-2-token') {
          return HttpResponse.json([tenant2Opportunity])
        }
        return HttpResponse.json([])
      }),
      http.post(url(endpoints.auth.selectTenant), () =>
        HttpResponse.json({ accessToken: 'tenant-2-token', expiresIn: 600, activeTenant: { tenantId: 2 } }),
      ),
    )

    const testClient = new QueryClient({ defaultOptions: { queries: { retryDelay: 0, staleTime: 0 } } })

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

    const { rerender } = render(
      <QueryClientProvider client={testClient}>
        <TestComponent />
      </QueryClientProvider>,
    )

    // Start loading tenant 1 data (slow)
    expect(await screen.findByText('Loading...')).toBeInTheDocument()

    // Immediately switch tenant before the first request completes
    useSessionStore.getState().applyTenantSelection({ accessToken: 'tenant-2-token', expiresIn: 600, activeTenant: { tenantId: 2 } })
    await selectTenant(2)

    rerender(
      <QueryClientProvider client={testClient}>
        <TestComponent />
      </QueryClientProvider>,
    )

    // Tenant 2 data loads and tenant 1 data never appears
    expect(await screen.findByTestId('opportunity-202')).toBeInTheDocument()
    await waitFor(() => {
      expect(screen.queryByTestId('opportunity-101')).not.toBeInTheDocument()
    }, { timeout: 200 })
  })

  it('calling selectTenant invokes both cancelQueries and clear on the query client', async () => {
    server.use(
      http.post(url(endpoints.auth.selectTenant), () =>
        HttpResponse.json({ accessToken: 'tenant-2-token', expiresIn: 600, activeTenant: { tenantId: 2 } }),
      ),
    )

    const cancelSpy = vi.spyOn(sharedQueryClient, 'cancelQueries')
    const clearSpy = vi.spyOn(sharedQueryClient, 'clear')

    useSessionStore.getState().applyAuthResult(authenticated({ activeTenant: { tenantId: 1 } }))

    await selectTenant(2)

    expect(cancelSpy).toHaveBeenCalled()
    expect(clearSpy).toHaveBeenCalled()

    cancelSpy.mockRestore()
    clearSpy.mockRestore()
  })
})
