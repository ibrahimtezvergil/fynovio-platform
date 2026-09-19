import type { LucideIcon } from 'lucide-react'
import { AtSign, BellRing, CircleAlert, ClipboardCheck, ShieldCheck } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import type { StatusTone } from '@/components/common/StatusBadge'
import { i18n } from '@/lib/i18n'

/**
 * Why the bell rang. The kind decides the tile's icon and tone, and it is the
 * axis the center's filter works on — a reader who wants "what needs me" is
 * asking for `task` and `approval`, not for a colour.
 */
export type NotificationKind = 'system' | 'task' | 'reminder' | 'mention' | 'approval'

export interface AppNotification {
  id: string
  kind: NotificationKind
  title: string
  body: string
  /** ISO timestamp. Rendered relative — "3 dakika önce". */
  at: string
  read: boolean
  /** Who caused it. Absent for events the system raised on its own. */
  actor?: string
  /** Two inline buttons instead of a chevron: the row is a decision, not a link. */
  decision?: { accept: string; reject: string }
}

const KIND_ICON: Record<NotificationKind, LucideIcon> = {
  system: CircleAlert,
  task: ClipboardCheck,
  reminder: BellRing,
  mention: AtSign,
  approval: ShieldCheck,
}

const KIND_TONE: Record<NotificationKind, Exclude<StatusTone, 'outline'>> = {
  system: 'red',
  task: 'blue',
  reminder: 'amber',
  mention: 'purple',
  approval: 'teal',
}

type KindMeta = Record<NotificationKind, { label: string; icon: LucideIcon; tone: Exclude<StatusTone, 'outline'> }>

/** Resolved against whichever language is active at call time — see `useKindMeta` for the reactive form. */
export function kindMeta(): KindMeta {
  return (Object.keys(KIND_ICON) as NotificationKind[]).reduce((meta, kind) => {
    meta[kind] = {
      label: i18n.t(`kind.${kind}`, { ns: 'notifications' }),
      icon: KIND_ICON[kind],
      tone: KIND_TONE[kind],
    }
    return meta
  }, {} as KindMeta)
}

export function useKindMeta(): KindMeta {
  const { t } = useTranslation('notifications')
  return (Object.keys(KIND_ICON) as NotificationKind[]).reduce((meta, kind) => {
    meta[kind] = { label: t(`kind.${kind}`), icon: KIND_ICON[kind], tone: KIND_TONE[kind] }
    return meta
  }, {} as KindMeta)
}

/** The center's filter tabs. `unread` cuts across every kind. */
export type NotificationFilter = 'all' | 'unread' | 'task' | 'system'

const FILTER_KEYS: Record<NotificationFilter, string> = {
  all: 'all',
  unread: 'unread',
  task: 'mine',
  system: 'system',
}

export function useFilterLabels(): Record<NotificationFilter, string> {
  const { t } = useTranslation('notifications')
  return (Object.keys(FILTER_KEYS) as NotificationFilter[]).reduce(
    (labels, filter) => {
      labels[filter] = t(`filter.${FILTER_KEYS[filter]}`)
      return labels
    },
    {} as Record<NotificationFilter, string>,
  )
}

/** `task` folds in approvals and mentions — everything waiting on this user. */
export function matchesFilter(item: AppNotification, filter: NotificationFilter): boolean {
  switch (filter) {
    case 'all':
      return true
    case 'unread':
      return !item.read
    case 'task':
      return item.kind === 'task' || item.kind === 'approval' || item.kind === 'mention'
    case 'system':
      return item.kind === 'system' || item.kind === 'reminder'
  }
}
