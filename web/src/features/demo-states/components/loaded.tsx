import { Bar, BarChart, CartesianGrid, XAxis } from 'recharts'
import { TrendingUp } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { StageBadge } from '@/components/common/StageBadge'
import { Button } from '@/components/ui/button'
import { ChartContainer, type ChartConfig } from '@/components/ui/chart'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { Stage } from '@/types'

/**
 * The settled counterparts of `skeletons.tsx`. They exist so the demo can run
 * the real swap, and they are laid out block-for-block against the skeleton
 * beside them — that identity is the whole claim a skeleton makes.
 */

export function MetricGridLoaded() {
  const { t } = useTranslation('demo-states')

  const metrics = [
    { label: t('loaded.metrics.openDeals.label'), value: '38', delta: '+12%', context: t('loaded.metrics.openDeals.context') },
    { label: t('loaded.metrics.won.label'), value: '14', delta: '+4%', context: t('loaded.metrics.won.context') },
    { label: t('loaded.metrics.avgAmount.label'), value: '₺84.2B', delta: '+9%', context: t('loaded.metrics.avgAmount.context') },
    { label: t('loaded.metrics.conversion.label'), value: '%23,4', delta: '+1,8p', context: t('loaded.metrics.conversion.context') },
  ]

  return (
    <div className="grid gap-3 p-4 sm:grid-cols-2">
      {metrics.map((metric) => (
        <div
          key={metric.label}
          className="flex flex-col gap-[13px] rounded-xl border border-[var(--nx-hairline)] px-[21px] py-[19px]"
        >
          <div className="flex items-center gap-2.5">
            <span aria-hidden className="nx-icon-tile size-[26px] rounded-[var(--nx-r-tile)]">
              <TrendingUp className="size-[15px]" strokeWidth={1.7} />
            </span>
            <p className="text-muted-foreground text-[12.5px] font-[550]">{metric.label}</p>
          </div>
          <p className="font-heading tnum text-[32px] leading-none font-semibold tracking-[-0.04em]">
            {metric.value}
          </p>
          <div className="text-muted-foreground flex items-center gap-2 text-[12.5px]">
            <span className="nx-pill tnum" data-tone="green">
              <TrendingUp aria-hidden className="size-3.5" strokeWidth={2} />
              {metric.delta}
            </span>
            <span>{metric.context}</span>
          </div>
        </div>
      ))}
    </div>
  )
}

const ROWS: readonly { company: string; stage: Stage; amount: string }[] = [
  { company: 'Aydın Lojistik A.Ş.', stage: 'quoted', amount: '₺142.000' },
  { company: 'Meriç Tekstil', stage: 'meeting', amount: '₺86.500' },
  { company: 'Karahan İnşaat', stage: 'new', amount: '₺215.000' },
  { company: 'Deniz Gıda Sanayi', stage: 'ready', amount: '₺58.200' },
  { company: 'Ilgaz Enerji', stage: 'contacted', amount: '₺97.400' },
]

export function TableLoaded() {
  const { t } = useTranslation('demo-states')

  return (
    <div className="flex flex-col">
      <div className="text-muted-foreground flex items-center gap-4 border-b border-[var(--nx-hairline)] px-4 py-2.5 text-[11px] font-[590] tracking-[0.02em] uppercase">
        <span className="flex-1">{t('tableHeaders.company')}</span>
        <span className="w-28">{t('tableHeaders.stage')}</span>
        <span className="w-24 text-right">{t('tableHeaders.amount')}</span>
      </div>
      {ROWS.map((row) => (
        <div
          key={row.company}
          className="flex h-[52px] items-center gap-4 border-b border-[var(--nx-hairline-soft)] px-4 text-[13px] last:border-b-0"
        >
          <span className="flex-1 truncate font-[550]">{row.company}</span>
          <StageBadge stage={row.stage} />
          <span className="tnum ml-auto w-24 text-right">{row.amount}</span>
        </div>
      ))}
    </div>
  )
}

