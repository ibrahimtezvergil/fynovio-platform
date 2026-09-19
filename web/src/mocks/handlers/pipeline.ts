import { delay, http, HttpResponse } from 'msw'
import { endpoints } from '@/api/endpoints'
import { API_BASE } from '@/mocks/apiBase'
import type { Deal } from '@/types'

/** The ten open deals the Pipeline artboard draws, verbatim. */
const PIPELINE_DEALS: Deal[] = [
  { id: 'd-1', title: 'Filo takip yenileme', account: 'Nordwind Lojistik', owner: 'Deniz Kaya', stage: 'meeting', probability: 68, value: 862_000, closeDate: '2026-09-18' },
  { id: 'd-2', title: 'Warehouse API rollout', account: 'Baltic Freight AB', owner: 'Selin Arslan', stage: 'quoted', probability: 54, value: 640_500, closeDate: '2026-09-30' },
  { id: 'd-3', title: 'Seat expansion — 40 kullanıcı', account: 'Meridian Retail Group', owner: 'Jonas Weber', stage: 'new', probability: 22, value: 412_000, closeDate: '2026-10-07' },
  { id: 'd-4', title: 'Yıllık destek paketi', account: 'Ege Yapı Malzeme', owner: 'Deniz Kaya', stage: 'meeting', probability: 61, value: 318_750, closeDate: '2026-09-12' },
  { id: 'd-5', title: 'Reporting add-on', account: 'Harborline Shipping', owner: 'Mira Sandström', stage: 'contacted', probability: 35, value: 246_000, closeDate: '2026-10-22' },
  { id: 'd-6', title: 'Pilot — 3 depo', account: 'Anadolu Gıda Dağıtım', owner: 'Selin Arslan', stage: 'ready', probability: 80, value: 184_400, closeDate: '2026-11-04' },
  { id: 'd-7', title: 'Rota optimizasyonu modülü', account: 'Kuzey Nakliyat A.Ş.', owner: 'Jonas Weber', stage: 'quoted', probability: 47, value: 172_900, closeDate: '2026-11-19' },
  { id: 'd-8', title: 'Gümrük entegrasyonu', account: 'Levant Trade Co.', owner: 'Mira Sandström', stage: 'contacted', probability: 28, value: 148_250, closeDate: '2026-12-02' },
  { id: 'd-9', title: 'Mobil sürücü uygulaması', account: 'Batı Ege Lojistik', owner: 'Deniz Kaya', stage: 'new', probability: 18, value: 132_000, closeDate: '2026-12-15' },
  { id: 'd-10', title: 'SLA yükseltme', account: 'Nordwind Lojistik', owner: 'Selin Arslan', stage: 'onhold', probability: 12, value: 96_500, closeDate: null },
]

export const pipelineHandlers = [
  http.get(`${API_BASE}${endpoints.pipeline.deals}`, async () => {
    await delay(300)
    return HttpResponse.json(PIPELINE_DEALS)
  }),
]
