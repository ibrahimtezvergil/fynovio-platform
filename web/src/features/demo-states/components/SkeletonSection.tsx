import { LayoutGrid } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { DemoSection } from '@/components/common/DemoSection'
import { LoadingPreview } from '@/features/demo-states/components/LoadingPreview'
import { StateCard } from '@/features/demo-states/components/StateCard'
import {
  ChartLoaded,
  FormLoaded,
  ListLoaded,
  MetricGridLoaded,
  ProfileLoaded,
  TableLoaded,
  TextLoaded,
} from '@/features/demo-states/components/loaded'
import {
  ChartSkeleton,
  FormSkeleton,
  ListSkeleton,
  MetricGridSkeleton,
  ProfileSkeleton,
  TableSkeleton,
  TextSkeleton,
} from '@/features/demo-states/components/skeletons'

/**
 * Every skeleton next to the thing it stands in for, with "Tekrar" to replay
 * the swap. Watch the layout, not the shimmer: nothing should jump when the
 * data lands. A block that moves is a skeleton whose geometry is wrong.
 */
export function SkeletonSection() {
  const { t } = useTranslation('demo-states')

  return (
    <DemoSection
      id="iskelet"
      title={t('skeleton.title')}
      description={t('skeleton.description')}
      icon={LayoutGrid}
    >
      <StateCard
        title={t('skeleton.metricCard.title')}
        description={t('skeleton.metricCard.description')}
        wide
      >
        <LoadingPreview
          label={t('skeleton.labels.summaryMetrics')}
          skeleton={<MetricGridSkeleton />}
          content={<MetricGridLoaded />}
        />
      </StateCard>

      <StateCard
        title={t('skeleton.tableCard.title')}
        description={t('skeleton.tableCard.description')}
        wide
      >
        <LoadingPreview
          label={t('skeleton.labels.dealsList')}
          skeleton={<TableSkeleton />}
          content={<TableLoaded />}
        />
      </StateCard>

      <StateCard title={t('skeleton.listCard.title')} description={t('skeleton.listCard.description')}>
        <LoadingPreview
          label={t('skeleton.labels.upcomingActivities')}
          skeleton={<ListSkeleton />}
          content={<ListLoaded />}
        />
      </StateCard>

      <StateCard title={t('skeleton.chartCard.title')} description={t('skeleton.chartCard.description')}>
        <LoadingPreview
          label={t('skeleton.labels.weeklyChart')}
          skeleton={<ChartSkeleton />}
          content={<ChartLoaded />}
          duration={1700}
        />
      </StateCard>

      <StateCard title={t('skeleton.formCard.title')} description={t('skeleton.formCard.description')}>
        <LoadingPreview
          label={t('skeleton.labels.dealForm')}
          skeleton={<FormSkeleton />}
          content={<FormLoaded />}
        />
      </StateCard>

      <StateCard title={t('skeleton.profileCard.title')} description={t('skeleton.profileCard.description')}>
        <LoadingPreview
          label={t('skeleton.labels.userCard')}
          skeleton={<ProfileSkeleton />}
          content={<ProfileLoaded />}
          duration={1100}
        />
      </StateCard>

      <StateCard title={t('skeleton.textCard.title')} description={t('skeleton.textCard.description')}>
        <LoadingPreview
          label={t('skeleton.labels.descriptionText')}
          skeleton={<TextSkeleton />}
          content={<TextLoaded />}
        />
      </StateCard>
    </DemoSection>
  )
}
