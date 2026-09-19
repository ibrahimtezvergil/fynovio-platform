import { Area, AreaChart, CartesianGrid, XAxis, YAxis } from 'recharts'
import { useTranslation } from 'react-i18next'
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from '@/components/ui/chart'
import { ChartCard } from '@/features/demo-charts/components/ChartCard'
import { STATIC_MARK } from '@/features/demo-charts/components/chartMotion'
import { SERIES } from '@/features/demo-charts/components/palette'
import { tooltipRow } from '@/features/demo-charts/components/tooltipRow'
import { REVENUE_TREND } from '@/features/demo-charts/data/crm'
import { money, moneyShort } from '@/features/demo-charts/data/format'

const total = REVENUE_TREND.at(-1)!

/**
 * Change over time, split into its two parts. Renewal is the base of the stack
 * because it is the part that carries over — new business is what sits on top
 * of it, which is the comparison a sales review actually makes.
 */
export function RevenueTrendChart() {
  const { t } = useTranslation('demo-charts')
  const config = {
    renewal: { label: t('revenueTrend.config.renewal'), color: SERIES[0] },
    newBusiness: { label: t('revenueTrend.config.newBusiness'), color: SERIES[1] },
  } satisfies ChartConfig

  return (
    <ChartCard
      title={t('revenueTrend.title')}
      description={t('revenueTrend.description')}
      hero={{ value: moneyShort(total.renewal + total.newBusiness), label: t('revenueTrend.heroLabel') }}
      note={t('revenueTrend.note')}
    >
      <ChartContainer config={config} className="aspect-auto h-[248px] w-full">
        <AreaChart data={REVENUE_TREND} margin={{ left: 4, right: 8, top: 4 }}>
          <CartesianGrid vertical={false} />
          <XAxis dataKey="month" tickLine={false} axisLine={false} tickMargin={10} />
          <YAxis
            width={54}
            tickLine={false}
            axisLine={false}
            tickMargin={8}
            tickFormatter={moneyShort}
          />
          <ChartTooltip
            cursor={{ strokeDasharray: '4 4' }}
            content={<ChartTooltipContent formatter={tooltipRow(money)} />}
          />
          {/* The 2px stroke in the series' own colour is the gap between the
              stacked bands — without it the two fills touch and read as one. */}
          <Area {...STATIC_MARK}
            dataKey="renewal"
            name={config.renewal.label}
            type="monotone"
            stackId="revenue"
            fill="var(--color-renewal)"
            fillOpacity={0.32}
            stroke="var(--color-renewal)"
            strokeWidth={2}
          />
          <Area {...STATIC_MARK}
            dataKey="newBusiness"
            name={config.newBusiness.label}
            type="monotone"
            stackId="revenue"
            fill="var(--color-newBusiness)"
            fillOpacity={0.32}
            stroke="var(--color-newBusiness)"
            strokeWidth={2}
          />
          <ChartLegend itemSorter={null} content={<ChartLegendContent />} />
        </AreaChart>
      </ChartContainer>
    </ChartCard>
  )
}
