import {
  Building2,
  Factory,
  Globe,
  HandCoins,
  Handshake,
  Mail,
  Megaphone,
  PhoneCall,
  Receipt,
  ScrollText,
  Truck,
  Users,
} from 'lucide-react'
import { apiClient } from '@/api/client'
import { endpoints } from '@/api/endpoints'
import { isArrayOf, unwrapApiResponse } from '@/api/response'
import type {
  AllocationTarget,
  SelectOption,
  SelectOptionGroup,
  TreeNode,
  Unit,
} from '@/components/common/inputs'
import { i18n } from '@/lib/i18n'

/**
 * Every label below resolves from the `demo-forms` catalog at module-load
 * time via the shared `i18n` instance — this is fixture data, not a
 * component, so there is no hook to re-run on a language change. See
 * `stageMeta()` in `StageBadge.tsx` for the same accepted tradeoff elsewhere.
 */
const t = (key: string, options?: Record<string, unknown>) =>
  i18n.t(key, { ns: 'demo-forms', ...options })

/* ---- company and contact ------------------------------------------------- */

export const SECTORS: SelectOption[] = [
  { value: 'bilisim', label: t('options.sectors.bilisim') },
  { value: 'imalat', label: t('options.sectors.imalat') },
  { value: 'perakende', label: t('options.sectors.perakende') },
  { value: 'lojistik', label: t('options.sectors.lojistik') },
  { value: 'insaat', label: t('options.sectors.insaat') },
  { value: 'saglik', label: t('options.sectors.saglik') },
  { value: 'egitim', label: t('options.sectors.egitim') },
  { value: 'turizm', label: t('options.sectors.turizm') },
  { value: 'enerji', label: t('options.sectors.enerji') },
  { value: 'finans', label: t('options.sectors.finans') },
]

/** Grouped the way a sales org thinks about territory, not alphabetically. */
export const CITIES: SelectOptionGroup[] = [
  {
    label: t('options.cityGroups.marmara'),
    options: [
      { value: '34', label: 'İstanbul', description: t('options.plateLabel', { code: '34' }) },
      { value: '16', label: 'Bursa', description: t('options.plateLabel', { code: '16' }) },
      { value: '41', label: 'Kocaeli', description: t('options.plateLabel', { code: '41' }) },
      { value: '59', label: 'Tekirdağ', description: t('options.plateLabel', { code: '59' }) },
    ],
  },
  {
    label: t('options.cityGroups.icAnadolu'),
    options: [
      { value: '06', label: 'Ankara', description: t('options.plateLabel', { code: '06' }) },
      { value: '42', label: 'Konya', description: t('options.plateLabel', { code: '42' }) },
      { value: '38', label: 'Kayseri', description: t('options.plateLabel', { code: '38' }) },
    ],
  },
  {
    label: t('options.cityGroups.egeVeAkdeniz'),
    options: [
      { value: '35', label: 'İzmir', description: t('options.plateLabel', { code: '35' }) },
      { value: '07', label: 'Antalya', description: t('options.plateLabel', { code: '07' }) },
      { value: '01', label: 'Adana', description: t('options.plateLabel', { code: '01' }) },
      { value: '20', label: 'Denizli', description: t('options.plateLabel', { code: '20' }) },
    ],
  },
]

