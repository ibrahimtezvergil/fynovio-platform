import { CalendarRange } from 'lucide-react'
import { useState } from 'react'
import type { DateRange } from 'react-day-picker'
import { useTranslation } from 'react-i18next'
import {
  DatePicker,
  DateRangePicker,
  DurationInput,
  QuarterPicker,
  TimeRangeInput,
  formatDate,
  formatDuration,
  quarterRange,
  timeRangeMinutes,
  type QuarterValue,
  type TimeRange,
} from '@/components/common/inputs'
import { Input } from '@/components/ui/input'
import { ControlDemo, show } from '@/features/demo-forms/components/ControlDemo'
import { DemoSection } from '@/components/common/DemoSection'

export function DateSection() {
  const { t } = useTranslation('demo-forms')
  const [orderDate, setOrderDate] = useState<Date | null>(new Date())
  const [deliveryDate, setDeliveryDate] = useState<Date | null>(null)
  const [period, setPeriod] = useState<DateRange | null>(null)
  const [time, setTime] = useState('09:30')
  const [meeting, setMeeting] = useState('')
  const [accountingMonth, setAccountingMonth] = useState('')
  const [shift, setShift] = useState<TimeRange>({ start: '09:00', end: '18:00' })
  const [duration, setDuration] = useState<number | null>(150)
  const [quarter, setQuarter] = useState<QuarterValue>({ year: 2026, quarter: 3 })
  const [week, setWeek] = useState('')

  return (
    <DemoSection
      id="tarih"
      title={t('dateSection.title')}
      description={t('dateSection.description')}
      icon={CalendarRange}
    >
      <ControlDemo
        name="<DatePicker/>"
        title={t('dateSection.orderDate.title')}
        description={t('dateSection.orderDate.description')}
        value={orderDate ? show(orderDate) : 'null'}
      >
        <DatePicker aria-label={t('dateSection.orderDate.aria')} value={orderDate} onValueChange={setOrderDate} />
      </ControlDemo>

      <ControlDemo
        name="<DatePicker min/>"
        title={t('dateSection.deliveryDate.title')}
        description={t('dateSection.deliveryDate.description')}
        value={deliveryDate ? show(deliveryDate) : 'null'}
      >
        <DatePicker
          aria-label={t('dateSection.deliveryDate.aria')}
          value={deliveryDate}
          onValueChange={setDeliveryDate}
          min={orderDate ?? undefined}
          placeholder={
            orderDate
              ? t('dateSection.deliveryDate.placeholderAfter', { date: formatDate(orderDate) })
              : t('dateSection.deliveryDate.placeholderNone')
          }
          disabled={!orderDate}
        />
      </ControlDemo>

      <ControlDemo
        name="<DateRangePicker/>"
        title={t('dateSection.dateRange.title')}
        description={t('dateSection.dateRange.description')}
        value={
          period?.from
            ? `{ from: ${show(period.from)}, to: ${period.to ? show(period.to) : 'null'} }`
            : 'null'
        }
        wide
      >
        <DateRangePicker
          aria-label={t('dateSection.dateRange.aria')}
          value={period}
          onValueChange={setPeriod}
          className="max-w-sm"
        />
      </ControlDemo>

      <ControlDemo
        name='<Input type="time"/>'
        title={t('dateSection.time.title')}
        description={t('dateSection.time.description')}
        value={show(time)}
      >
        <Input
          aria-label={t('dateSection.time.aria')}
          type="time"
          className="tnum"
          value={time}
          onChange={(event) => setTime(event.target.value)}
        />
      </ControlDemo>

      <ControlDemo
        name='<Input type="datetime-local"/>'
        title={t('dateSection.dateTime.title')}
        description={t('dateSection.dateTime.description')}
        value={show(meeting)}
      >
        <Input
          aria-label={t('dateSection.dateTime.aria')}
          type="datetime-local"
          className="tnum"
          value={meeting}
          onChange={(event) => setMeeting(event.target.value)}
        />
      </ControlDemo>

      <ControlDemo
        name='<Input type="month"/>'
        title={t('dateSection.month.title')}
        description={t('dateSection.month.description')}
        value={show(accountingMonth)}
      >
        <Input
          aria-label={t('dateSection.month.aria')}
          type="month"
          className="tnum"
          value={accountingMonth}
          onChange={(event) => setAccountingMonth(event.target.value)}
        />
      </ControlDemo>
      <ControlDemo
        name="<TimeRangeInput/>"
        title={t('dateSection.timeRange.title')}
        description={t('dateSection.timeRange.description')}
        value={`${show(shift)} → ${timeRangeMinutes(shift) ?? 0} ${t('dateSection.minutesSuffix')}`}
      >
        <TimeRangeInput aria-label={t('dateSection.timeRange.aria')} value={shift} onValueChange={setShift} />
      </ControlDemo>

      <ControlDemo
        name="<DurationInput/>"
        title={t('dateSection.duration.title')}
        description={t('dateSection.duration.description')}
        value={`${show(duration)} → ${formatDuration(duration ?? 0)}`}
      >
        <DurationInput aria-label={t('dateSection.duration.aria')} value={duration} onValueChange={setDuration} />
      </ControlDemo>

      <ControlDemo
        name="<QuarterPicker/>"
        title={t('dateSection.quarter.title')}
        description={t('dateSection.quarter.description')}
        value={`${show(quarter)} → ${formatDate(quarterRange(quarter).from)} – ${formatDate(quarterRange(quarter).to)}`}
        wide
      >
        <QuarterPicker aria-label={t('dateSection.quarter.aria')} value={quarter} onValueChange={setQuarter} />
      </ControlDemo>

      <ControlDemo
        name='<Input type="week"/>'
        title={t('dateSection.week.title')}
        description={t('dateSection.week.description')}
        value={show(week)}
      >
        <Input
          aria-label={t('dateSection.week.aria')}
          type="week"
          className="tnum"
          value={week}
          onChange={(event) => setWeek(event.target.value)}
        />
      </ControlDemo>
    </DemoSection>
  )
}
