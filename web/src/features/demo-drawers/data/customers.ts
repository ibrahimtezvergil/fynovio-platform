import { i18n } from '@/lib/i18n'
import type { StatusRegistry } from '@/components/common/StatusBadge'

export const CUSTOMER_STATUSES = ['active', 'prospect', 'risk', 'churned'] as const
export type CustomerStatus = (typeof CUSTOMER_STATUSES)[number]

/**
 * Resolved against the active language at call time — this whole module is
 * evaluated once at import, so (like `stageMeta()`) it renders whatever
 * language was active on load rather than switching live.
 */
export function customerStatusMeta(): StatusRegistry<CustomerStatus> {
  const tone: Record<CustomerStatus, StatusRegistry<CustomerStatus>[CustomerStatus]['tone']> = {
    active: 'green',
    prospect: 'blue',
    risk: 'amber',
    churned: 'gray',
  }
  return CUSTOMER_STATUSES.reduce((meta, status) => {
    meta[status] = {
      label: i18n.t(`customerStatus.${status}`, { ns: 'demo-drawers' }),
      tone: tone[status],
    }
    return meta
  }, {} as StatusRegistry<CustomerStatus>)
}

export const CUSTOMER_STATUS: StatusRegistry<CustomerStatus> = customerStatusMeta()

export interface CustomerContact {
  name: string
  role: string
  email: string
}

export interface CustomerDocument {
  name: string
  size: string
  date: string
}

/** The drawer's history is a summary, not the full feed — time plus one line. */
export interface CustomerEvent {
  time: string
  title: string
  note: string
}

export interface CustomerRecord {
  id: string
  name: string
  segment: string
  owner: string
  status: CustomerStatus
  city: string
  email: string
  phone: string
  taxId: string
  openDeals: number
  lifetimeValue: number
  paymentTerm: string
  lastContact: string
  note: string
  contacts: CustomerContact[]
  documents: CustomerDocument[]
  history: CustomerEvent[]
}

const t = (key: string) => i18n.t(key, { ns: 'demo-drawers' })

