import { Bar, BarChart, XAxis, YAxis } from 'recharts'
import { useTranslation } from 'react-i18next'
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from '@/components/ui/chart'
import { ChartCard } from '@/features/demo-charts/components/ChartCard'
import { STATIC_MARK } from '@/features/demo-charts/components/chartMotion'
import { RAMP, SERIES, SURFACE_GAP } from '@/features/demo-charts/components/palette'
import { STOCK_AGING } from '@/features/demo-charts/data/erp'
import { money, moneyShort, percent } from '@/features/demo-charts/data/format'

const total = STOCK_AGING.reduce((sum, band) => sum + band.value, 0)

/** One row: the whole stock, split into its ageing bands. */
const row = [
  Object.fromEntries([['name', 'stock'], ...STOCK_AGING.map((band) => [band.key, band.value])]),
]

const config = Object.fromEntries(
  STOCK_AGING.map((band) => [band.key, { label: band.label, color: SERIES[0] }]),
) satisfies ChartConfig

/**
 * A single 100% bar: age is an ordered quantity, so the bands step along one
 * hue instead of picking four unrelated colours. The oldest band is the one
 * being hunted, and it is the darkest end of the ramp.
 */
export function StockAgingChart() {
  const { t } = useTranslation('demo-charts')
  const oldest = STOCK_AGING.at(-1)!

  return (
    <ChartCard
      title={t('stockAging.title')}
      description={t('stockAging.description')}
      hero={{ value: percent((oldest.value / total) * 100), label: t('stockAging.heroLabel') }}
      note={t('stockAging.note')}
    >
      <ChartContainer config={config} className="aspect-auto h-[92px] w-full">
        <BarChart data={row} layout="vertical" margin={{ left: 0, right: 0, top: 8, bottom: 8 }}>
          <XAxis type="number" hide />
          <YAxis type="category" dataKey="name" hide />
          <ChartTooltip
            cursor={false}
            content={
              <ChartTooltipContent
                hideLabel
                formatter={(value, name) => (
                  <div className="flex flex-1 items-center justify-between gap-5 leading-none">
                    <span className="text-muted-foreground">{String(name)}</span>
                    <span className="tnum font-medium">
                      {money(Number(value))} · {percent((Number(value) / total) * 100)}
                    </span>
                  </div>
                )}
              />
            }
          />
          {STOCK_AGING.map((band, index) => (
            <Bar {...STATIC_MARK}
              key={band.key}
              dataKey={band.key}
              name={band.label}
              stackId="aging"
              fill={SERIES[0]}
              fillOpacity={RAMP[STOCK_AGING.length - 1 - index]}
              barSize={30}
              radius={4}
              {...SURFACE_GAP}
            />
          ))}
        </BarChart>
      </ChartContainer>

      <ul className="grid grid-cols-2 gap-x-6 gap-y-2 sm:grid-cols-4">
        {STOCK_AGING.map((band, index) => (
          <li key={band.key} className="flex min-w-0 flex-col gap-1">
            <span className="flex items-center gap-2 text-[12px]">
              <span
                aria-hidden
                className="size-2.5 shrink-0 rounded-[3px]"
                style={{ background: SERIES[0], opacity: RAMP[STOCK_AGING.length - 1 - index] }}
              />
              <span className="text-muted-foreground truncate">{band.label}</span>
            </span>
            <span className="tnum text-[13px] font-[550]">{moneyShort(band.value)}</span>
          </li>
        ))}
      </ul>
    </ChartCard>
  )
}
