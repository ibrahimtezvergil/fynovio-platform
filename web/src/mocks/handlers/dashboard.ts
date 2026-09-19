import { delay, http, HttpResponse } from 'msw'
import { endpoints } from '@/api/endpoints'
import { API_BASE } from '@/mocks/apiBase'
import type { Activity, Deal, StageBucket } from '@/types'

const MOCK_DEALS: Deal[] = [
  { id: '1', title: 'Filo takip yenileme', account: 'Nordwind Lojistik', stage: 'meeting', owner: 'Deniz Kaya', value: 862_000, closeDate: '2026-09-18', probability: 68 },
  { id: '2', title: 'Warehouse API rollout', account: 'Baltic Freight AB', stage: 'quoted', owner: 'Selin Arslan', value: 640_500, closeDate: '2026-09-30', probability: 54 },
  { id: '3', title: 'Seat expansion — 40 kullanıcı', account: 'Meridian Retail Group', stage: 'new', owner: 'Jonas Weber', value: 412_000, closeDate: '2026-10-07', probability: 22 },
  { id: '4', title: 'Yıllık destek paketi', account: 'Ege Yapı Malzeme', stage: 'meeting', owner: 'Deniz Kaya', value: 318_750, closeDate: '2026-09-12', probability: 61 },
  { id: '5', title: 'Reporting add-on', account: 'Harborline Shipping', stage: 'contacted', owner: 'Mira Sandström', value: 246_000, closeDate: '2026-10-22', probability: 35 },
  { id: '6', title: 'Pilot — 3 depo', account: 'Anadolu Gıda Dağıtım', stage: 'ready', owner: 'Selin Arslan', value: 184_400, closeDate: '2026-11-04', probability: 80 },
]

/** Whole-pipeline roll-up (38 open deals), not just the six rows the grid shows. */
const MOCK_STAGES: StageBucket[] = [
  { stage: 'new', count: 11, value: 1_204_000 },
  { stage: 'contacted', count: 9, value: 946_500 },
  { stage: 'quoted', count: 6, value: 694_700 },
  { stage: 'meeting', count: 8, value: 812_300 },
  { stage: 'ready', count: 4, value: 525_000 },
]

const MOCK_ACTIVITIES: Activity[] = [
  { id: 'a1', kind: 'task', title: 'Baltic Freight AB · teklif revizyonu', when: 'Bugün 14:30' },
  { id: 'a2', kind: 'message', title: 'Meridian Retail — demo geri bildirimi', when: 'Bugün 16:00' },
  { id: 'a3', kind: 'email', title: 'Ege Yapı Malzeme · sözleşme taslağı', when: 'Yarın 09:15' },
  { id: 'a4', kind: 'review', title: 'Harborline · güvenlik değerlendirmesi', when: 'Yarın 11:00' },
]

export const dashboardHandlers = [
  http.get(`${API_BASE}${endpoints.dashboard.deals}`, async () => {
    await delay(300)
    return HttpResponse.json(MOCK_DEALS)
  }),
  http.get(`${API_BASE}${endpoints.dashboard.stages}`, async () => {
    await delay(300)
    return HttpResponse.json(MOCK_STAGES)
  }),
  http.get(`${API_BASE}${endpoints.dashboard.activities}`, async () => {
    await delay(300)
    return HttpResponse.json(MOCK_ACTIVITIES)
  }),
]
