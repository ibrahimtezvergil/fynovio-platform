import { QueryClient } from '@tanstack/react-query'
import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { endpoints } from '@/api/endpoints'
import { useSessionStore } from '@/lib/auth'
import { server } from '@/mocks/server'
import { authenticated, url } from '@/test/authHandlers'
import { renderPage } from '@/test/render'
import { resetSession } from '@/test/session'
import type { ApiError } from '@/types'
import { useReassignOpportunity } from './api'

beforeEach(() => {
  resetSession()
  useSessionStore.getState().applyAuthResult(authenticated())
})

describe('useReassignOpportunity — contract and error handling', () => {
  it('sends exactly the contract body: expectedVersion, newPrincipalIssuer, newPrincipalSubject (no id, idempotencyKey, or tenant)', async () => {
    let recordedRequest: { body: unknown; path: string; idempotencyKey: string | null } | null = null

    server.use(
      http.post(url(endpoints.opportunities.reassign(12)), async ({ request }) => {
        const body = await request.json()
        recordedRequest = {
          body,
          path: new URL(request.url).pathname,
          idempotencyKey: request.headers.get('Idempotency-Key'),
        }
        return HttpResponse.json({ opportunityId: 12 })
      }),
    )

    const TestHarness = () => {
      const mutation = useReassignOpportunity()

      return (
        <button
          onClick={() =>
            mutation.mutateAsync({
              id: 12,
              expectedVersion: 5,
              idempotencyKey: 'k-1',
              newPrincipalIssuer: 'https://platform.example',
              newPrincipalSubject: 'account-9',
            })
          }
        >
          Reassign
        </button>
      )
    }

    renderPage(<TestHarness />, '/', new QueryClient({ defaultOptions: { queries: { retryDelay: 0, staleTime: 0 } } }))
    const button = screen.getByRole('button', { name: 'Reassign' })
    button.click()

    await waitFor(() => {
      expect(recordedRequest).not.toBeNull()
    })

    expect(recordedRequest!.body).toEqual({
      expectedVersion: 5,
      newPrincipalIssuer: 'https://platform.example',
      newPrincipalSubject: 'account-9',
    })
    expect(recordedRequest!.idempotencyKey).toBe('k-1')
    expect(recordedRequest!.path).toBe('/api/opportunities/12/reassign')
  })

  it('a 409 concurrency_conflict response rejects with ApiError having code and status', async () => {
    server.use(
      http.post(url(endpoints.opportunities.reassign(12)), () =>
        HttpResponse.json(
          { type: 'concurrency_conflict', title: 'concurrency_conflict', status: 409 },
          { status: 409 },
        ),
      ),
    )

    let capturedError: ApiError | null = null

    const TestHarness = () => {
      const mutation = useReassignOpportunity()

      return (
        <button
          onClick={async () => {
            try {
              await mutation.mutateAsync({
                id: 12,
                expectedVersion: 5,
                idempotencyKey: 'k-1',
                newPrincipalIssuer: 'https://platform.example',
                newPrincipalSubject: 'account-9',
              })
            } catch (error) {
              capturedError = error as ApiError
            }
          }}
        >
          Reassign
        </button>
      )
    }

    renderPage(<TestHarness />, '/', new QueryClient({ defaultOptions: { queries: { retryDelay: 0, staleTime: 0 } } }))
    const button = screen.getByRole('button', { name: 'Reassign' })
    button.click()

    await waitFor(() => {
      expect(capturedError).not.toBeNull()
    })

    expect(capturedError!.code).toBe('concurrency_conflict')
    expect(capturedError!.status).toBe(409)
  })

  it('no component under components/ or pages/ references useReassignOpportunity', async () => {
    // Use import.meta.glob to find all component and page files
    const componentFiles = import.meta.glob('/src/features/opportunities/components/**/*.tsx', { query: '?raw', import: 'default', eager: true })
    const pageFiles = import.meta.glob('/src/features/opportunities/pages/**/*.tsx', { query: '?raw', import: 'default', eager: true })

    const allFiles = { ...componentFiles, ...pageFiles }

    // Filter out test files since we're checking component source, not test source
    const nonTestFiles = Object.entries(allFiles)
      .filter(([path]) => !path.includes('.test.tsx'))
      .map(([, content]) => content as string)

    nonTestFiles.forEach((content) => {
      expect(content).not.toContain('useReassignOpportunity')
    })
  })
})
