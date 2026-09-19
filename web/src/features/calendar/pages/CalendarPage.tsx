import { useCalendarController } from '@fullcalendar/react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useLocation, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { PageHeader } from '@/components/common/PageHeader'
import { StatusBadge } from '@/components/common/StatusBadge'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useCalendarEvents, useMoveCalendarEvent } from '@/features/calendar/api'
import { CalendarBoard } from '@/features/calendar/components/CalendarBoard'
import { CalendarToolbar } from '@/features/calendar/components/CalendarToolbar'
import { EventDetailDialog } from '@/features/calendar/components/EventDetailDialog'
import { openDialog } from '@/lib/overlay'
import { paths } from '@/routes/paths'
import { EVENT_KINDS, useKindMeta, type CalendarView } from '@/features/calendar/types'

export default function CalendarPage() {
  const { t } = useTranslation('calendar')
  const kindMeta = useKindMeta()
  const { data, isPending } = useCalendarEvents()
  const moveEvent = useMoveCalendarEvent()
  const navigate = useNavigate()
  const location = useLocation()

  // `location.key` is `'default'` only for the tab's very first history entry
  // (a direct URL load or refresh) — there is nothing in-app to go back to,
  // so fall back to the dashboard instead of navigating the browser away.
  const goBack = () => {
    if (location.key === 'default') navigate(paths.dashboard)
    else navigate(-1)
  }

  // v7's controller replaces `ref.getApi()`: it re-renders this page on every
  // `datesSet`, which is what keeps the toolbar's title in sync.
  const controller = useCalendarController()
  const [view, setView] = useState<CalendarView>('dayGridMonth')

  const events = useMemo(() => data ?? [], [data])

  return (
    <div className="mx-auto flex w-full max-w-[1320px] flex-col gap-5">
      <PageHeader
        eyebrow={t('page.eyebrow')}
        title={t('page.title')}
        description={t('page.description')}
        actions={<Badge variant="secondary">{t('page.recordCount', { count: events.length })}</Badge>}
        onBack={goBack}
        backLabel={t('page.back')}
      />

      <Card className="gap-0 px-4 pt-4 pb-4">
        <CalendarToolbar
          controller={controller}
          view={view}
          onViewChange={setView}
          onCreate={() =>
            toast(t('page.createToast'), { description: t('page.createToastDescription') })
          }
        />

        {isPending ? (
          <Skeleton className="nx-skeleton h-[700px] w-full rounded-xl" />
        ) : (
          <CalendarBoard
            events={events}
            view={view}
            controller={controller}
            onSelectEvent={(id) => {
              const event = events.find((candidate) => candidate.id === id)
              if (event) void openDialog({ content: <EventDetailDialog event={event} />, className: 'sm:max-w-[440px]' })
            }}
            onSelectRange={(start, _end, allDay) =>
              toast(t('page.newRangeToast'), {
                description: allDay
                  ? t('page.newRangeAllDay', { start })
                  : start.replace('T', ' '),
              })
            }
            onMoveEvent={(id, patch) => moveEvent.mutate({ id, patch })}
          />
        )}
      </Card>

      {/* The grid encodes kind as a hue; this is where the hue gets its name.
          Colour alone never carries meaning — the same rule the pipeline
          StageBadge follows. */}
      <Card className="flex-row flex-wrap items-center gap-x-4 gap-y-2.5 px-5 py-4">
        <span className="nx-eyebrow">{t('page.eventKindsHeading')}</span>
        {EVENT_KINDS.map((kind) => {
          const meta = kindMeta[kind]
          return (
            <StatusBadge
              key={kind}
              label={meta.label}
              tone={meta.tone}
              icon={meta.icon}
              size="sm"
            />
          )
        })}
      </Card>

    </div>
  )
}
