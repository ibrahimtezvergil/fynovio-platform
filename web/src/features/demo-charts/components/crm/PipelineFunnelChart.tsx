import { Bar, BarChart, Cell, LabelList, XAxis, YAxis } from 'recharts'
import { useTranslation } from 'react-i18next'
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from '@/components/ui/chart'
import { ChartCard } from '@/features/demo-charts/components/ChartCard'
import { STATIC_MARK } from '@/features/demo-charts/components/chartMotion'
import { STAGE_COLOR } from '@/features/demo-charts/components/palette'
import { FUNNEL } from '@/features/demo-charts/data/crm'
import { count, moneyShort, percent } from '@/features/demo-charts/data/format'

/** Each step also carries what share of the step above it survived. */
const steps = FUNNEL.map((step, index) => ({
  ...step,
  carried: index === 0 ? 100 : (step.count / FUNNEL[index - 1].count) * 100,
}))

const endToEnd = (FUNNEL.at(-1)!.count / FUNNEL[0].count) * 100

/**
 * The funnel as a ranked bar, not a tapered polygon — a polygon's area lies
 * about the drop between steps. Stage colour repeats the board and the badges,
 * and the stage name sits on the axis, so colour never carries it alone.
 */
export function PipelineFunnelChart() {
  const { t } = useTranslation('demo-charts')
  const config = { count: { label: t('pipelineFunnel.config.count') } } satisfies ChartConfig

  return (
    <ChartCard
      title={t('pipelineFunnel.title')}
      description={t('pipelineFunnel.description')}
      hero={{ value: percent(endToEnd, 1), label: t('pipelineFunnel.heroLabel') }}
      note={t('pipelineFunnel.note')}
    >
      <ChartContainer config={config} className="aspect-auto h-[248px] w-full">
        <BarChart data={steps} layout="vertical" margin={{ left: 4, right: 56 }}>
          <XAxis type="number" dataKey="count" hide />
          <YAxis
            type="category"
            dataKey="label"
            width={78}
            tickLine={false}
            axisLine={false}
            tickMargin={8}
          />
          <ChartTooltip
            cursor={false}
            content={
              <ChartTooltipContent
                labelKey="label"
                formatter={(value, _name, item) => (
                  <div className="flex flex-1 flex-col gap-1 leading-none">
                    <div className="flex items-center justify-between gap-5">
                      <span className="text-muted-foreground">{t('pipelineFunnel.tooltip.deals')}</span>
                      <span className="tnum font-medium">{count(Number(value))}</span>
                    </div>
                    <div className="flex items-center justify-between gap-5">
                      <span className="text-muted-foreground">{t('pipelineFunnel.tooltip.totalValue')}</span>
                      <span className="tnum font-medium">{moneyShort(item.payload.value)}</span>
                    </div>
                    <div className="flex items-center justify-between gap-5">
                      <span className="text-muted-foreground">{t('pipelineFunnel.tooltip.fromPreviousStage')}</span>
                      <span className="tnum font-medium">{percent(item.payload.carried, 1)}</span>
                    </div>
                  </div>
                )}
              />
            }
          />
          <Bar {...STATIC_MARK} dataKey="count" radius={4} barSize={26}>
            {steps.map((step) => (
              <Cell key={step.stage} fill={STAGE_COLOR[step.stage]} />
            ))}
            <LabelList
              dataKey="count"
              position="right"
              offset={10}
              className="fill-foreground tnum"
              fontSize={12}
            />
          </Bar>
        </BarChart>
      </ChartContainer>
    </ChartCard>
  )
}
