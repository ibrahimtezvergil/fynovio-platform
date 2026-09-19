import { i18n } from '@/lib/i18n'
import { minutesAgo } from '@/lib/datetime'
import type { TimelineEntry } from '@/features/demo-timeline/types'

const t = (key: string) => i18n.t(key, { ns: 'demo-timeline' })

/**
 * A customer's history, newest first. This is the CRM shape: mostly
 * touchpoints, with the occasional state change threaded through them.
 *
 * Resolved against the active language at import time — see `stageMeta()` in
 * `StageBadge.tsx` for why this module doesn't switch live.
 */
export const CUSTOMER_FEED: TimelineEntry[] = [
  {
    id: 'a-1',
    type: 'note',
    actor: 'Deniz Kaya',
    title: t('customer.a1.title'),
    detail: t('customer.a1.detail'),
    at: minutesAgo(4_320),
    pinned: true,
  },
  {
    id: 'a-2',
    type: 'stage',
    actor: 'Deniz Kaya',
    title: t('customer.a2.title'),
    at: minutesAgo(38),
    change: { field: t('customer.a2.change.field'), from: t('customer.a2.change.from'), to: t('customer.a2.change.to') },
  },
  {
    id: 'a-3',
    type: 'call',
    actor: 'Deniz Kaya',
    title: t('customer.a3.title'),
    detail: t('customer.a3.detail'),
    at: minutesAgo(52),
    duration: t('customer.a3.duration'),
    direction: 'out',
  },
  {
    id: 'a-4',
    type: 'email',
    actor: 'Selin Arslan',
    title: t('customer.a4.title'),
    detail: t('customer.a4.detail'),
    at: minutesAgo(190),
    direction: 'out',
    attachments: ['Nordwind-teklif-v3.pdf', 'TCO-karşılaştırma.xlsx'],
  },
  {
    id: 'a-5',
    type: 'document',
    actor: t('activityType.system'),
    title: t('customer.a5.title'),
    detail: t('customer.a5.detail'),
    at: minutesAgo(240),
  },
  {
    id: 'a-6',
    type: 'meeting',
    actor: 'Deniz Kaya',
    title: t('customer.a6.title'),
    detail: t('customer.a6.detail'),
    at: minutesAgo(1_580),
    duration: t('customer.a6.duration'),
  },
  {
    id: 'a-7',
    type: 'task',
    actor: 'Jonas Weber',
    title: t('customer.a7.title'),
    detail: t('customer.a7.detail'),
    at: minutesAgo(1_720),
  },
  {
    id: 'a-8',
    type: 'call',
    actor: t('activityType.system'),
    title: t('customer.a8.title'),
    detail: t('customer.a8.detail'),
    at: minutesAgo(2_960),
    direction: 'in',
  },
  {
    id: 'a-9',
    type: 'stage',
    actor: 'Selin Arslan',
    title: t('customer.a9.title'),
    at: minutesAgo(4_310),
    change: { field: t('customer.a9.change.field'), from: t('customer.a9.change.from'), to: t('customer.a9.change.to') },
  },
  {
    id: 'a-10',
    type: 'email',
    actor: 'Selin Arslan',
    title: t('customer.a10.title'),
    at: minutesAgo(4_400),
    direction: 'out',
    attachments: ['Nordwind-teklif-v1.pdf'],
  },
]
