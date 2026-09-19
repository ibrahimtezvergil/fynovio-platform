import type { CalendarController } from '@fullcalendar/react'
import { format } from 'date-fns'
import { tr } from 'date-fns/locale'
import { ChevronLeft, ChevronRight, Plus } from 'lucide-react'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { SegmentedControl, type Segment } from '@/components/common/SegmentedControl'
import { Button } from '@/components/ui/button'
import type { CalendarView } from '@/features/calendar/types'

interface CalendarToolbarProps {
  controller: CalendarController
  view: CalendarView
  onViewChange: (view: CalendarView) => void
  onCreate: () => void
}

/**
 * FullCalendar's own toolbar is switched off (`headerToolbar={false}`) and
 * rebuilt here out of our own primitives — that is the whole reason the header
 * matches every other page instead of looking like a third-party widget. The
 * calendar is driven through the controller, so nothing here touches its DOM.
 */
export function CalendarToolbar({
  controller,
  view,
  onViewChange,
  onCreate,
}: CalendarToolbarProps) {
  const { t } = useTranslation('calendar')
  const viewSegments: readonly Segment<CalendarView>[] = useMemo(
    () => [
      { value: 'dayGridMonth', label: t('view.month') },
      { value: 'timeGridWeek', label: t('view.week') },
      { value: 'timeGridDay', label: t('view.day') },
      { value: 'listWeek', label: t('view.agenda') },
    ],
    [t],
  )
  // Empty until the calendar's first `datesSet`; the fallback keeps the header
  // from rendering a blank line on the very first paint.
  const title = controller.view?.title ?? format(new Date(), 'LLLL yyyy', { locale: tr })

  const changeView = (next: CalendarView) => {
    onViewChange(next)
    controller.changeView(next)
  }

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 px-1 pb-4">
      <div className="flex min-w-0 items-center gap-3">
        <div className="flex items-center gap-1">
          <Button
            variant="outline"
            size="icon-sm"
            onClick={() => controller.prev()}
            aria-label={t('prevPeriod')}
          >
            <ChevronLeft />
          </Button>
          <Button
            variant="outline"
            size="icon-sm"
            onClick={() => controller.next()}
            aria-label={t('nextPeriod')}
          >
            <ChevronRight />
          </Button>
        </div>

        <Button variant="outline" size="sm" onClick={() => controller.today()}>
          {t('today')}
        </Button>

        {/* aria-live: the title is the only thing that tells a screen-reader
            user that prev/next actually moved the calendar. */}
        <h2
          aria-live="polite"
          className="font-heading min-w-0 truncate text-[17px] leading-tight font-[620] tracking-[-0.024em] capitalize"
        >
          {title}
        </h2>
      </div>

      <div className="flex items-center gap-2.5">
        <SegmentedControl
          segments={viewSegments}
          value={view}
          onChange={changeView}
          aria-label={t('viewLabel')}
        />
        <Button size="sm" onClick={onCreate}>
          <Plus />
          {t('createEvent')}
        </Button>
      </div>
    </div>
  )
}
