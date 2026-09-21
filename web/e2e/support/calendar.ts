import { expect } from '@playwright/test'
import calendar from '../../src/locales/tr/calendar.ts'
import { newApi } from './api.ts'

export { calendar }

const CSRF = { 'X-Requested-With': 'fynovio' }

export interface WireEntry {
  id: number
  rowVersion: number
  title: string
  color: string
  allDay: boolean
  startAt: string | null
  endAt: string | null
  startDate: string | null
  endDate: string | null
  link: { ref: { boundedContext: string; entityType: string; id: number }; state: 'accessible' | 'unavailable'; label?: string } | null
}

/** A window that surely contains "now": the API caps a range at 100 days. */
export const currentWindow = () => ({
  from: new Date(Date.now() - 30 * 86_400_000).toISOString(),
  to: new Date(Date.now() + 60 * 86_400_000).toISOString(),
})

export async function listEntries(token: string): Promise<WireEntry[]> {
  const api = await newApi()
  const response = await api.get('/api/calendar/entries', { params: currentWindow(), headers: { Authorization: `Bearer ${token}` } })
  expect(response.status()).toBe(200)
  const items = ((await response.json()) as { items: WireEntry[] }).items
  await api.dispose()
  return items
}

/** A timed entry starting in one hour, created straight through the API. */
export async function createEntry(token: string, body: Record<string, unknown> = {}) {
  const start = new Date(Date.now() + 3_600_000)
  const api = await newApi()
  const response = await api.post('/api/calendar/entries', {
    data: {
      title: `E2E ${Date.now().toString(36)}`,
      notes: null,
      color: '#3c8cf0',
      allDay: false,
      startAt: start.toISOString(),
      endAt: new Date(start.getTime() + 3_600_000).toISOString(),
      startDate: null,
      endDate: null,
      link: null,
      ...body,
    },
    headers: { ...CSRF, Authorization: `Bearer ${token}`, 'Idempotency-Key': crypto.randomUUID() },
  })
  const status = response.status()
  const json = (await response.json().catch(() => null)) as { id: number; rowVersion: number; type?: string } | null
  await api.dispose()
  return { status, body: json }
}
