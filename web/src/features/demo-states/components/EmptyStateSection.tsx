import { FilterX, Inbox, Plus, RotateCw, Send, ShieldCheck, Wifi } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { DemoSection } from '@/components/common/DemoSection'
import { EmptyState } from '@/components/common/EmptyState'
import {
  AllDoneIllustration,
  BrokenIllustration,
  EmptyBoxIllustration,
  NoAccessIllustration,
  NoResultsIllustration,
  OfflineIllustration,
} from '@/components/common/illustrations'
import { Button } from '@/components/ui/button'
import { StateCard } from '@/features/demo-states/components/StateCard'

/**
 * The six illustrated states, each with the action its cause implies. Tone is
 * carried on the wrapper — the drawings are `currentColor`, so the same line
 * art reads neutral in an empty collection and red in a failed request
 * without a second asset.
 */
export function EmptyStateSection() {
  const { t } = useTranslation('demo-states')

  return (
    <DemoSection
      id="bos-durumlar"
      title={t('emptyStates.title')}
      description={t('emptyStates.description')}
      icon={Inbox}
    >
      <StateCard
        title={t('emptyStates.firstEmpty.cardTitle')}
        description={t('emptyStates.firstEmpty.cardDescription')}
      >
        <EmptyState
          illustration={<EmptyBoxIllustration />}
          title={t('emptyStates.firstEmpty.title')}
          description={t('emptyStates.firstEmpty.description')}
          action={
            <Button size="sm">
              <Plus aria-hidden />
              {t('emptyStates.firstEmpty.action')}
            </Button>
          }
        />
      </StateCard>

      <StateCard
        title={t('emptyStates.filteredEmpty.cardTitle')}
        description={t('emptyStates.filteredEmpty.cardDescription')}
      >
        <EmptyState
          illustration={<NoResultsIllustration />}
          title={t('emptyStates.filteredEmpty.title')}
          description={t('emptyStates.filteredEmpty.description')}
          action={
            <Button size="sm" variant="outline">
              <FilterX aria-hidden />
              {t('emptyStates.filteredEmpty.action')}
            </Button>
          }
        />
      </StateCard>

      <StateCard title={t('emptyStates.error.cardTitle')} description={t('emptyStates.error.cardDescription')}>
        <EmptyState
          tone="danger"
          illustration={<BrokenIllustration />}
          title={t('emptyStates.error.title')}
          description={t('emptyStates.error.description')}
          action={
            <Button size="sm" variant="outline">
              <RotateCw aria-hidden />
              {t('emptyStates.error.action')}
            </Button>
          }
        />
      </StateCard>

      <StateCard title={t('emptyStates.offline.cardTitle')} description={t('emptyStates.offline.cardDescription')}>
        <EmptyState
          tone="warning"
          illustration={<OfflineIllustration />}
          title={t('emptyStates.offline.title')}
          description={t('emptyStates.offline.description')}
          action={
            <Button size="sm" variant="outline">
              <Wifi aria-hidden />
              {t('emptyStates.offline.action')}
            </Button>
          }
        />
      </StateCard>

      <StateCard title={t('emptyStates.noAccess.cardTitle')} description={t('emptyStates.noAccess.cardDescription')}>
        <EmptyState
          illustration={<NoAccessIllustration />}
          title={t('emptyStates.noAccess.title')}
          description={t('emptyStates.noAccess.description')}
          action={
            <Button size="sm" variant="outline">
              <Send aria-hidden />
              {t('emptyStates.noAccess.action')}
            </Button>
          }
        />
      </StateCard>

      <StateCard title={t('emptyStates.allDone.cardTitle')} description={t('emptyStates.allDone.cardDescription')}>
        <EmptyState
          tone="success"
          illustration={<AllDoneIllustration />}
          title={t('emptyStates.allDone.title')}
          description={t('emptyStates.allDone.description')}
          action={
            <Button size="sm" variant="ghost">
              <ShieldCheck aria-hidden />
              {t('emptyStates.allDone.action')}
            </Button>
          }
        />
      </StateCard>
    </DemoSection>
  )
}
