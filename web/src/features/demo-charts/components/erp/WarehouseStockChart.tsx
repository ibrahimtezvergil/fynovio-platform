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
import { SERIES, SURFACE_GAP } from '@/features/demo-charts/components/palette'
import { tooltipRow } from '@/features/demo-charts/components/tooltipRow'
import { WAREHOUSE_STOCK } from '@/features/demo-charts/data/erp'
import { money, moneyShort } from '@/features/demo-charts/data/format'

const total = WAREHOUSE_STOCK.reduce(
  (sum, warehouse) => sum + warehouse.raw + warehouse.wip + warehouse.finished,
  0,
)

/**
 * Stacked, because the depot total is the thing being compared and the split
 * inside it is secondary. A 2px card-coloured stroke keeps the three segments
 * from melting into one block.
 */
export function WarehouseStockChart() {
  const { t } = useTranslation('demo-charts')
  const config = {
    raw: { label: t('warehouseStock.config.raw'), color: SERIES[0] },
    wip: { label: t('warehouseStock.config.wip'), color: SERIES[1] },
    finished: { label: t('warehouseStock.config.finished'), color: SERIES[2] },
  } satisfies ChartConfig

  return (
    <ChartCard
      title={t('warehouseStock.title')}
      description={t('warehouseStock.description')}
      hero={{ value: moneyShort(total), label: t('warehouseStock.heroLabel') }}
      note={t('warehouseStock.note')}
    >
      <ChartContainer config={config} className="aspect-auto h-[248px] w-full">
        <BarChart data={WAREHOUSE_STOCK} margin={{ left: 4, right: 8, top: 4 }}>
          <CartesianGrid vertical={false} />
          <XAxis dataKey="warehouse" tickLine={false} axisLine={false} tickMargin={10} />
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
          <Bar {...STATIC_MARK}
            dataKey="raw"
            name={config.raw.label}
            stackId="stock"
            fill="var(--color-raw)"
            maxBarSize={44}
            {...SURFACE_GAP}
          />
          <Bar {...STATIC_MARK}
            dataKey="wip"
            name={config.wip.label}
            stackId="stock"
            fill="var(--color-wip)"
            maxBarSize={44}
            {...SURFACE_GAP}
          />
          <Bar {...STATIC_MARK}
            dataKey="finished"
            name={config.finished.label}
            stackId="stock"
            fill="var(--color-finished)"
            radius={[4, 4, 0, 0]}
            maxBarSize={44}
            {...SURFACE_GAP}
          />
          <ChartLegend itemSorter={null} content={<ChartLegendContent />} />
        </BarChart>
      </ChartContainer>
    </ChartCard>
  )
}
