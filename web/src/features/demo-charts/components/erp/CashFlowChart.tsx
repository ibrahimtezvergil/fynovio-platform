import { Bar, CartesianGrid, ComposedChart, Line, XAxis, YAxis } from 'recharts'
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
import { CASH_FLOW } from '@/features/demo-charts/data/erp'
import { money, moneyShort } from '@/features/demo-charts/data/format'

const latest = CASH_FLOW.at(-1)!

/**
 * Bars for the two flows, a line for what is left of them. All three are the
 * same unit on the same axis — a second y-scale would let any of them be drawn
 * above or below the others at will, which is the fastest way to lie with a
 * finance chart.
 */
export function CashFlowChart() {
  const { t } = useTranslation('demo-charts')
  const config = {
    income: { label: t('cashFlow.config.income'), color: SERIES[0] },
    expense: { label: t('cashFlow.config.expense'), color: SERIES[1] },
    net: { label: t('cashFlow.config.net'), color: SERIES[2] },
  } satisfies ChartConfig

  return (
    <ChartCard
      title={t('cashFlow.title')}
      description={t('cashFlow.description')}
      hero={{ value: moneyShort(latest.net), label: t('cashFlow.heroLabel') }}
      note={t('cashFlow.note')}
    >
      <ChartContainer config={config} className="aspect-auto h-[248px] w-full">
        <ComposedChart data={CASH_FLOW} margin={{ left: 4, right: 8, top: 4 }} barGap={2}>
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
            cursor={{ fill: 'var(--muted)', fillOpacity: 0.5 }}
            content={<ChartTooltipContent formatter={tooltipRow(money)} />}
          />
          <Bar
            {...STATIC_MARK}
            dataKey="income"
            name={config.income.label}
            fill="var(--color-income)"
            radius={4}
            maxBarSize={22}
          />
          <Bar
            {...STATIC_MARK}
            dataKey="expense"
            name={config.expense.label}
            fill="var(--color-expense)"
            radius={4}
            maxBarSize={22}
          />
          <Line {...STATIC_MARK}
            dataKey="net"
            name={config.net.label}
            type="monotone"
            stroke="var(--color-net)"
            strokeWidth={2}
            dot={{ r: 3, strokeWidth: 0, fill: 'var(--color-net)' }}
            activeDot={{ r: 5, stroke: 'var(--card)', strokeWidth: 2 }}
          />
          <ChartLegend itemSorter={null} content={<ChartLegendContent />} />
        </ComposedChart>
      </ChartContainer>
    </ChartCard>
  )
}
