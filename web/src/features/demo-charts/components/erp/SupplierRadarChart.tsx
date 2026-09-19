import { PolarAngleAxis, PolarGrid, PolarRadiusAxis, Radar, RadarChart } from 'recharts'
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
import { SUPPLIER_SCORES } from '@/features/demo-charts/data/erp'

/** The two supplier names ("Ege Metal", "Nordic Steel") are proper nouns — not translated. */
const config = {
  aegean: { label: 'Ege Metal', color: SERIES[0] },
  nordic: { label: 'Nordic Steel', color: SERIES[1] },
} satisfies ChartConfig

/**
 * Two suppliers on the same five axes. A radar earns its place only when the
 * axes share a scale and there are at most a couple of shapes to compare —
 * a third supplier here would turn it into a knot.
 */
export function SupplierRadarChart() {
  const { t } = useTranslation('demo-charts')

  return (
    <ChartCard
      title={t('supplierRadar.title')}
      description={t('supplierRadar.description')}
      note={t('supplierRadar.note')}
    >
      <ChartContainer config={config} className="mx-auto aspect-square h-[248px]">
        <RadarChart data={SUPPLIER_SCORES} outerRadius="72%">
          <PolarGrid />
          <PolarAngleAxis dataKey="criterion" />
          <PolarRadiusAxis domain={[0, 100]} tick={false} axisLine={false} />
          <ChartTooltip content={<ChartTooltipContent labelKey="criterion" />} />
          <Radar {...STATIC_MARK}
            dataKey="aegean"
            name={config.aegean.label}
            stroke="var(--color-aegean)"
            strokeWidth={2}
            fill="var(--color-aegean)"
            fillOpacity={0.22}
          />
          <Radar {...STATIC_MARK}
            dataKey="nordic"
            name={config.nordic.label}
            stroke="var(--color-nordic)"
            strokeWidth={2}
            fill="var(--color-nordic)"
            fillOpacity={0.22}
          />
          <ChartLegend itemSorter={null} content={<ChartLegendContent />} />
        </RadarChart>
      </ChartContainer>

      {/* A radar's radius has no readable axis, so the scores would otherwise
          live only inside the tooltip. This is the chart's table view. */}
      <table className="w-full text-[12.5px]">
        <caption className="sr-only">{t('supplierRadar.tableCaption')}</caption>
        <thead>
          <tr className="text-muted-foreground text-[11px]">
            <th scope="col" className="pb-1.5 text-left font-[550]">
              {t('supplierRadar.criterionHeader')}
            </th>
            <th scope="col" className="pb-1.5 text-right font-[550]">
              {config.aegean.label}
            </th>
            <th scope="col" className="pb-1.5 text-right font-[550]">
              {config.nordic.label}
            </th>
          </tr>
        </thead>
        <tbody>
          {SUPPLIER_SCORES.map((score) => (
            <tr key={score.criterion} className="border-border/60 border-t">
              <th scope="row" className="py-1.5 text-left font-normal">
                {score.criterion}
              </th>
              <td className="tnum py-1.5 text-right font-[550]">{score.aegean}</td>
              <td className="tnum py-1.5 text-right font-[550]">{score.nordic}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </ChartCard>
  )
}
