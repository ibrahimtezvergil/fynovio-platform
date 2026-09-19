import { format, isSameDay } from 'date-fns'
import { tr } from 'date-fns/locale'
import { Building2, Clock, MapPin, User } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { StatusBadge } from '@/components/common/StatusBadge'
import { Button } from '@/components/ui/button'
import {
  DialogClose,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { useKindMeta, type CalendarEvent } from '@/features/calendar/types'

interface EventDetailDialogProps { event: CalendarEvent }

/** "8 Eylül, 14:00 – 15:30" · "8 – 11 Eylül" for all-day spans. */
function formatWhen(event: CalendarEvent): string {
  const start = new Date(event.start)
  const end = event.end ? new Date(event.end) : null

  if (event.allDay) {
    // FullCalendar's all-day end is exclusive; the label has to be inclusive.
    const lastDay = end ? new Date(end.getTime() - 86_400_000) : start
    if (isSameDay(start, lastDay)) return format(start, 'd MMMM EEEE', { locale: tr })
    return `${format(start, 'd MMMM', { locale: tr })} – ${format(lastDay, 'd MMMM', { locale: tr })}`
  }

  const day = format(start, 'd MMMM EEEE', { locale: tr })
  if (!end) return `${day}, ${format(start, 'HH:mm')}`
  return `${day}, ${format(start, 'HH:mm')} – ${format(end, 'HH:mm')}`
}

function DetailRow({ icon: Icon, children }: { icon: LucideIcon; children: string }) {
  return (
    <div className="flex items-center gap-2.5">
      <Icon className="text-muted-foreground size-3.5 shrink-0" strokeWidth={1.75} />
      <span className="text-[13px]">{children}</span>
    </div>
  )
}

/**
 * The record behind a block on the grid. The kind is repeated here as a labelled
 * badge — on the calendar itself it is only a hue, and a hue alone never carries
 * meaning (WCAG 1.4.1).
 */
export function EventDetailDialog({ event }: EventDetailDialogProps) {
  const { t } = useTranslation('calendar')
  const kindMeta = useKindMeta()
  const meta = kindMeta[event.kind]

  return (
    <>
            <DialogHeader>
              {/* self-start: DialogHeader is a stretching flex column and would
                  otherwise pull the pill to the full dialog width. */}
              <StatusBadge
                label={meta.label}
                tone={meta.tone}
                icon={meta.icon}
                size="sm"
                className="self-start"
              />
              <DialogTitle className="pt-1.5 pr-8">{event.title}</DialogTitle>
              <DialogDescription>{formatWhen(event)}</DialogDescription>
            </DialogHeader>

            <div className="flex flex-col gap-2.5">
              <DetailRow icon={Clock}>
                {event.allDay
                  ? t('allDay')
                  : t('startsAt', { time: format(new Date(event.start), 'HH:mm') })}
              </DetailRow>
              <DetailRow icon={User}>{event.owner}</DetailRow>
              {event.account && <DetailRow icon={Building2}>{event.account}</DetailRow>}
              {event.location && <DetailRow icon={MapPin}>{event.location}</DetailRow>}
            </div>

            {event.notes && (
              <p className="text-muted-foreground border-border border-t pt-3.5 text-[12.5px] leading-[1.55]">
                {event.notes}
              </p>
            )}

            <DialogFooter>
              <DialogClose render={<Button variant="outline" size="sm" />}>{t('close')}</DialogClose>
              <Button size="sm">{t('openRecord')}</Button>
            </DialogFooter>
    </>
  )
}