/** Long enough that scrolling stops working — which is the point of a combobox. */
export const CUSTOMERS: SelectOption[] = [
  { value: 'akdeniz-lojistik', label: 'Akdeniz Lojistik A.Ş.', description: t('options.customers.akdenizLojistik') },
  { value: 'anadolu-metal', label: 'Anadolu Metal San. Ltd.', description: t('options.customers.anadoluMetal') },
  { value: 'arge-yazilim', label: 'Arge Yazılım A.Ş.', description: t('options.customers.argeYazilim') },
  { value: 'bati-enerji', label: 'Batı Enerji Holding', description: t('options.customers.batiEnerji') },
  { value: 'bosfor-insaat', label: 'Bosfor İnşaat A.Ş.', description: t('options.customers.bosforInsaat') },
  { value: 'cukurova-gida', label: 'Çukurova Gıda Ltd.', description: t('options.customers.cukurovaGida') },
  { value: 'demir-celik', label: 'Demir Çelik Sanayi', description: t('options.customers.demirCelik') },
  { value: 'ege-tekstil', label: 'Ege Tekstil A.Ş.', description: t('options.customers.egeTekstil') },
  { value: 'fenix-medikal', label: 'Fenix Medikal Ltd.', description: t('options.customers.fenixMedikal') },
  { value: 'gunes-turizm', label: 'Güneş Turizm A.Ş.', description: t('options.customers.gunesTurizm') },
  { value: 'horizon-teknoloji', label: 'Horizon Teknoloji', description: t('options.customers.horizonTeknoloji') },
  { value: 'kuzey-kimya', label: 'Kuzey Kimya San.', description: t('options.customers.kuzeyKimya') },
  { value: 'marmara-market', label: 'Marmara Market Zinciri', description: t('options.customers.marmaraMarket') },
  { value: 'nova-egitim', label: 'Nova Eğitim Kurumları', description: t('options.customers.novaEgitim') },
  { value: 'orion-finans', label: 'Orion Finans A.Ş.', description: t('options.customers.orionFinans') },
  { value: 'pamukkale-nakliyat', label: 'Pamukkale Nakliyat', description: t('options.customers.pamukkaleNakliyat') },
  { value: 'selcuk-makina', label: 'Selçuk Makina Ltd.', description: t('options.customers.selcukMakina') },
  { value: 'toros-tarim', label: 'Toros Tarım A.Ş.', description: t('options.customers.torosTarim') },
  { value: 'vega-otomotiv', label: 'Vega Otomotiv', description: t('options.customers.vegaOtomotiv') },
  { value: 'zirve-danismanlik', label: 'Zirve Danışmanlık', description: t('options.customers.zirveDanismanlik') },
]

export const LEAD_SOURCES: SelectOption[] = [
  { value: 'web', label: t('options.leadSources.web'), icon: Globe },
  { value: 'referans', label: t('options.leadSources.referans'), icon: Handshake },
  { value: 'fuar', label: t('options.leadSources.fuar'), icon: Users },
  { value: 'telefon', label: t('options.leadSources.telefon'), icon: PhoneCall },
  { value: 'eposta', label: t('options.leadSources.eposta'), icon: Mail },
  { value: 'reklam', label: t('options.leadSources.reklam'), icon: Megaphone },
]

export const SEGMENTS: SelectOption[] = [
  { value: 'kurumsal', label: t('options.segments.kurumsal.label'), description: t('options.segments.kurumsal.description'), icon: Building2 },
  { value: 'kobi', label: t('options.segments.kobi.label'), description: t('options.segments.kobi.description'), icon: Factory },
  { value: 'bayi', label: t('options.segments.bayi.label'), description: t('options.segments.bayi.description'), icon: Truck },
]

/* ---- commercial terms ---------------------------------------------------- */

/** Turkish VAT bands. `value` is the rate itself, so the label never lies. */
export const VAT_RATES = [0, 1, 10, 20] as const
export type VatRate = (typeof VAT_RATES)[number]

export const PAYMENT_TERMS: SelectOption[] = [
  { value: 'pesin', label: t('options.paymentTerms.pesin') },
  { value: 'vade-30', label: t('options.paymentTerms.vade30') },
  { value: 'vade-60', label: t('options.paymentTerms.vade60') },
  { value: 'vade-90', label: t('options.paymentTerms.vade90') },
  { value: 'kapida', label: t('options.paymentTerms.kapida') },
]

export const DOCUMENT_TYPES: SelectOption[] = [
  {
    value: 'fatura',
    label: t('options.documentTypes.fatura.label'),
    description: t('options.documentTypes.fatura.description'),
    icon: Receipt,
  },
  {
    value: 'proforma',
    label: t('options.documentTypes.proforma.label'),
    description: t('options.documentTypes.proforma.description'),
    icon: ScrollText,
  },
  {
    value: 'irsaliye',
    label: t('options.documentTypes.irsaliye.label'),
    description: t('options.documentTypes.irsaliye.description'),
    icon: Truck,
  },
]

export const PRIORITIES: SelectOption[] = [
  { value: 'dusuk', label: t('options.priorities.dusuk') },
  { value: 'normal', label: t('options.priorities.normal') },
  { value: 'yuksek', label: t('options.priorities.yuksek') },
  { value: 'acil', label: t('options.priorities.acil') },
]

export const WAREHOUSES: SelectOption[] = [
  { value: 'merkez', label: t('options.warehouses.merkez') },
  { value: 'ege', label: t('options.warehouses.ege') },
  { value: 'avrupa', label: t('options.warehouses.avrupa') },
  { value: 'konsinye', label: t('options.warehouses.konsinye'), disabled: true },
]

