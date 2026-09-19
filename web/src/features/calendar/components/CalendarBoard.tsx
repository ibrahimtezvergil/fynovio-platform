import FullCalendar, { type CalendarController, type EventInput } from '@fullcalendar/react'
import dayGridPlugin from '@fullcalendar/react/daygrid'
import interactionPlugin from '@fullcalendar/react/interaction'
import listPlugin from '@fullcalendar/react/list'
import trLocale from '@fullcalendar/react/locales/tr'
import classicThemePlugin from '@fullcalendar/react/themes/classic'
import timeGridPlugin from '@fullcalendar/react/timegrid'
import { format } from 'date-fns'
import { CalendarDays } from 'lucide-react'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { useResolvedTheme } from '@/store/useAppStore'
import { KIND_STYLE, type CalendarEvent, type CalendarView } from '@/features/calendar/types'

const PLUGINS = [
  dayGridPlugin,
  timeGridPlugin,
  listPlugin,
  interactionPlugin,
  classicThemePlugin,
]

const BUSINESS_HOURS = {
  daysOfWeek: [1, 2, 3, 4, 5],
  startTime: '09:00',
  endTime: '18:00',
}

/** 24h, zero-padded — the same digits the rest of the app prints. */
const TIME_FORMAT = { hour: '2-digit', minute: '2-digit', hour12: false } as const

interface CalendarBoardProps {
  events: CalendarEvent[]
  view: CalendarView
  controller: CalendarController
  onSelectEvent: (id: string) => void
  onSelectRange: (startIso: string, endIso: string, allDay: boolean) => void
  onMoveEvent: (
    id: string,
    patch: { start: string; end: string | null; allDay: boolean },
  ) => void
}

/**
 * Local wall-clock, never UTC: the mock store keeps `2026-09-08T14:00:00`, and
 * `toISOString()` would silently shift every dragged event by the UTC offset.
 */
const toLocalIso = (date: Date, allDay: boolean) =>
  format(date, allDay ? 'yyyy-MM-dd' : "yyyy-MM-dd'T'HH:mm:ss")

/** The shape both drag and resize report back. */
function movedTo(event: { start: Date | null; end: Date | null; allDay: boolean }) {
  return {
    start: toLocalIso(event.start ?? new Date(), event.allDay),
    end: event.end ? toLocalIso(event.end, event.allDay) : null,
    allDay: event.allDay,
  }
}

/**
 * The single place FullCalendar is configured.
 *
 * Everything visual comes from two seams and no CSS overrides: the palette in
 * `src/styles/fullcalendar.css`, and the per-event `color` / `contrastColor`
 * below. The theme plugin owns every `*Class` option — passing our own would
 * *replace* the theme's classes rather than add to them, so we don't; the root
 * `class` option is the one slot the theme leaves free.
 */
export function CalendarBoard({
  events,
  view,
  controller,
  onSelectEvent,
  onSelectRange,
  onMoveEvent,
}: CalendarBoardProps) {
  const { t } = useTranslation('calendar')
  const theme = useResolvedTheme()

  const fcEvents = useMemo<EventInput[]>(
    () =>
      events.map((event) => ({
        id: event.id,
        title: event.title,
        start: event.start,
        end: event.end ?? undefined,
        allDay: event.allDay,
        // The hue is a token, resolved by the browser — so it re-resolves on
        // theme change without this component re-mapping anything.
        color: KIND_STYLE[event.kind].color,
        // Every tone in the ladder is a *text* colour, picked to clear 6:1
        // against the page ground; inverting to that ground is therefore
        // legible on the fill in both themes.
        contrastColor: 'var(--background)',
        extendedProps: { kind: event.kind, owner: event.owner },
      })),
    [events],
  )

  return (
    <FullCalendar
      plugins={PLUGINS}
      controller={controller}
      /* `class`, not `className` — the classic theme owns `className`. */
      class="fc-fynovio"
      locale={trLocale}
      colorScheme={theme}
      initialView={view}
      headerToolbar={false}
      borderless
      height={700}
      expandRows
      events={fcEvents}
      /* --- month grid: dot + time + title rather than filled blocks, so a
         dense month stays readable and the hue never has to carry text --- */
      views={{
        dayGridMonth: { eventDisplay: 'list-item', dayMaxEvents: 4 },
        listWeek: { listDayFormat: { weekday: 'long', day: 'numeric', month: 'long' } },
      }}
      /* --- time grid --- */
      nowIndicator
      slotMinTime="07:00:00"
      slotMaxTime="21:00:00"
      slotDuration="00:30:00"
      scrollTime="08:00:00"
      businessHours={BUSINESS_HOURS}
      allDayText={t('allDay')}
      eventTimeFormat={TIME_FORMAT}
      slotHeaderFormat={TIME_FORMAT}
      /* --- interaction --- */
      editable
      selectable
      selectMirror
      eventClick={(info) => onSelectEvent(info.event.id)}
      select={(info) =>
        onSelectRange(
          toLocalIso(info.start, info.allDay),
          toLocalIso(info.end, info.allDay),
          info.allDay,
        )
      }
      eventDrop={(info) => onMoveEvent(info.event.id, movedTo(info.event))}
      eventResize={(info) => onMoveEvent(info.event.id, movedTo(info.event))}
      /* The agenda view's own empty state, in our vocabulary. Content hooks are
         free — the theme only claims the `*Class` ones. */
      noEventsContent={() => (
        <div className="text-muted-foreground flex flex-col items-center gap-2 py-10">
          <CalendarDays className="size-5 opacity-60" strokeWidth={1.75} />
          <p className="text-[13px]">{t('emptyRange')}</p>
        </div>
      )}
    />
  )
}
