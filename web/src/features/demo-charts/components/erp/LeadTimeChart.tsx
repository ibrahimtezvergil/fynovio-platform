import { Area, AreaChart, CartesianGrid, ReferenceLine, XAxis, YAxis } from 'recharts'
import { useTranslation } from 'react-i18next'
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from '@/components/ui/chart'
import { ChartCard } from '@/features/demo-charts/components/ChartCard'
import { STATIC_MARK } from '@/features/demo-charts/components/chartMotion'
import { SERIES } from '@/features/demo-charts/components/palette'
import { tooltipRow } from '@/features/demo-charts/components/tooltipRow'
import { LEAD_TIME, LEAD_TIME_SLA } from '@/features/demo-charts/data/erp'
import { days } from '@/features/demo-charts/data/format'

const latest = LEAD_TIME.at(-1)!

/**
 * One series, so the fill is here to make the trend legible rather than to
 * separate anything. The SLA is the line to be under — drawn once, labelled,
 * and left recessive so the data stays the loudest thing on the card.
 */
export function LeadTimeChart() {
  const { t } = useTranslation('demo-charts')
  const config = { days: { label: t('leadTime.config.days'), color: SERIES[0] } } satisfies ChartConfig

  return (
    <ChartCard
      title={t('leadTime.title')}
      description={t('leadTime.description')}
      hero={{ value: days(latest.days), label: t('leadTime.heroLabel') }}
      note={t('leadTime.note', { sla: LEAD_TIME_SLA })}
    >
      <ChartContainer config={config} className="aspect-auto h-[228px] w-full">
        <AreaChart data={LEAD_TIME} margin={{ left: 4, right: 12, top: 8 }}>
          <CartesianGrid vertical={false} />
          <XAxis dataKey="week" tickLine={false} axisLine={false} tickMargin={10} />
          <YAxis
            width={44}
            domain={[0, 10]}
            tickLine={false}
            axisLine={false}
            tickMargin={8}
            tickFormatter={(value: number) => String(value)}
          />
          <ChartTooltip
            cursor={{ strokeDasharray: '4 4' }}
            content={<ChartTooltipContent formatter={tooltipRow((value) => days(value))} />}
          />
          <ReferenceLine
            y={LEAD_TIME_SLA}
            stroke="var(--muted-foreground)"
            strokeDasharray="5 5"
            label={{
              value: t('leadTime.slaLabel', { sla: LEAD_TIME_SLA }),
              position: 'insideTopRight',
              fill: 'var(--muted-foreground)',
              fontSize: 11,
            }}
          />
          <Area {...STATIC_MARK}
            dataKey="days"
            name={config.days.label}
            type="monotone"
            stroke="var(--color-days)"
            strokeWidth={2}
            fill="var(--color-days)"
            fillOpacity={0.18}
            dot={{ r: 3, strokeWidth: 0, fill: 'var(--color-days)' }}
            activeDot={{ r: 5, stroke: 'var(--card)', strokeWidth: 2 }}
          />
        </AreaChart>
      </ChartContainer>
    </ChartCard>
  )
}
