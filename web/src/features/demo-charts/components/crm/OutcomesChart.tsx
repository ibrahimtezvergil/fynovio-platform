import { Bar, BarChart, CartesianGrid, XAxis, YAxis } from 'recharts'
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
import { OUTCOMES } from '@/features/demo-charts/data/crm'
import { count, percent } from '@/features/demo-charts/data/format'

const last = OUTCOMES.at(-1)!
const winRate = (last.won / (last.won + last.lost + last.postponed)) * 100

/**
 * Grouped, not stacked: the question is "how did this quarter's closes split",
 * and a stack makes the middle band impossible to compare across quarters.
 * Three series is the ceiling — the fourth colour would stop being separable.
 */
export function OutcomesChart() {
  const { t } = useTranslation('demo-charts')
  const config = {
    won: { label: t('outcomes.config.won'), color: SERIES[0] },
    lost: { label: t('outcomes.config.lost'), color: SERIES[1] },
    postponed: { label: t('outcomes.config.postponed'), color: SERIES[2] },
  } satisfies ChartConfig

  return (
    <ChartCard
      title={t('outcomes.title')}
      description={t('outcomes.description')}
      hero={{ value: percent(winRate), label: t('outcomes.heroLabel') }}
      note={t('outcomes.note')}
    >
      <ChartContainer config={config} className="aspect-auto h-[248px] w-full">
        <BarChart data={OUTCOMES} margin={{ left: 4, right: 8, top: 4 }} barGap={2}>
          <CartesianGrid vertical={false} />
          <XAxis dataKey="period" tickLine={false} axisLine={false} tickMargin={10} />
          <YAxis width={34} tickLine={false} axisLine={false} tickMargin={8} />
          <ChartTooltip
            cursor={false}
            content={<ChartTooltipContent formatter={tooltipRow(count)} />}
          />
          <Bar
            {...STATIC_MARK}
            dataKey="won"
            name={config.won.label}
            fill="var(--color-won)"
            radius={4}
            maxBarSize={26}
          />
          <Bar
            {...STATIC_MARK}
            dataKey="lost"
            name={config.lost.label}
            fill="var(--color-lost)"
            radius={4}
            maxBarSize={26}
          />
          <Bar {...STATIC_MARK}
            dataKey="postponed"
            name={config.postponed.label}
            fill="var(--color-postponed)"
            radius={4}
            maxBarSize={26}
          />
          <ChartLegend itemSorter={null} content={<ChartLegendContent />} />
        </BarChart>
      </ChartContainer>
    </ChartCard>
  )
}