export const CUSTOMERS: CustomerRecord[] = [
  {
    id: 'CR-1042',
    name: 'Nordwind Lojistik',
    segment: t('customers.cr1042.segment'),
    owner: 'Deniz Kaya',
    status: 'active',
    city: 'Kocaeli',
    email: 'satinalma@nordwind.com.tr',
    phone: '+90 262 555 04 18',
    taxId: '4820135697',
    openDeals: 3,
    lifetimeValue: 4_280_000,
    paymentTerm: t('customers.cr1042.paymentTerm'),
    lastContact: t('customers.cr1042.lastContact'),
    note: t('customers.cr1042.note'),
    contacts: [
      { name: 'Hakan Yılmaz', role: t('customers.cr1042.contacts.0.role'), email: 'h.yilmaz@nordwind.com.tr' },
      { name: 'Pınar Ateş', role: t('customers.cr1042.contacts.1.role'), email: 'p.ates@nordwind.com.tr' },
    ],
    documents: [
      { name: t('customers.cr1042.documents.0.name'), size: '2,4 MB', date: t('customers.cr1042.documents.0.date') },
      { name: t('customers.cr1042.documents.1.name'), size: '860 KB', date: t('customers.cr1042.documents.1.date') },
    ],
    history: [
      {
        time: t('customers.cr1042.history.0.time'),
        title: t('customers.cr1042.history.0.title'),
        note: t('customers.cr1042.history.0.note'),
      },
      {
        time: t('customers.cr1042.history.1.time'),
        title: t('customers.cr1042.history.1.title'),
        note: t('customers.cr1042.history.1.note'),
      },
      {
        time: t('customers.cr1042.history.2.time'),
        title: t('customers.cr1042.history.2.title'),
        note: t('customers.cr1042.history.2.note'),
      },
    ],
  },
  {
    id: 'CR-1088',
    name: 'Baltic Freight AB',
    segment: t('customers.cr1088.segment'),
    owner: 'Selin Arslan',
    status: 'prospect',
    city: 'Stockholm',
    email: 'procurement@balticfreight.se',
    phone: '+46 8 555 21 90',
    taxId: 'SE556012345601',
    openDeals: 2,
    lifetimeValue: 0,
    paymentTerm: t('customers.cr1088.paymentTerm'),
    lastContact: t('customers.cr1088.lastContact'),
    note: t('customers.cr1088.note'),
    contacts: [
      { name: 'Erik Lund', role: t('customers.cr1088.contacts.0.role'), email: 'e.lund@balticfreight.se' },
      { name: 'Anna Berg', role: t('customers.cr1088.contacts.1.role'), email: 'a.berg@balticfreight.se' },
    ],
    documents: [
      { name: t('customers.cr1088.documents.0.name'), size: '1,1 MB', date: t('customers.cr1088.documents.0.date') },
    ],
    history: [
      {
        time: t('customers.cr1088.history.0.time'),
        title: t('customers.cr1088.history.0.title'),
        note: t('customers.cr1088.history.0.note'),
      },
      {
        time: t('customers.cr1088.history.1.time'),
        title: t('customers.cr1088.history.1.title'),
        note: t('customers.cr1088.history.1.note'),
      },
    ],
  },
  {
    id: 'CR-0977',
    name: 'Ege Yapı Malzeme',
    segment: t('customers.cr0977.segment'),
    owner: 'Deniz Kaya',
    status: 'risk',
    city: 'İzmir',
    email: 'muhasebe@egeyapi.com.tr',
    phone: '+90 232 555 76 41',
    taxId: '3310928475',
    openDeals: 1,
    lifetimeValue: 1_120_000,
    paymentTerm: t('customers.cr0977.paymentTerm'),
    lastContact: t('customers.cr0977.lastContact'),
    note: t('customers.cr0977.note'),
    contacts: [
      { name: 'Murat Şen', role: t('customers.cr0977.contacts.0.role'), email: 'm.sen@egeyapi.com.tr' },
    ],
    documents: [
      { name: t('customers.cr0977.documents.0.name'), size: '48 KB', date: t('customers.cr0977.documents.0.date') },
      { name: t('customers.cr0977.documents.1.name'), size: '1,8 MB', date: t('customers.cr0977.documents.1.date') },
    ],
    history: [
      {
        time: t('customers.cr0977.history.0.time'),
        title: t('customers.cr0977.history.0.title'),
        note: t('customers.cr0977.history.0.note'),
      },
      {
        time: t('customers.cr0977.history.1.time'),
        title: t('customers.cr0977.history.1.title'),
        note: t('customers.cr0977.history.1.note'),
      },
    ],
  },
  {
    id: 'CR-1130',
    name: 'Meridian Retail Group',
    segment: t('customers.cr1130.segment'),
    owner: 'Jonas Weber',
    status: 'active',
    city: 'İstanbul',
    email: 'it@meridianretail.com',
    phone: '+90 212 555 33 07',
    taxId: '7719034628',
    openDeals: 4,
    lifetimeValue: 6_940_000,
    paymentTerm: t('customers.cr1130.paymentTerm'),
    lastContact: t('customers.cr1130.lastContact'),
    note: t('customers.cr1130.note'),
    contacts: [
      { name: 'Ceren Doğan', role: t('customers.cr1130.contacts.0.role'), email: 'c.dogan@meridianretail.com' },
      { name: 'Levent Akar', role: t('customers.cr1130.contacts.1.role'), email: 'l.akar@meridianretail.com' },
      { name: 'Sibel Uçar', role: t('customers.cr1130.contacts.2.role'), email: 's.ucar@meridianretail.com' },
    ],
    documents: [
      { name: t('customers.cr1130.documents.0.name'), size: '640 KB', date: t('customers.cr1130.documents.0.date') },
    ],
    history: [
      {
        time: t('customers.cr1130.history.0.time'),
        title: t('customers.cr1130.history.0.title'),
        note: t('customers.cr1130.history.0.note'),
      },
      {
        time: t('customers.cr1130.history.1.time'),
        title: t('customers.cr1130.history.1.title'),
        note: t('customers.cr1130.history.1.note'),
      },
    ],
  },
  {
    id: 'CR-0851',
    name: 'Harborline Shipping',
    segment: t('customers.cr0851.segment'),
    owner: 'Mira Sandström',
    status: 'churned',
    city: 'Gdańsk',
    email: 'office@harborline.pl',
    phone: '+48 58 555 12 04',
    taxId: 'PL5842710093',
    openDeals: 0,
    lifetimeValue: 780_000,
    paymentTerm: t('customers.cr0851.paymentTerm'),
    lastContact: t('customers.cr0851.lastContact'),
    note: t('customers.cr0851.note'),
    contacts: [
      { name: 'Marek Nowak', role: t('customers.cr0851.contacts.0.role'), email: 'm.nowak@harborline.pl' },
    ],
    documents: [
      { name: t('customers.cr0851.documents.0.name'), size: '210 KB', date: t('customers.cr0851.documents.0.date') },
    ],
    history: [
      {
        time: t('customers.cr0851.history.0.time'),
        title: t('customers.cr0851.history.0.title'),
        note: t('customers.cr0851.history.0.note'),
      },
      {
        time: t('customers.cr0851.history.1.time'),
        title: t('customers.cr0851.history.1.title'),
        note: t('customers.cr0851.history.1.note'),
      },
    ],
  },
]

export const money = new Intl.NumberFormat('tr-TR', {
  style: 'currency',
  currency: 'TRY',
  maximumFractionDigits: 0,
})
