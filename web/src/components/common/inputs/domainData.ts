/**
 * Deterministic mock catalogues for the domain-tier pickers (`CustomerPicker`,
 * `ProductPicker`, `ErpCodeField`) — small, fixed, no faker. A real
 * integration replaces `searchDomainRecords`'s body with an API call; the
 * pickers themselves don't change.
 */
export interface DomainRecord {
  id: string
  display: string
  subtitle?: string
}

export const MOCK_CUSTOMERS: DomainRecord[] = [
  { id: 'akdeniz-lojistik', display: 'Akdeniz Lojistik A.Ş.', subtitle: 'İstanbul' },
  { id: 'anadolu-metal', display: 'Anadolu Metal San. Ltd.', subtitle: 'Kocaeli' },
  { id: 'arge-yazilim', display: 'Arge Yazılım A.Ş.', subtitle: 'Ankara' },
  { id: 'bati-enerji', display: 'Batı Enerji Holding', subtitle: 'İzmir' },
  { id: 'bosfor-insaat', display: 'Bosfor İnşaat A.Ş.', subtitle: 'İstanbul' },
  { id: 'cukurova-gida', display: 'Çukurova Gıda Ltd.', subtitle: 'Adana' },
  { id: 'demir-celik', display: 'Demir Çelik Sanayi', subtitle: 'Kocaeli' },
  { id: 'ege-tekstil', display: 'Ege Tekstil A.Ş.', subtitle: 'İzmir' },
  { id: 'fenix-medikal', display: 'Fenix Medikal Ltd.', subtitle: 'Ankara' },
  { id: 'gunes-turizm', display: 'Güneş Turizm A.Ş.', subtitle: 'Antalya' },
]

export const MOCK_PRODUCTS: DomainRecord[] = [
  { id: 'LIC-CRM-01', display: 'CRM Lisansı', subtitle: 'adet · ₺4.800' },
  { id: 'LIC-ERP-01', display: 'ERP Lisansı', subtitle: 'adet · ₺12.500' },
  { id: 'SRV-KUR-01', display: 'Kurulum Hizmeti', subtitle: 'saat · ₺1.750' },
  { id: 'SRV-EGT-01', display: 'Eğitim Hizmeti', subtitle: 'gün · ₺9.500' },
  { id: 'SRV-BAK-01', display: 'Bakım Sözleşmesi', subtitle: 'adet · ₺22.000' },
  { id: 'DON-SRV-01', display: 'Sunucu Donanımı', subtitle: 'adet · ₺86.000' },
  { id: 'DON-BAR-01', display: 'Barkod Okuyucu', subtitle: 'adet · ₺14.750' },
  { id: 'SRF-KAG-01', display: 'A4 Kağıt Kutusu', subtitle: 'kutu · ₺640' },
]

export const MOCK_ERP_CODES: DomainRecord[] = [
  { id: '600.01', display: 'Yurt İçi Satışlar — Ürün Satışları', subtitle: '600.01' },
  { id: '600.02', display: 'Yurt İçi Satışlar — Hizmet Satışları', subtitle: '600.02' },
  { id: '600.03', display: 'Yurt İçi Satışlar — Bakım ve Destek', subtitle: '600.03' },
  { id: '601.01', display: 'Yurt Dışı Satışlar — Avrupa', subtitle: '601.01' },
  { id: '601.02', display: 'Yurt Dışı Satışlar — Orta Doğu', subtitle: '601.02' },
  { id: '770.01', display: 'Genel Yönetim — Personel Giderleri', subtitle: '770.01' },
  { id: '770.02.01', display: 'Genel Yönetim — Kira', subtitle: '770.02.01' },
  { id: '770.02.02', display: 'Genel Yönetim — Elektrik/Su', subtitle: '770.02.02' },
  { id: '770.03', display: 'Genel Yönetim — Danışmanlık', subtitle: '770.03' },
]

/**
 * Filters `records` client-side and resolves after a short delay so the
 * pickers exercise the same loading/abort path a real network call would.
 * `signal` is accepted (not read) to keep the same shape as `AsyncCombobox`'s
 * `onSearch` — a real implementation passes it straight to `fetch`/`axios`.
 */
export function searchDomainRecords(
  records: readonly DomainRecord[],
  query: string,
  _signal?: AbortSignal,
): Promise<DomainRecord[]> {
  const needle = query.trim().toLocaleLowerCase('tr')
  const results = records.filter((record) =>
    `${record.display} ${record.subtitle ?? ''}`.toLocaleLowerCase('tr').includes(needle),
  )
  return new Promise((resolve) => setTimeout(() => resolve(results.slice(0, 8)), 250))
}
