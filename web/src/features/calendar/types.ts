import type { LucideIcon } from 'lucide-react'
import { CalendarClock, CheckSquare, Flag, Phone, Plane } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import type { StatusTone } from '@/components/common/StatusBadge'

/** The four calendar views this app exposes, in toolbar order. */
export const CALENDAR_VIEWS = ['dayGridMonth', 'timeGridWeek', 'timeGridDay', 'listWeek'] as const
export type CalendarView = (typeof CALENDAR_VIEWS)[number]

/**
 * What kind of thing sits on the calendar. Deliberately generic: a clinic reads
 * `meeting` as an appointment, a workshop as a job. Renaming the labels below is
 * the whole vertical customisation — nothing else in the feature knows the words.
 */
export const EVENT_KINDS = ['meeting', 'call', 'task', 'deadline', 'away'] as const
export type EventKind = (typeof EVENT_KINDS)[number]

export interface CalendarEvent {
  id: string
  title: string
  /** ISO 8601. Local time for timed entries, `YYYY-MM-DD` for all-day ones. */
  start: string
  /** Exclusive, as everywhere in FullCalendar. `null` means "no duration yet". */
  end: string | null
  allDay: boolean
  kind: EventKind
  owner: string
  location?: string
  account?: string
  notes?: string
}

interface KindMeta {
  label: string
  tone: StatusTone
  icon: LucideIcon
  /** The colour handed to FullCalendar. One of the `--color-tone-*` mappings. */
  color: string
}

type KindStyle = Omit<KindMeta, 'label'>

/**
 * One vocabulary, one place. The tone drives the pill, the dot and the event
 * fill at once, so a "call" is the same teal in the badge, the month dot and the
 * week block — and never *only* the colour: every surface prints the label too.
 * Label lives in the `calendar` catalog; everything else here is not translatable.
 */
export const KIND_STYLE: Record<EventKind, KindStyle> = {
  meeting: { tone: 'purple', icon: CalendarClock, color: 'var(--color-tone-purple)' },
  call: { tone: 'teal', icon: Phone, color: 'var(--color-tone-teal)' },
  task: { tone: 'blue', icon: CheckSquare, color: 'var(--color-tone-blue)' },
  deadline: { tone: 'red', icon: Flag, color: 'var(--color-tone-red)' },
  away: { tone: 'gray', icon: Plane, color: 'var(--color-tone-gray)' },
}

/** The reactive form — re-renders when the language changes. */
export function useKindMeta(): Record<EventKind, KindMeta> {
  const { t } = useTranslation('calendar')
  return (Object.keys(KIND_STYLE) as EventKind[]).reduce(
    (meta, kind) => {
      meta[kind] = { ...KIND_STYLE[kind], label: t(`kind.${kind}`) }
      return meta
    },
    {} as Record<EventKind, KindMeta>,
  )
}
