import { CartesianGrid, Cell, Scatter, ScatterChart, XAxis, YAxis, ZAxis } from 'recharts'
import { useTranslation } from 'react-i18next'
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from '@/components/ui/chart'
import { ChartCard } from '@/features/demo-charts/components/ChartCard'
import { STATIC_MARK } from '@/features/demo-charts/components/chartMotion'
import { SERIES, STATUS_COLOR } from '@/features/demo-charts/components/palette'
import { PRODUCTION_LINES } from '@/features/demo-charts/data/erp'
import { count, percent } from '@/features/demo-charts/data/format'

/** Above this the line needs a quality review, whatever its utilisation. */
const DEFECT_LIMIT = 3

/**
 * Two measures, one point per line: how hard it runs against what it scraps.
 * The dot's size is output, so a small dot high on the y axis is a line that
 * is both slow and wasteful. Colour marks only the ones over the fire limit —
 * it is a status, not a series, and it never travels without the label.
 */
export function ProductionScatterChart() {
  const { t } = useTranslation('demo-charts')
  const config = { lines: { label: t('productionScatter.config.lines') } } satisfies ChartConfig
  const flagged = PRODUCTION_LINES.filter((line) => line.defectRate > DEFECT_LIMIT)

  return (
    <ChartCard
      title={t('productionScatter.title')}
      description={t('productionScatter.description')}
      hero={{ value: String(flagged.length), label: t('productionScatter.heroLabel') }}
      note={t('productionScatter.note', { limit: DEFECT_LIMIT })}
    >
      <ChartContainer config={config} className="aspect-auto h-[248px] w-full">
        <ScatterChart margin={{ left: 4, right: 12, top: 8, bottom: 8 }}>
          <CartesianGrid />
          <XAxis
            type="number"
            dataKey="utilization"
            name={t('productionScatter.axis.capacity')}
            domain={[50, 100]}
            tickLine={false}
            axisLine={false}
            tickMargin={10}
            tickFormatter={(value: number) => percent(value)}
          />
          <YAxis
            type="number"
            dataKey="defectRate"
            name={t('productionScatter.axis.defect')}
            width={44}
            domain={[0, 5]}
            tickLine={false}
            axisLine={false}
            tickMargin={8}
            tickFormatter={(value: number) => percent(value)}
          />
          {/* Floor the smallest mark at a hittable size — the range is an area,
              so 200 is roughly a 16px dot. */}
          <ZAxis type="number" dataKey="output" range={[200, 520]} name={t('productionScatter.axis.output')} />
          <ChartTooltip
            cursor={{ strokeDasharray: '4 4' }}
            content={
              <ChartTooltipContent
                hideLabel
                formatter={(_value, _name, item) => (
                  <div className="flex flex-1 flex-col gap-1.5 leading-none">
                    <span className="font-medium">{item.payload.line}</span>
                    <div className="flex items-center justify-between gap-5">
                      <span className="text-muted-foreground">{t('productionScatter.axis.capacity')}</span>
                      <span className="tnum">{percent(item.payload.utilization)}</span>
                    </div>
                    <div className="flex items-center justify-between gap-5">
                      <span className="text-muted-foreground">{t('productionScatter.axis.defect')}</span>
                      <span className="tnum">{percent(item.payload.defectRate, 1)}</span>
                    </div>
                    <div className="flex items-center justify-between gap-5">
                      <span className="text-muted-foreground">{t('productionScatter.axis.output')}</span>
                      <span className="tnum">{count(item.payload.output)} {t('productionScatter.unit')}</span>
                    </div>
                  </div>
                )}
              />
            }
          />
          <Scatter {...STATIC_MARK} data={PRODUCTION_LINES} fillOpacity={0.75}>
            {PRODUCTION_LINES.map((line) => (
              <Cell
                key={line.line}
                fill={line.defectRate > DEFECT_LIMIT ? STATUS_COLOR.negative : SERIES[0]}
                stroke="var(--card)"
                strokeWidth={2}
              />
            ))}
          </Scatter>
        </ScatterChart>
      </ChartContainer>

      <ul className="grid gap-x-6 gap-y-1.5 sm:grid-cols-2">
        {PRODUCTION_LINES.map((line) => (
          <li key={line.line} className="flex items-center gap-2 text-[12px]">
            <span className="min-w-0 flex-1 truncate">{line.line}</span>
            <span className="tnum text-muted-foreground">{percent(line.utilization)}</span>
            <span
              className="nx-pill tnum"
              data-tone={line.defectRate > DEFECT_LIMIT ? 'red' : 'gray'}
            >
              {t('productionScatter.defectPill', { value: percent(line.defectRate, 1) })}
            </span>
          </li>
        ))}
      </ul>
    </ChartCard>
  )
}
