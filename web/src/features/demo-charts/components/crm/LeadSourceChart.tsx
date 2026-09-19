import { Cell, Pie, PieChart } from 'recharts'
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
import { LEAD_SOURCES } from '@/features/demo-charts/data/crm'
import { count, percent } from '@/features/demo-charts/data/format'

const total = LEAD_SOURCES.reduce((sum, source) => sum + source.deals, 0)

/**
 * A knowing trade-off, so don't "fix" it blind. Channels are nominal, and the
 * rule for nominal categories is one hue per series — but five separable hues
 * is exactly what this palette does not have (see `palette.ts`), and five
 * identical slices would be an unreadable ring. So the slices step along one
 * hue by rank and identity is carried entirely by the named list beside them:
 * the ring shows the split, the list carries the numbers.
 *
 * If a sixth channel ever appears, this becomes a ranked bar rather than a
 * sixth step on the ramp.
 */
export function LeadSourceChart() {
  const { t } = useTranslation('demo-charts')
  const config = { deals: { label: t('leadSource.config.deals') } } satisfies ChartConfig

  return (
    <ChartCard
      title={t('leadSource.title')}
      description={t('leadSource.description')}
      hero={{ value: count(total), label: t('leadSource.heroLabel') }}
      note={t('leadSource.note')}
    >
      <div className="grid items-center gap-3 sm:grid-cols-[190px_minmax(0,1fr)]">
        <div className="relative">
          <ChartContainer config={config} className="aspect-square h-[190px] w-full">
            <PieChart>
              <ChartTooltip
                content={
                  <ChartTooltipContent
                    hideLabel
                    formatter={(value, _name, item) => (
                      <div className="flex flex-1 items-center justify-between gap-5 leading-none">
                        <span className="text-muted-foreground">{item.payload.source}</span>
                        <span className="tnum font-medium">
                          {count(Number(value))} · {percent((Number(value) / total) * 100)}
                        </span>
                      </div>
                    )}
                  />
                }
              />
              <Pie {...STATIC_MARK}
                data={LEAD_SOURCES}
                dataKey="deals"
                nameKey="source"
                innerRadius="58%"
                outerRadius="92%"
                paddingAngle={1}
              >
                {LEAD_SOURCES.map((source, index) => (
                  <Cell
                    key={source.source}
                    fill={SERIES[0]}
                    fillOpacity={RAMP[index]}
                    {...SURFACE_GAP}
                  />
                ))}
              </Pie>
            </PieChart>
          </ChartContainer>

          {/* The ring's hole is HTML, not SVG text — it inherits the page's
              type ramp and survives a narrow card. */}
          <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
            <span className="font-heading tnum text-[21px] leading-none font-semibold tracking-[-0.03em]">
              {count(total)}
            </span>
            <span className="text-muted-foreground mt-1 text-[11px]">{t('leadSource.centerLabel')}</span>
          </div>
        </div>

        {/* The legend is the data table: name, share and count, ranked. */}
        <ul className="flex flex-col gap-2">
          {LEAD_SOURCES.map((source, index) => (
            <li key={source.source} className="flex items-center gap-2.5 text-[12.5px]">
              <span
                aria-hidden
                className="size-2.5 shrink-0 rounded-[3px]"
                style={{ background: SERIES[0], opacity: RAMP[index] }}
              />
              <span className="min-w-0 flex-1 truncate">{source.source}</span>
              <span className="tnum text-muted-foreground">
                {percent((source.deals / total) * 100)}
              </span>
              <span className="tnum w-9 text-right font-[550]">{source.deals}</span>
            </li>
          ))}
        </ul>
      </div>
    </ChartCard>
  )
}
