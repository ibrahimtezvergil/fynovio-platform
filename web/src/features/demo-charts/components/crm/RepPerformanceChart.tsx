import { Bar, BarChart, LabelList, XAxis, YAxis } from 'recharts'
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
import { REP_PERFORMANCE } from '@/features/demo-charts/data/crm'
import { money, moneyShort, percent } from '@/features/demo-charts/data/format'

const total = REP_PERFORMANCE.reduce((sum, rep) => sum + rep.revenue, 0)

/**
 * One measure, ranked, so there is nothing for a second colour to say — the
 * bar's own length is the encoding and the value is printed at its end (the
 * value axis is hidden, so the labels are not decoration). The title names the
 * series, which is why there is no legend.
 */
export function RepPerformanceChart() {
  const { t } = useTranslation('demo-charts')
  const config = { revenue: { label: t('repPerformance.config.revenue') } } satisfies ChartConfig

  return (
    <ChartCard
      title={t('repPerformance.title')}
      description={t('repPerformance.description')}
      hero={{ value: moneyShort(total), label: t('repPerformance.heroLabel') }}
      note={t('repPerformance.note')}
    >
      <ChartContainer config={config} className="aspect-auto h-[228px] w-full">
        <BarChart data={REP_PERFORMANCE} layout="vertical" margin={{ left: 4, right: 72 }}>
          <XAxis type="number" dataKey="revenue" hide />
          <YAxis
            type="category"
            dataKey="rep"
            width={118}
            tickLine={false}
            axisLine={false}
            tickMargin={8}
          />
          <ChartTooltip
            cursor={false}
            content={
              <ChartTooltipContent
                labelKey="rep"
                formatter={(value, _name, item) => (
                  <div className="flex flex-1 flex-col gap-1 leading-none">
                    <div className="flex items-center justify-between gap-5">
                      <span className="text-muted-foreground">{t('repPerformance.tooltip.closed')}</span>
                      <span className="tnum font-medium">{money(Number(value))}</span>
                    </div>
                    <div className="flex items-center justify-between gap-5">
                      <span className="text-muted-foreground">{t('repPerformance.tooltip.quota')}</span>
                      <span className="tnum font-medium">{percent(item.payload.quotaPercent)}</span>
                    </div>
                  </div>
                )}
              />
            }
          />
          {/* Names are nominal, so every bar takes the same hue: tinting them by
              rank would spend the identity channel re-drawing what the bar
              length already says. */}
          <Bar {...STATIC_MARK} dataKey="revenue" fill={SERIES[0]} radius={4} barSize={22}>
            <LabelList
              dataKey="revenue"
              position="right"
              offset={10}
              className="fill-foreground tnum"
              fontSize={12}
              formatter={(value) => moneyShort(Number(value))}
            />
          </Bar>
        </BarChart>
      </ChartContainer>
    </ChartCard>
  )
}
