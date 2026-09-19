import type { LucideIcon } from 'lucide-react'
import {
  ArrowLeftRight,
  Banknote,
  ClipboardCheck,
  FileCheck,
  FileText,
  Handshake,
  Mail,
  PhoneCall,
  Settings2,
} from 'lucide-react'
import { useTranslation } from 'react-i18next'
import type { StatusTone } from '@/components/common/StatusBadge'

/**
 * What happened. The type picks the icon and the tile's tone, and it is the
 * axis the feed filters on — "calls only" is a question about type, and
 * no other property answers it.
 */
export type ActivityType =
  | 'call'
  | 'email'
  | 'meeting'
  | 'note'
  | 'stage'
  | 'document'
  | 'payment'
  | 'task'
  | 'system'

const ACTIVITY_TONE: Record<ActivityType, Exclude<StatusTone, 'outline'>> = {
  call: 'blue',
  email: 'purple',
  meeting: 'teal',
  note: 'gray',
  stage: 'amber',
  document: 'blue',
  payment: 'green',
  task: 'purple',
  system: 'gray',
}

const ACTIVITY_ICON: Record<ActivityType, LucideIcon> = {
  call: PhoneCall,
  email: Mail,
  meeting: Handshake,
  note: FileText,
  stage: ArrowLeftRight,
  document: FileCheck,
  payment: Banknote,
  task: ClipboardCheck,
  system: Settings2,
}

export interface ActivityMeta {
  label: string
  icon: LucideIcon
  tone: Exclude<StatusTone, 'outline'>
}

/** The reactive activity-type registry — re-renders when the language changes. */
export function useActivityMeta(): Record<ActivityType, ActivityMeta> {
  const { t } = useTranslation('demo-timeline')
  return (Object.keys(ACTIVITY_TONE) as ActivityType[]).reduce((meta, type) => {
    meta[type] = { label: t(`activityType.${type}`), icon: ACTIVITY_ICON[type], tone: ACTIVITY_TONE[type] }
    return meta
  }, {} as Record<ActivityType, ActivityMeta>)
}

export interface TimelineEntry {
  id: string
  type: ActivityType
  /** Who did it. The "system" activity label is used for anything no person triggered. */
  actor: string
  title: string
  detail?: string
  /** ISO timestamp. The feed is always newest first. */
  at: string
  /**
   * A field that changed. Rendered as `from → to` rather than as a sentence:
   * an audit line has to be scannable down a column, and prose isn't.
   */
  change?: { field: string; from: string; to: string }
  attachments?: string[]
  /** Calls and meetings only. */
  duration?: string
  /** Inbound (customer reached us) or outbound (we reached them). */
  direction?: 'in' | 'out'
  /** Held at the top of the feed, above the day groups. */
  pinned?: boolean
}
