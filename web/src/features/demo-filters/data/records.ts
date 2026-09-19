import type { OrderRecord } from '@/features/demo-filters/types'

export const OWNERS = ['Deniz Kaya', 'Selin Arslan', 'Jonas Weber', 'Mira Sandström'] as const
export const CITIES = ['İstanbul', 'İzmir', 'Ankara', 'Bursa', 'Kocaeli', 'Gdańsk', 'Stockholm'] as const
export const TAGS = ['Yenileme', 'İhale', 'Kampanya', 'Riskli', 'Stratejik'] as const

/** 26 sipariş — bir filtre panelinin gerçekten daraltacak kadarı. */
export const ORDER_RECORDS: OrderRecord[] = [
  { id: 'SP-11482', account: 'Nordwind Lojistik', owner: 'Deniz Kaya', city: 'Kocaeli', channel: 'direct', status: 'approved', amount: 862_000, items: 14, createdAt: '2026-08-28', tags: ['Yenileme', 'Stratejik'] },
  { id: 'SP-11479', account: 'Baltic Freight AB', owner: 'Selin Arslan', city: 'Stockholm', channel: 'partner', status: 'pending', amount: 640_500, items: 9, createdAt: '2026-08-30', tags: ['İhale'] },
  { id: 'SP-11491', account: 'Meridian Retail Group', owner: 'Jonas Weber', city: 'İstanbul', channel: 'direct', status: 'paid', amount: 412_000, items: 22, createdAt: '2026-09-01', tags: [] },
  { id: 'SP-11455', account: 'Ege Yapı Malzeme', owner: 'Deniz Kaya', city: 'İzmir', channel: 'direct', status: 'shipped', amount: 318_750, items: 31, createdAt: '2026-08-19', tags: ['Yenileme'] },
  { id: 'SP-11503', account: 'Harborline Shipping', owner: 'Mira Sandström', city: 'Gdańsk', channel: 'partner', status: 'pending', amount: 246_000, items: 6, createdAt: '2026-09-03', tags: [] },
  { id: 'SP-11468', account: 'Anadolu Gıda Dağıtım', owner: 'Selin Arslan', city: 'Ankara', channel: 'online', status: 'paid', amount: 184_400, items: 48, createdAt: '2026-08-24', tags: ['Kampanya'] },
  { id: 'SP-11512', account: 'Kuzey Nakliyat A.Ş.', owner: 'Jonas Weber', city: 'İstanbul', channel: 'direct', status: 'approved', amount: 172_900, items: 11, createdAt: '2026-09-04', tags: ['İhale'] },
  { id: 'SP-11440', account: 'Levant Trade Co.', owner: 'Mira Sandström', city: 'İstanbul', channel: 'partner', status: 'cancelled', amount: 148_250, items: 7, createdAt: '2026-08-12', tags: ['Riskli'] },
  { id: 'SP-11497', account: 'Batı Ege Lojistik', owner: 'Deniz Kaya', city: 'İzmir', channel: 'online', status: 'shipped', amount: 132_000, items: 19, createdAt: '2026-09-02', tags: [] },
  { id: 'SP-11423', account: 'Nordwind Lojistik', owner: 'Selin Arslan', city: 'Kocaeli', channel: 'direct', status: 'refunded', amount: 96_500, items: 4, createdAt: '2026-08-06', tags: ['Riskli'] },
  { id: 'SP-11515', account: 'Trakya Soğuk Zincir', owner: 'Mira Sandström', city: 'Bursa', channel: 'partner', status: 'pending', amount: 94_800, items: 13, createdAt: '2026-09-05', tags: ['Kampanya'] },
  { id: 'SP-11461', account: 'Marmara Tekstil', owner: 'Jonas Weber', city: 'Bursa', channel: 'online', status: 'paid', amount: 88_300, items: 27, createdAt: '2026-08-21', tags: [] },
  { id: 'SP-11436', account: 'Anadolu Gıda Dağıtım', owner: 'Deniz Kaya', city: 'Ankara', channel: 'direct', status: 'approved', amount: 76_150, items: 16, createdAt: '2026-08-10', tags: ['Yenileme'] },
  { id: 'SP-11508', account: 'Gdańsk Port Services', owner: 'Mira Sandström', city: 'Gdańsk', channel: 'partner', status: 'shipped', amount: 71_400, items: 5, createdAt: '2026-09-03', tags: ['Stratejik'] },
  { id: 'SP-11450', account: 'İzmir Liman İşletme', owner: 'Selin Arslan', city: 'İzmir', channel: 'direct', status: 'cancelled', amount: 68_900, items: 8, createdAt: '2026-08-16', tags: [] },
  { id: 'SP-11486', account: 'Meridian Retail Group', owner: 'Jonas Weber', city: 'İstanbul', channel: 'online', status: 'paid', amount: 64_200, items: 39, createdAt: '2026-08-29', tags: ['Kampanya'] },
  { id: 'SP-11429', account: 'Baltic Freight AB', owner: 'Mira Sandström', city: 'Stockholm', channel: 'partner', status: 'refunded', amount: 58_700, items: 3, createdAt: '2026-08-08', tags: ['Riskli'] },
  { id: 'SP-11473', account: 'Ege Yapı Malzeme', owner: 'Deniz Kaya', city: 'İzmir', channel: 'direct', status: 'approved', amount: 54_600, items: 21, createdAt: '2026-08-26', tags: [] },
  { id: 'SP-11519', account: 'Kuzey Nakliyat A.Ş.', owner: 'Selin Arslan', city: 'İstanbul', channel: 'online', status: 'pending', amount: 48_900, items: 12, createdAt: '2026-09-05', tags: ['İhale'] },
  { id: 'SP-11444', account: 'Marmara Tekstil', owner: 'Jonas Weber', city: 'Bursa', channel: 'direct', status: 'shipped', amount: 44_300, items: 18, createdAt: '2026-08-14', tags: [] },
  { id: 'SP-11494', account: 'Trakya Soğuk Zincir', owner: 'Deniz Kaya', city: 'Bursa', channel: 'partner', status: 'paid', amount: 39_800, items: 9, createdAt: '2026-09-01', tags: ['Yenileme'] },
  { id: 'SP-11417', account: 'Harborline Shipping', owner: 'Mira Sandström', city: 'Gdańsk', channel: 'direct', status: 'cancelled', amount: 36_500, items: 6, createdAt: '2026-08-04', tags: ['Riskli'] },
  { id: 'SP-11465', account: 'Batı Ege Lojistik', owner: 'Selin Arslan', city: 'İzmir', channel: 'online', status: 'approved', amount: 31_200, items: 24, createdAt: '2026-08-22', tags: [] },
  { id: 'SP-11500', account: 'İzmir Liman İşletme', owner: 'Jonas Weber', city: 'İzmir', channel: 'direct', status: 'pending', amount: 27_600, items: 7, createdAt: '2026-09-02', tags: ['Kampanya'] },
  { id: 'SP-11432', account: 'Levant Trade Co.', owner: 'Deniz Kaya', city: 'İstanbul', channel: 'partner', status: 'paid', amount: 22_400, items: 15, createdAt: '2026-08-09', tags: [] },
  { id: 'SP-11521', account: 'Gdańsk Port Services', owner: 'Selin Arslan', city: 'Gdańsk', channel: 'online', status: 'pending', amount: 18_950, items: 4, createdAt: '2026-09-06', tags: ['Stratejik'] },
]
