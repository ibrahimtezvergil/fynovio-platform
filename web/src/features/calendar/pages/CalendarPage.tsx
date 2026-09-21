import { useCalendarController } from '@fullcalendar/react'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { PageHeader } from '@/components/common/PageHeader'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { toVisibleRange, useCalendarEntries, useMoveCalendarEntry, type VisibleRange } from '@/features/calendar/api'
import { CalendarBoard } from '@/features/calendar/components/CalendarBoard'
import { CalendarToolbar } from '@/features/calendar/components/CalendarToolbar'
import { EntryDialog } from '@/features/calendar/components/EntryDialog'
import type { EntryPrefill } from '@/features/calendar/components/EntryForm'
import { ProblemNotice } from '@/features/calendar/components/ProblemNotice'
import { parseLinkParam } from '@/features/calendar/lib/links'
import { toProblem } from '@/features/calendar/lib/problem'
import { defaultFields, fieldsFromRange, type DraggedRange } from '@/features/calendar/lib/time'
import type { CalendarEntry } from '@/features/calendar/schema'
import type { CalendarView } from '@/features/calendar/types'
import { openDialog } from '@/lib/overlay'
import { paths } from '@/routes/paths'
import type { ApiError } from '@/types'

const NO_ENTRIES: CalendarEntry[] = []
const DIALOG_CLASS = 'sm:max-w-[560px]'

function openEntryDialog(entry?: CalendarEntry, prefill?: EntryPrefill) {
  void openDialog({
    content: <EntryDialog entry={entry} prefill={prefill} />,
    className: entry ? `${DIALOG_CLASS} [&_[data-slot=dialog-close]]:hidden` : DIALOG_CLASS,
  })
}

export default function CalendarPage() {
  const { t } = useTranslation('calendar')
  const navigate = useNavigate()
  const location = useLocation()
  const [searchParams, setSearchParams] = useSearchParams()

  // v7's controller replaces `ref.getApi()`: it re-renders this page on every
  // `datesSet`, which is what keeps the toolbar's title in sync.
  const controller = useCalendarController()
  const [view, setView] = useState<CalendarView>('dayGridMonth')
  // `null` until the grid reports its first visible window: nothing is requested before then.
  const [range, setRange] = useState<VisibleRange | null>(null)

  const query = useCalendarEntries(range)
  const moveEntry = useMoveCalendarEntry()
  const entries = query.data ?? NO_ENTRIES
  const problem = useMemo(() => (query.error ? toProblem(query.error as unknown as ApiError) : null), [query.error])

  // `location.key` is `'default'` only for the tab's very first history entry
  // (a direct URL load or refresh) — there is nothing in-app to go back to,
  // so fall back to the dashboard instead of navigating the browser away.
  const goBack = () => {
    if (location.key === 'default') navigate(paths.dashboard)
    else navigate(-1)
  }

  const onDatesSet = (start: Date, end: Date) => {
    const next = toVisibleRange(start, end)
    setRange((current) => (current?.from === next.from && current.to === next.to ? current : next))
  }

  // "Add to calendar" from a record arrives as `?link=crm/opportunity/17`: open the create dialog with that link, then
  // drop the parameter so a refresh or Back does not reopen it. The ref keeps StrictMode's double effect to one dialog.
  const openedFromLink = useRef(false)
  useEffect(() => {
    const raw = searchParams.get('link')
    if (raw === null) {
      openedFromLink.current = false
      return
    }
    setSearchParams(
      (params) => {
        params.delete('link')
        return params
      },
      { replace: true },
    )
    const link = parseLinkParam(raw)
    if (link && !openedFromLink.current) {
      openedFromLink.current = true
      openEntryDialog(undefined, { link, fields: defaultFields() })
    }
  }, [searchParams, setSearchParams])

  return (
    <div className="mx-auto flex w-full max-w-[1320px] flex-col gap-5">
      <PageHeader
        eyebrow={t('page.eyebrow')}
        title={t('page.title')}
        description={t('page.description')}
        actions={<Badge variant="secondary">{t('page.recordCount', { count: entries.length })}</Badge>}
        onBack={goBack}
        backLabel={t('page.back')}
      />

      <Card className="gap-0 px-4 pt-4 pb-4">
        <CalendarToolbar
          controller={controller}
          view={view}
          onViewChange={setView}
          onCreate={() => openEntryDialog(undefined, { fields: defaultFields() })}
        />

        {problem && <ProblemNotice problem={problem} onRetry={() => void query.refetch()} className="mb-3" />}

        <p id="calendar-keyboard-hint" className="sr-only">
          {t('page.keyboardHint')}
        </p>
        <div
          aria-busy={query.isFetching}
          aria-describedby="calendar-keyboard-hint"
          className={query.isPlaceholderData ? 'opacity-60 transition-opacity' : 'transition-opacity'}
        >
          {/* The grid is always mounted: its first `datesSet` is what starts the first request. */}
          {range === null && <Skeleton className="nx-skeleton mb-3 h-2 w-full rounded-full" />}
          <CalendarBoard
            entries={entries}
            view={view}
            controller={controller}
            onDatesSet={onDatesSet}
            onSelectEntry={(id) => {
              const entry = entries.find((candidate) => candidate.id === id)
              if (entry) openEntryDialog(entry)
            }}
            onSelectRange={(selected: DraggedRange) => openEntryDialog(undefined, { fields: fieldsFromRange(selected) })}
            onMoveEntry={(id, moved) => void moveEntry(id, moved)}
          />
        </div>
      </Card>
    </div>
  )
}