export const DELIVERY_TERMS: SelectOption[] = [
  { value: 'exw', label: t('options.deliveryTerms.exw.label'), description: t('options.deliveryTerms.exw.description') },
  { value: 'fob', label: t('options.deliveryTerms.fob.label'), description: t('options.deliveryTerms.fob.description') },
  { value: 'cif', label: t('options.deliveryTerms.cif.label'), description: t('options.deliveryTerms.cif.description') },
  { value: 'ddp', label: t('options.deliveryTerms.ddp.label'), description: t('options.deliveryTerms.ddp.description') },
]

export const PERMISSIONS: SelectOption[] = [
  { value: 'teklif', label: t('options.permissions.teklif') },
  { value: 'iskonto', label: t('options.permissions.iskonto.label'), description: t('options.permissions.iskonto.description') },
  { value: 'fatura', label: t('options.permissions.fatura') },
  { value: 'rapor', label: t('options.permissions.rapor') },
  { value: 'tahsilat', label: t('options.permissions.tahsilat'), icon: HandCoins },
]

export const WEEKDAYS: SelectOption[] = [
  { value: 'pzt', label: t('options.weekdays.pzt.label'), description: t('options.weekdays.pzt.description') },
  { value: 'sal', label: t('options.weekdays.sal.label'), description: t('options.weekdays.sal.description') },
  { value: 'car', label: t('options.weekdays.car.label'), description: t('options.weekdays.car.description') },
  { value: 'per', label: t('options.weekdays.per.label'), description: t('options.weekdays.per.description') },
  { value: 'cum', label: t('options.weekdays.cum.label'), description: t('options.weekdays.cum.description') },
  { value: 'cmt', label: t('options.weekdays.cmt.label'), description: t('options.weekdays.cmt.description') },
  { value: 'paz', label: t('options.weekdays.paz.label'), description: t('options.weekdays.paz.description') },
]

/* ---- catalogue ----------------------------------------------------------- */

export interface CatalogueItem {
  code: string
  name: string
  unit: Unit
  unitPrice: number
  vatRate: VatRate
}

/** The price list the line editor picks from — deterministic, no faker. */
export const CATALOGUE: CatalogueItem[] = [
  { code: 'LIC-CRM-01', name: t('options.catalogue.licCrm01'), unit: 'adet', unitPrice: 4800, vatRate: 20 },
  { code: 'LIC-ERP-01', name: t('options.catalogue.licErp01'), unit: 'adet', unitPrice: 12500, vatRate: 20 },
  { code: 'SRV-KUR-01', name: t('options.catalogue.srvKur01'), unit: 'saat', unitPrice: 1750, vatRate: 20 },
  { code: 'SRV-EGT-01', name: t('options.catalogue.srvEgt01'), unit: 'gun', unitPrice: 9500, vatRate: 20 },
  { code: 'SRV-BAK-01', name: t('options.catalogue.srvBak01'), unit: 'adet', unitPrice: 22000, vatRate: 20 },
  { code: 'DON-SRV-01', name: t('options.catalogue.donSrv01'), unit: 'adet', unitPrice: 86000, vatRate: 20 },
  { code: 'DON-BAR-01', name: t('options.catalogue.donBar01'), unit: 'adet', unitPrice: 14750, vatRate: 20 },
  { code: 'SRF-KAG-01', name: t('options.catalogue.srfKag01'), unit: 'kutu', unitPrice: 640, vatRate: 20 },
  { code: 'GID-SU-01', name: t('options.catalogue.gidSu01'), unit: 'lt', unitPrice: 12.5, vatRate: 1 },
  { code: 'KIT-EGT-01', name: t('options.catalogue.kitEgt01'), unit: 'paket', unitPrice: 380, vatRate: 10 },
]

export const CATALOGUE_OPTIONS: SelectOption[] = CATALOGUE.map((item) => ({
  value: item.code,
  label: item.name,
  description: `${item.code} · ${item.unit}`,
}))

/* ---- geography, for the dependent selects -------------------------------- */

interface District {
  value: string
  label: string
  neighbourhoods: string[]
}

