import { delay, http, HttpResponse } from 'msw'
import { endpoints } from '@/api/endpoints'
import { API_BASE } from '@/mocks/apiBase'
import { paths } from '@/routes/paths'
import type { AttentionItem, RecentWorkItem, TeamActivityItem } from '@/features/home/schema'

/**
 * Same organizations/documents across attention, recent work and team
 * activity — and, via `src/lib/search/entitySearch.ts`, the command bar too —
 * so the mock reads as one consistent book of business rather than isolated
 * fixtures. Baltic Freight AB, Meridian Retail Group and Ege Yapı Malzeme are
 * shared with `src/mocks/handlers/dashboard.ts`'s deal list on purpose.
 */
const MOCK_ATTENTION_ITEMS: AttentionItem[] = [
  {
    id: 'att-1',
    type: 'delivery-risk',
    severity: 'critical',
    title: 'Teslimat riski',
    description: '3 ürün stokta yetersiz.',
    entityType: 'order',
    entityId: 'SO-23891',
    entityLabel: 'ACME Holding',
    documentNumber: 'SO-23891',
    priority: 1,
    timestamp: '18 Eyl teslimat',
    owner: 'Deniz Kaya',
    action: { label: 'İncele', url: paths.crmOpportunities },
  },
  {
    id: 'att-2',
    type: 'approval-pending',
    severity: 'warning',
    title: 'Onay bekliyor',
    description: '2 saattir onay bekliyor.',
    entityType: 'quote',
    entityId: 'Q-8392',
    entityLabel: 'Baltic Freight AB',
    documentNumber: 'Q-8392',
    amount: 1_240_000,
    priority: 2,
    timestamp: '2 saat önce',
    owner: 'Selin Arslan',
    action: { label: 'İncele', url: paths.crmOpportunities },
  },
  {
    id: 'att-3',
    type: 'quote-expiring',
    severity: 'warning',
    title: 'Bugün sona eriyor',
    description: 'Teklif bugün geçerliliğini kaybediyor.',
    entityType: 'quote',
    entityId: 'Q-1292',
    entityLabel: 'Meridian Retail Group',
    documentNumber: 'Q-1292',
    priority: 3,
    timestamp: 'Bugün',
    owner: 'Jonas Weber',
    action: { label: 'İncele', url: paths.crmOpportunities },
  },
  {
    id: 'att-4',
    type: 'stale-deal',
    severity: 'warning',
    title: 'Takip gecikti',
    description: '6 gündür güncelleme yok.',
    entityType: 'deal',
    entityId: 'Ege Yapı Malzeme',
    entityLabel: 'Ege Yapı Malzeme',
    priority: 4,
    timestamp: '6 gün önce',
    owner: 'Deniz Kaya',
    action: { label: 'İncele', url: paths.crmOpportunities },
  },
]

const MOCK_RECENT_WORK: RecentWorkItem[] = [
  {
    id: 'rw-1',
    entityType: 'quote',
    entityId: 'Q-2381',
    title: 'Q-2381',
    subtitle: 'ACME Holding',
    reference: 'Teklif',
    route: paths.crmOpportunities,
    lastAccessedAt: '12 dk önce',
  },
  {
    id: 'rw-2',
    entityType: 'order',
    entityId: 'SO-291',
    title: 'SO-291',
    subtitle: 'Baltic Freight AB',
    reference: 'Sipariş',
    route: paths.crmOpportunities,
    lastAccessedAt: '38 dk önce',
  },
  {
    id: 'rw-3',
    entityType: 'customer',
    entityId: 'Meridian Retail Group',
    title: 'Meridian Retail Group',
    reference: 'Müşteri',
    route: paths.crmOpportunities,
    lastAccessedAt: 'Dün',
  },
]

const MOCK_TEAM_ACTIVITY: TeamActivityItem[] = [
  {
    id: 'ta-1',
    user: 'Deniz Kaya',
    avatarInitials: 'DK',
    action: 'ACME teklifini düzenliyor',
    entityType: 'quote',
    entityId: 'Q-2381',
    entityLabel: 'ACME Holding',
    timestamp: 'şimdi',
    route: paths.crmOpportunities,
  },
  {
    id: 'ta-2',
    user: 'Selin Arslan',
    avatarInitials: 'SA',
    action: 'Baltic Freight siparişini inceliyor',
    entityType: 'order',
    entityId: 'SO-291',
    entityLabel: 'Baltic Freight AB',
    timestamp: '4 dk önce',
    route: paths.crmOpportunities,
  },
  {
    id: 'ta-3',
    user: 'Jonas Weber',
    avatarInitials: 'JW',
    action: 'Meridian müşteri kaydını görüntülüyor',
    entityType: 'customer',
    entityId: 'Meridian Retail Group',
    entityLabel: 'Meridian Retail Group',
    timestamp: '12 dk önce',
    route: paths.crmOpportunities,
  },
]

export const homeHandlers = [
  http.get(`${API_BASE}${endpoints.home.attention}`, async () => {
    await delay(300)
    return HttpResponse.json(MOCK_ATTENTION_ITEMS)
  }),
  http.get(`${API_BASE}${endpoints.home.recentWork}`, async () => {
    await delay(300)
    return HttpResponse.json(MOCK_RECENT_WORK)
  }),
  http.get(`${API_BASE}${endpoints.home.teamActivity}`, async () => {
    await delay(300)
    return HttpResponse.json(MOCK_TEAM_ACTIVITY)
  }),
]
