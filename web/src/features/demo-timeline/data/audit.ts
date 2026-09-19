import { i18n } from '@/lib/i18n'
import { minutesAgo } from '@/lib/datetime'
import type { TimelineEntry } from '@/features/demo-timeline/types'

const t = (key: string) => i18n.t(key, { ns: 'demo-timeline' })

/**
 * The ERP shape: an audit trail. Almost every line is a field change, so the
 * `from → to` pair carries the content and the prose stays out of the way.
 *
 * Resolved against the active language at import time — see `stageMeta()` in
 * `StageBadge.tsx` for why this module doesn't switch live.
 */
export const AUDIT_FEED: TimelineEntry[] = [
  {
    id: 'u-1',
    type: 'payment',
    actor: t('activityType.system'),
    title: t('audit.u1.title'),
    detail: t('audit.u1.detail'),
    at: minutesAgo(21),
    change: { field: t('audit.u1.change.field'), from: t('audit.u1.change.from'), to: t('audit.u1.change.to') },
  },
  {
    id: 'u-2',
    type: 'system',
    actor: 'Onur Şahin',
    title: t('audit.u2.title'),
    detail: t('audit.u2.detail'),
    at: minutesAgo(95),
    change: { field: t('audit.u2.change.field'), from: t('audit.u2.change.from'), to: t('audit.u2.change.to') },
  },
  {
    id: 'u-3',
    type: 'document',
    actor: 'Burak Demir',
    title: t('audit.u3.title'),
    detail: t('audit.u3.detail'),
    at: minutesAgo(160),
    attachments: ['IR-2026-3312.pdf'],
  },
  {
    id: 'u-4',
    type: 'system',
    actor: 'Ece Yıldırım',
    title: t('audit.u4.title'),
    at: minutesAgo(1_340),
    change: { field: t('audit.u4.change.field'), from: t('audit.u4.change.from'), to: t('audit.u4.change.to') },
  },
  {
    id: 'u-5',
    type: 'task',
    actor: 'Onur Şahin',
    title: t('audit.u5.title'),
    detail: t('audit.u5.detail'),
    at: minutesAgo(1_520),
    change: { field: t('audit.u5.change.field'), from: t('audit.u5.change.from'), to: t('audit.u5.change.to') },
  },
  {
    id: 'u-6',
    type: 'system',
    actor: t('activityType.system'),
    title: t('audit.u6.title'),
    detail: t('audit.u6.detail'),
    at: minutesAgo(2_880),
    change: { field: t('audit.u6.change.field'), from: t('audit.u6.change.from'), to: t('audit.u6.change.to') },
  },
]
