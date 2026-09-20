import { expect } from '@playwright/test'
import { newApi } from './api.ts'
import { VIEWER } from './env.ts'

const CSRF = { 'X-Requested-With': 'fynovio' }

export async function viewerToken(): Promise<string> {
  const api = await newApi()
  const response = await api.post('/api/auth/login', {
    data: { email: VIEWER, password: process.env.E2E_SEED_PASSWORD ?? 'E2E-Seed-Passw0rd-1234' },
    headers: CSRF,
  })
  expect(response.status()).toBe(200)
  const token = (await response.json()).accessToken as string
  await api.dispose()
  return token
}

export interface CreateOpportunityResponse {
  opportunityId: number
  replayed: boolean
}

export async function createViaApi(
  token: string,
  params: { partyId: number; currency: string; estimatedAmount: number },
): Promise<number> {
  const api = await newApi()
  const response = await api.post('/api/opportunities', {
    data: params,
    headers: {
      ...CSRF,
      Authorization: `Bearer ${token}`,
      'Idempotency-Key': crypto.randomUUID(),
    },
  })
  expect(response.status()).toBe(201)
  const body = (await response.json()) as CreateOpportunityResponse
  await api.dispose()
  return body.opportunityId
}

export async function addLineViaApi(
  token: string,
  opportunityId: number,
  params: { expectedVersion: number; productId: number; quantity: number; unitPrice: number; isOptional: boolean },
): Promise<void> {
  const api = await newApi()
  const response = await api.post(`/api/opportunities/${opportunityId}/lines`, {
    data: { ...params, sortOrder: 0 },
    headers: {
      ...CSRF,
      Authorization: `Bearer ${token}`,
      'Idempotency-Key': crypto.randomUUID(),
    },
  })
  expect(response.status()).toBe(200)
  await api.dispose()
}

export interface Opportunity {
  id: number
  status: number // 0=Draft, 1=Open, 2=Won, 3=Lost
  partyId: number | null
  currency: string | null
  estimatedAmount: number | null
  totalAmount: number | null
  pipelineDefinitionVersionId: number | null
  pipelineStageId: number | null
  lostReason: string | null
  expiryDate: string | null
  openedDate: string | null
  wonDate: string | null
  lostDate: string | null
  rowVersion: number
  lines: Array<{ id: number; quantity: number; unitPrice: number; lineTotal: number | null; isOptional: boolean; isCanceled: boolean }>
}

export async function getViaApi(token: string, opportunityId: number): Promise<Opportunity> {
  const api = await newApi()
  const response = await api.get(`/api/opportunities/${opportunityId}`, {
    headers: { Authorization: `Bearer ${token}` },
  })
  expect(response.ok()).toBe(true)
  const body = (await response.json()) as Opportunity
  await api.dispose()
  return body
}

export interface OpportunitySummary {
  id: number
  status: number
  estimatedAmount: number | null
  currency: string | null
  assignedPrincipalIssuer: string | null
  assignedPrincipalSubject: string | null
  pipelineStageId: number | null
}

export async function listViaApi(token: string, params?: { status?: number; skip?: number; take?: number }): Promise<OpportunitySummary[]> {
  const api = await newApi()
  const response = await api.get('/api/opportunities', {
    params,
    headers: { Authorization: `Bearer ${token}` },
  })
  expect(response.ok()).toBe(true)
  const body = (await response.json()) as OpportunitySummary[]
  await api.dispose()
  return body
}

export async function openViaApi(
  token: string,
  opportunityId: number,
  params: { expectedVersion: number; expiryDate: string },
): Promise<void> {
  const api = await newApi()
  const response = await api.post(`/api/opportunities/${opportunityId}/open`, {
    data: params,
    headers: {
      ...CSRF,
      Authorization: `Bearer ${token}`,
      'Idempotency-Key': crypto.randomUUID(),
    },
  })
  expect(response.status()).toBe(200)
  await api.dispose()
}

export async function winViaApi(token: string, opportunityId: number, expectedVersion: number): Promise<void> {
  const api = await newApi()
  const response = await api.post(`/api/opportunities/${opportunityId}/win`, {
    data: { expectedVersion },
    headers: {
      ...CSRF,
      Authorization: `Bearer ${token}`,
      'Idempotency-Key': crypto.randomUUID(),
    },
  })
  expect(response.status()).toBe(200)
  await api.dispose()
}

export async function loseViaApi(
  token: string,
  opportunityId: number,
  params: { expectedVersion: number; lostReason: string },
): Promise<void> {
  const api = await newApi()
  const response = await api.post(`/api/opportunities/${opportunityId}/lose`, {
    data: params,
    headers: {
      ...CSRF,
      Authorization: `Bearer ${token}`,
      'Idempotency-Key': crypto.randomUUID(),
    },
  })
  expect(response.status()).toBe(200)
  await api.dispose()
}