/** A slice deep enough to show three real levels, not the whole country. */
export const PROVINCES: { value: string; label: string; districts: District[] }[] = [
  {
    value: '34',
    label: 'İstanbul',
    districts: [
      { value: 'kadikoy', label: 'Kadıköy', neighbourhoods: ['Caferağa', 'Fenerbahçe', 'Göztepe'] },
      { value: 'sisli', label: 'Şişli', neighbourhoods: ['Esentepe', 'Mecidiyeköy', 'Teşvikiye'] },
      { value: 'beyoglu', label: 'Beyoğlu', neighbourhoods: ['Asmalımescit', 'Cihangir', 'Galata'] },
    ],
  },
  {
    value: '06',
    label: 'Ankara',
    districts: [
      { value: 'cankaya', label: 'Çankaya', neighbourhoods: ['Bahçelievler', 'Kavaklıdere', 'Oran'] },
      { value: 'yenimahalle', label: 'Yenimahalle', neighbourhoods: ['Batıkent', 'Demetevler'] },
    ],
  },
  {
    value: '35',
    label: 'İzmir',
    districts: [
      { value: 'konak', label: 'Konak', neighbourhoods: ['Alsancak', 'Güzelyalı'] },
      { value: 'bornova', label: 'Bornova', neighbourhoods: ['Erzene', 'Kazımdirik'] },
    ],
  },
]

export const ADDRESS_LEVELS = [
  {
    label: t('options.addressLevels.province'),
    optionsFor: () => PROVINCES.map(({ value, label }) => ({ value, label })),
  },
  {
    label: t('options.addressLevels.district'),
    optionsFor: (path: readonly (string | null)[]) =>
      PROVINCES.find((province) => province.value === path[0])?.districts.map(
        ({ value, label }) => ({ value, label }),
      ) ?? [],
  },
  {
    label: t('options.addressLevels.neighbourhood'),
    optionsFor: (path: readonly (string | null)[]) =>
      PROVINCES.find((province) => province.value === path[0])
        ?.districts.find((district) => district.value === path[1])
        ?.neighbourhoods.map((name) => ({ value: name, label: name })) ?? [],
  },
]

/* ---- chart of accounts, for the tree select ------------------------------ */

export const CHART_OF_ACCOUNTS: TreeNode[] = [
  {
    value: '600',
    label: t('options.chartOfAccounts.domesticSales'),
    code: '600',
    children: [
      { value: '600.01', label: t('options.chartOfAccounts.productSales'), code: '600.01' },
      { value: '600.02', label: t('options.chartOfAccounts.serviceSales'), code: '600.02' },
      { value: '600.03', label: t('options.chartOfAccounts.maintenanceSupport'), code: '600.03' },
    ],
  },
  {
    value: '601',
    label: t('options.chartOfAccounts.foreignSales'),
    code: '601',
    children: [
      { value: '601.01', label: t('options.chartOfAccounts.europe'), code: '601.01' },
      { value: '601.02', label: t('options.chartOfAccounts.middleEast'), code: '601.02' },
    ],
  },
  {
    value: '770',
    label: t('options.chartOfAccounts.generalAdmin'),
    code: '770',
    children: [
      { value: '770.01', label: t('options.chartOfAccounts.staffExpenses'), code: '770.01' },
      {
        value: '770.02',
        label: t('options.chartOfAccounts.officeExpenses'),
        code: '770.02',
        children: [
          { value: '770.02.01', label: t('options.chartOfAccounts.rent'), code: '770.02.01' },
          { value: '770.02.02', label: t('options.chartOfAccounts.utilities'), code: '770.02.02' },
          { value: '770.02.03', label: t('options.chartOfAccounts.internetPhone'), code: '770.02.03' },
        ],
      },
      { value: '770.03', label: t('options.chartOfAccounts.consulting'), code: '770.03' },
    ],
  },
]

/* ---- allocation and rates ------------------------------------------------ */

export const COST_CENTERS: AllocationTarget[] = [
  { id: 'satis', label: t('options.costCenters.satis'), description: 'MC-100' },
  { id: 'pazarlama', label: t('options.costCenters.pazarlama'), description: 'MC-200' },
  { id: 'urun', label: t('options.costCenters.urun'), description: 'MC-300' },
  { id: 'destek', label: t('options.costCenters.destek'), description: 'MC-400' },
]

/** Stand-in for a daily rate feed; a real one would come from the API. */
export const SUGGESTED_RATES = { USD: 41.28, EUR: 44.9, GBP: 52.15 } as const

/** `GET /demo-forms/customers?q=`, served by MSW — aborts via the shared axios instance. */
export async function searchCustomers(query: string, signal: AbortSignal): Promise<SelectOption[]> {
  const { data } = await apiClient.get<unknown>(endpoints.demoForms.customers, {
    params: { q: query },
    signal,
  })
  return unwrapApiResponse(data, isArrayOf<SelectOption>)
}
