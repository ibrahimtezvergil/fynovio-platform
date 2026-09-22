import { QueryClientProvider } from '@tanstack/react-query'
import { act, renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { queryClient } from '@/api/queryClient'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, url } from '@/test/authHandlers'
import { resetSession } from '@/test/session'
import { companySettingsKeys, useCompanySettings, useUpdateCompanySettings } from './api'

const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
const settings = {
  displayName: 'Acme Logistics', legalName: null, taxNumber: null, taxOffice: null, email: 'hello@acme.test', phone: null,
  address: null, timezone: 'Europe/Istanbul', currencyCode: 'TRY', rowVersion: 3,
} as const

beforeEach(() => {
  resetSession()
  queryClient.clear()
  useSessionStore.getState().applyAuthResult(authenticated({ activeTenant: { tenantId: 1 } }))
})

describe('company settings API boundary', () => {
  it('reads the active tenant profile without ever putting a tenant id on the request', async () => {
    let seen: { authorization: string | null; query: string } | undefined
    server.use(http.get(url(endpoints.companySettings.profile), ({ request }) => {
      seen = { authorization: request.headers.get('authorization'), query: new URL(request.url).search }
      return HttpResponse.json(settings)
    }))
    const { result } = renderHook(() => useCompanySettings(), { wrapper })
    await waitFor(() => expect(result.current.data).toMatchObject(settings))
    expect(seen).toEqual({ authorization: 'Bearer access-token-1', query: '' })
  })

  it('keeps tenant caches separate', () => {
    expect(companySettingsKeys.profile(1)).not.toEqual(companySettingsKeys.profile(2))
    expect(companySettingsKeys.profile(1)).toEqual(['company-settings', 1, 'profile'])
  })

  it('sends a full replacement with expected version and an idempotency key', async () => {
    let seen: { body: Record<string, unknown>; key: string | null } | undefined
    server.use(http.put(url(endpoints.companySettings.profile), async ({ request }) => {
      seen = { body: await request.json() as Record<string, unknown>, key: request.headers.get('idempotency-key') }
      return HttpResponse.json({ settings: { ...settings, displayName: 'New Acme', rowVersion: 4 }, replayed: false })
    }))
    const { result } = renderHook(() => useUpdateCompanySettings(), { wrapper })
    const { rowVersion: _rowVersion, ...values } = settings
    await act(async () => {
      await result.current.mutateAsync({
        values: { ...values, displayName: 'New Acme' },
        expectedVersion: 3,
        idempotencyKey: 'company-update-1',
      })
    })
    expect(seen).toEqual({
      body: { ...values, displayName: 'New Acme', expectedVersion: 3 },
      key: 'company-update-1',
    })
  })
})
