import FullCalendar, { type CalendarController, type EventInput } from '@fullcalendar/react'
import dayGridPlugin from '@fullcalendar/react/daygrid'
import interactionPlugin from '@fullcalendar/react/interaction'
import listPlugin from '@fullcalendar/react/list'
import trLocale from '@fullcalendar/react/locales/tr'
import classicThemePlugin from '@fullcalendar/react/themes/classic'
import timeGridPlugin from '@fullcalendar/react/timegrid'
import { CalendarDays } from 'lucide-react'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { useResolvedTheme } from '@/store/useAppStore'
import { calendarHeaderPalette } from '../lib/contrast'
import type { DraggedRange } from '../lib/time'
import type { CalendarEntry } from '../schema'
import type { CalendarView } from '../types'

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
  entries: CalendarEntry[]
  view: CalendarView
  controller: CalendarController
  /** The visible window changed (first render, prev/next, view switch). `end` is exclusive. */
  onDatesSet: (start: Date, end: Date) => void
  onSelectEntry: (id: number) => void
  onSelectRange: (range: DraggedRange) => void
  onMoveEntry: (id: number, range: DraggedRange) => void
}

/** FullCalendar's event → the range the API layer converts to wire timing. `end` is exclusive, `null` = no duration. */
const rangeOf = (event: { start: Date | null; end: Date | null; allDay: boolean }): DraggedRange => ({
  start: event.start ?? new Date(),
  end: event.end,
  allDay: event.allDay,
})

/**
 * The single place FullCalendar is configured.
 *
 * Everything visual comes from two seams and no CSS overrides: the palette in `src/styles/fullcalendar.css`, and the
 * per-event `color` / `contrastColor` below. The theme plugin owns every `*Class` option — passing our own would
 * *replace* the theme's classes rather than add to them, so we don't; the root `class` option is the one slot the theme
 * leaves free.
 */
export function CalendarBoard({
  entries,
  view,
  controller,
  onDatesSet,
  onSelectEntry,
  onSelectRange,
  onMoveEntry,
}: CalendarBoardProps) {
  const { t, i18n } = useTranslation('calendar')
  const theme = useResolvedTheme()

  const fcEvents = useMemo<EventInput[]>(
    () =>
      entries.map((entry) => {
        const palette = calendarHeaderPalette(entry.color)

        return {
          id: String(entry.id),
          title: entry.title,
          start: (entry.allDay ? entry.startDate : entry.startAt) ?? undefined,
          end: (entry.allDay ? entry.endDate : entry.endAt) ?? undefined,
          allDay: entry.allDay,
          // Coloured event cards and their detail headers use one rule: white text. Light user-picked colours are
          // darkened only as far as needed for AA contrast, so the calendar stays consistent without losing the hue.
          color: palette.middle,
          contrastColor: palette.textColor,
        }
      }),
    [entries],
  )

  return (
    <FullCalendar
      plugins={PLUGINS}
      controller={controller}
      /* `class`, not `className` — the classic theme owns `className`. */
      class="fc-fynovio"
      locale={i18n.language.startsWith('tr') ? trLocale : undefined}
      colorScheme={theme}
      initialView={view}
      headerToolbar={false}
      borderless
      height={700}
      expandRows
      events={fcEvents}
      datesSet={(info) => onDatesSet(info.start, info.end)}
      /* --- month grid: dot + time + title rather than filled blocks, so a
         dense month stays readable and the hue never has to carry text --- */
      views={{
        dayGridMonth: { eventDisplay: 'list-item', dayMaxEvents: 4 },
        listWeek: { listDayFormat: { weekday: 'long', day: 'numeric', month: 'long' } },
      }}
      /* --- event chrome. The colour is user-selected, so a very light or very dark pick can vanish into the surface:
         a hairline ring in the foreground colour keeps every dot and bar visible in both themes, and titles end in an
         ellipsis instead of being cut mid-glyph. --- */
      listItemEventBeforeClass="ring-foreground/30 ring-1"
      listItemEventTitleClass="min-w-0 truncate"
      rowEventClass="ring-foreground/25 ring-1 ring-inset"
      rowEventTitleClass="min-w-0 truncate"
      blockEventClass="ring-foreground/25 ring-1 ring-inset"
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
      /* Keyboard: entries take focus, Enter opens the dialog (which edits every field), so nothing is drag-only. */
      eventInteractive
      eventClick={(info) => onSelectEntry(Number(info.event.id))}
      select={(info) => onSelectRange({ start: info.start, end: info.end, allDay: info.allDay })}
      eventDrop={(info) => onMoveEntry(Number(info.event.id), rangeOf(info.event))}
      eventResize={(info) => onMoveEntry(Number(info.event.id), rangeOf(info.event))}
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
