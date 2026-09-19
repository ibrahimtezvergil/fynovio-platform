import { CircleDollarSign, Plus, Target, Timer, Layers, SlidersHorizontal } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { DensityToggle } from '@/components/common/DensityToggle'
import { PageHeader } from '@/components/common/PageHeader'
import { SegmentedControl, type Segment } from '@/components/common/SegmentedControl'
import { Toolbar, ToolbarSearch, ToolbarSpacer } from '@/components/common/Toolbar'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { useActivities, useDeals, useStageDistribution } from '@/features/dashboard/api'
import { DealsTable } from '@/features/dashboard/components/DealsTable'
import { MetricCard } from '@/features/dashboard/components/MetricCard'
import { StageDistribution } from '@/features/dashboard/components/StageDistribution'
import { UpcomingActivities } from '@/features/dashboard/components/UpcomingActivities'
import { useDashboardStore, type DateRange } from '@/features/dashboard/store/useDashboardStore'

export default function DashboardPage() {
  const { t } = useTranslation('dashboard')
  const rangeSegments: readonly Segment<DateRange>[] = useMemo(
    () => [
      { value: 'current', label: t('page.rangeCurrent') },
      { value: 'previous', label: t('page.rangePrevious') },
      { value: 'yearly', label: t('page.rangeYearly') },
    ],
    [t],
  )
  const range = useDashboardStore((state) => state.range)
  const setRange = useDashboardStore((state) => state.setRange)
  // The query is ephemeral page state, not a preference: it dies with the
  // page, so it stays in the component rather than in a store.
  const [query, setQuery] = useState('')
  const { data: deals = [], isLoading } = useDeals()
  const { data: buckets = [], isLoading: stagesLoading } = useStageDistribution()
  const { data: activities = [], isLoading: activitiesLoading } = useActivities()

  const visible = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase('tr')
    if (needle === '') return deals
    return deals.filter((deal) =>
      [deal.title, deal.account, deal.owner].some((field) =>
        field.toLocaleLowerCase('tr').includes(needle),
      ),
    )
  }, [deals, query])

  return (
    <div className="flex flex-col gap-5">
      <PageHeader
        eyebrow={t('page.eyebrow')}
        title={t('page.title')}
        description={t('page.description')}
        actions={
          <>
            <SegmentedControl
              aria-label={t('page.rangeLabel')}
              segments={rangeSegments}
              value={range}
              onChange={setRange}
            />
            <Button>
              <Plus aria-hidden strokeWidth={2} />
              {t('page.newDeal')}
            </Button>
          </>
        }
      />

      <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-2 xl:grid-cols-4">
        <MetricCard
          icon={CircleDollarSign}
          label={t('metrics.pipelineValue')}
          value="₺4.182.500"
          delta={{ value: '+8,2%', direction: 'up' }}
          context={t('metrics.pipelineValueContext')}
        />
        <MetricCard icon={Target} label={t('metrics.winRate')} value="34,6%" progress={34.6} />
        <MetricCard
          icon={Timer}
          label={t('metrics.avgCycle')}
          value={t('metrics.avgCycleValue')}
          delta={{ value: t('metrics.avgCycleDelta'), direction: 'up' }}
          context={t('metrics.avgCycleContext')}
        />
        <MetricCard
          icon={Layers}
          label={t('metrics.openDeals')}
          value="38"
          context={t('metrics.openDealsContext')}
        />
      </div>

      {/* Grid + rail, at the artboard's own proportions: 266 sidebar + 56 page
          padding + 372 rail + 16 gap = 710px of chrome, leaving the grid 730px
          at the 1440px design width — what the artboard gives it. Below 1440
          the two cards stack under the grid instead of squeezing it. */}
      <div className="grid grid-cols-1 items-start gap-4 min-[1440px]:grid-cols-[minmax(0,1fr)_372px]">
        <Card className="gap-0 rounded-[var(--nx-r-panel)] p-0">
          {/* Card header: the panel's identity, and comfortable in both modes. */}
          <div className="border-b border-[var(--nx-hairline)] px-6 pt-4 pb-3">
            <h2 className="font-heading text-[15.5px] leading-5 font-[620] tracking-[-0.022em]">
              {t('openDeals.heading')}
            </h2>
            <p className="text-muted-foreground mt-0.5 text-[12.5px]">
              {visible.length === deals.length
                ? t('openDeals.summaryAll', { count: deals.length })
                : t('openDeals.summaryFiltered', { total: deals.length, visible: visible.length })}
            </p>

            {/* Toolbar: the first density region on the page. */}
            <Toolbar className="mt-3">
              <ToolbarSearch
                value={query}
                onChange={setQuery}
                placeholder={t('openDeals.searchPlaceholder')}
              />
              <Button variant="secondary">
                <SlidersHorizontal aria-hidden strokeWidth={1.7} />
                {t('openDeals.filter')}
              </Button>
              <ToolbarSpacer />
              <DensityToggle />
            </Toolbar>
          </div>

          <DealsTable deals={visible} isLoading={isLoading} />
        </Card>

        <div className="flex flex-col gap-4">
          <StageDistribution buckets={buckets} isLoading={stagesLoading} />
          <UpcomingActivities activities={activities} isLoading={activitiesLoading} />
        </div>
      </div>
    </div>
  )
}
