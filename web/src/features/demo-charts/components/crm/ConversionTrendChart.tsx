import { CartesianGrid, Line, LineChart, ReferenceLine, XAxis, YAxis } from 'recharts'
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
import { CONVERSION_TARGET, CONVERSION_TREND } from '@/features/demo-charts/data/crm'
import { percent } from '@/features/demo-charts/data/format'

const latest = CONVERSION_TREND.at(-1)!

/**
 * A single line with the commitment drawn behind it. The target is a reference
 * line rather than a second series — it is not measured data, and drawing it
 * as one would put two different kinds of number on the same legend.
 */
export function ConversionTrendChart() {
  const { t } = useTranslation('demo-charts')
  const config = { rate: { label: t('conversionTrend.config.rate'), color: SERIES[0] } } satisfies ChartConfig

  return (
    <ChartCard
      title={t('conversionTrend.title')}
      description={t('conversionTrend.description')}
      hero={{ value: percent(latest.rate, 1), label: t('conversionTrend.heroLabel') }}
      note={t('conversionTrend.note', { target: CONVERSION_TARGET })}
    >
      <ChartContainer config={config} className="aspect-auto h-[228px] w-full">
        <LineChart data={CONVERSION_TREND} margin={{ left: 4, right: 12, top: 8 }}>
          <CartesianGrid vertical={false} />
          <XAxis dataKey="month" tickLine={false} axisLine={false} tickMargin={10} />
          <YAxis
            width={44}
            domain={[12, 30]}
            tickLine={false}
            axisLine={false}
            tickMargin={8}
            tickFormatter={(value: number) => percent(value)}
          />
          <ChartTooltip
            cursor={{ strokeDasharray: '4 4' }}
            content={<ChartTooltipContent formatter={tooltipRow((value) => percent(value, 1))} />}
          />
          <ReferenceLine
            y={CONVERSION_TARGET}
            stroke="var(--muted-foreground)"
            strokeDasharray="5 5"
            label={{
              value: t('conversionTrend.targetLabel', { target: CONVERSION_TARGET }),
              position: 'insideTopRight',
              fill: 'var(--muted-foreground)',
              fontSize: 11,
            }}
          />
          <Line {...STATIC_MARK}
            dataKey="rate"
            name={config.rate.label}
            type="monotone"
            stroke="var(--color-rate)"
            strokeWidth={2}
            dot={{ r: 3.5, strokeWidth: 0, fill: 'var(--color-rate)' }}
            activeDot={{ r: 5, stroke: 'var(--card)', strokeWidth: 2 }}
          />
        </LineChart>
      </ChartContainer>
    </ChartCard>
  )
}