export function ListLoaded() {
  const { t } = useTranslation('demo-states')

  const activities = [
    {
      name: t('loaded.activities.quoteFollowUp', { company: 'Meriç Tekstil' }),
      meta: 'Ayşe Kaya',
      when: '09:30',
    },
    {
      name: t('loaded.activities.demoCall', { company: 'Ilgaz Enerji' }),
      meta: 'Burak Demir',
      when: '11:00',
    },
    {
      name: t('loaded.activities.contractReview'),
      meta: t('loaded.activities.metaLegal'),
      when: '14:15',
    },
    {
      name: t('loaded.activities.weeklyMeeting'),
      meta: t('loaded.activities.metaSalesTeam'),
      when: '16:00',
    },
  ]

  return (
    <div className="flex flex-col">
      {activities.map((activity) => (
        <div key={activity.name} className="nx-row px-4">
          <span aria-hidden className="nx-icon-tile" data-tone="blue">
            <TrendingUp className="size-4" strokeWidth={1.75} />
          </span>
          <div className="flex min-w-0 flex-1 flex-col gap-0.5">
            <p className="truncate text-[13px] font-[550]">{activity.name}</p>
            <p className="text-muted-foreground text-[11.5px]">{activity.meta}</p>
          </div>
          <span className="text-muted-foreground tnum text-[12px]">{activity.when}</span>
        </div>
      ))}
    </div>
  )
}

export function ChartLoaded() {
  const { t } = useTranslation('demo-states')

  const trend = [
    { day: t('loaded.chart.days.mon'), deals: 12 },
    { day: t('loaded.chart.days.tue'), deals: 18 },
    { day: t('loaded.chart.days.wed'), deals: 9 },
    { day: t('loaded.chart.days.thu'), deals: 22 },
    { day: t('loaded.chart.days.fri'), deals: 15 },
    { day: t('loaded.chart.days.sat'), deals: 20 },
    { day: t('loaded.chart.days.sun'), deals: 13 },
  ]

  const chartConfig = {
    deals: { label: t('loaded.chart.dealsLabel'), color: 'var(--color-chart-1)' },
  } satisfies ChartConfig

  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex items-center justify-between">
        <p className="text-[13px] font-[590]">{t('loaded.chart.title')}</p>
        <span className="nx-pill" data-tone="green">
          {t('loaded.chart.weeklyChange')}
        </span>
      </div>
      <ChartContainer config={chartConfig} className="aspect-auto h-[152px] w-full">
        <BarChart data={trend} margin={{ top: 4 }}>
          <CartesianGrid vertical={false} />
          <XAxis dataKey="day" tickLine={false} axisLine={false} tickMargin={8} />
          <Bar
            dataKey="deals"
            fill="var(--color-deals)"
            radius={[4, 4, 0, 0]}
            isAnimationActive={false}
          />
        </BarChart>
      </ChartContainer>
    </div>
  )
}

export function FormLoaded() {
  const { t } = useTranslation('demo-states')

  return (
    <div className="flex flex-col gap-4 p-4">
      <div className="flex flex-col gap-2">
        <Label htmlFor="states-demo-company">{t('loaded.form.companyLabel')}</Label>
        <Input id="states-demo-company" defaultValue="Meriç Tekstil" readOnly />
      </div>
      <div className="flex flex-col gap-2">
        <Label htmlFor="states-demo-contact">{t('loaded.form.contactLabel')}</Label>
        <Input id="states-demo-contact" defaultValue="Ayşe Kaya" readOnly />
      </div>
      <div className="flex flex-col gap-2">
        <Label htmlFor="states-demo-amount">{t('loaded.form.amountLabel')}</Label>
        <Input id="states-demo-amount" defaultValue="₺86.500" readOnly />
      </div>
      <div className="flex justify-end gap-2 pt-1">
        <Button variant="outline">{t('loaded.form.cancel')}</Button>
        <Button>{t('loaded.form.save')}</Button>
      </div>
    </div>
  )
}

export function ProfileLoaded() {
  const { t } = useTranslation('demo-states')

  return (
    <div className="flex items-center gap-3.5 p-4">
      <span aria-hidden className="nx-avatar size-12 text-[15px]">
        AK
      </span>
      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <p className="text-[14px] font-[590]">Ayşe Kaya</p>
        <p className="text-muted-foreground text-[12px]">ayse.kaya@fynovio.com</p>
        <div className="mt-0.5 flex gap-2">
          <span className="nx-pill" data-tone="purple">
            {t('loaded.profile.salesPill')}
          </span>
          <span className="nx-pill" data-tone="green">
            {t('loaded.profile.activePill')}
          </span>
        </div>
      </div>
    </div>
  )
}

export function TextLoaded() {
  const { t } = useTranslation('demo-states')

  return (
    <div className="p-4 text-[13px] leading-[1.65]">
      <p>{t('loaded.text')}</p>
    </div>
  )
}
