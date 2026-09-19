import { GitCompareArrows, Inbox, LayoutGrid, Shapes, Timer } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { PageHeader } from '@/components/common/PageHeader'
import { SectionNav, type NavSection } from '@/components/common/SectionNav'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { ChoosingSection } from '@/features/demo-states/components/ChoosingSection'
import { EmptyStateSection } from '@/features/demo-states/components/EmptyStateSection'
import { IllustrationSection } from '@/features/demo-states/components/IllustrationSection'
import { InlineLoadingSection } from '@/features/demo-states/components/InlineLoadingSection'
import { SkeletonSection } from '@/features/demo-states/components/SkeletonSection'

/**
 * The two halves of "there is nothing to show yet": the wait, and the void
 * left when the wait ends with no data. They belong on one page because the
 * failure mode is shared — both get answered with a generic spinner or a
 * generic "no data", and both then leave the reader with no idea what to do.
 */
export default function StatesDemoPage() {
  const { t } = useTranslation('demo-states')

  const sections: readonly NavSection[] = [
    { id: 'secim', label: t('page.sections.choosing'), icon: GitCompareArrows },
    { id: 'illustrasyon', label: t('page.sections.illustrations'), icon: Shapes },
    { id: 'bos-durumlar', label: t('page.sections.emptyStates'), icon: Inbox },
    { id: 'iskelet', label: t('page.sections.skeleton'), icon: LayoutGrid },
    { id: 'kismi', label: t('page.sections.inlineLoading'), icon: Timer },
  ]

  const rules = t('page.rules', { returnObjects: true }) as [string, string][]

  return (
    <div className="mx-auto flex w-full max-w-[1320px] flex-col gap-5">
      <PageHeader
        eyebrow={t('page.eyebrow')}
        title={t('page.title')}
        description={t('page.description')}
        actions={
          <Badge variant="secondary">
            {t('page.summary', { count: sections.length, illustrations: 6 })}
          </Badge>
        }
      />

      <div className="grid grid-cols-1 items-start gap-5 lg:grid-cols-[236px_minmax(0,1fr)]">
        <SectionNav sections={sections} label={t('page.sectionsNavLabel')} />

        <div className="flex min-w-0 flex-col gap-4">
          <ChoosingSection />
          <IllustrationSection />
          <EmptyStateSection />
          <SkeletonSection />
          <InlineLoadingSection />

          <Card className="gap-3.5 px-5 pt-[19px] pb-5">
            <h2 className="font-heading text-[15.5px] leading-snug font-[620] tracking-[-0.022em]">
              {t('page.rulesHeading')}
            </h2>
            <dl className="grid gap-x-8 gap-y-3 sm:grid-cols-2">
              {rules.map(([term, detail]) => (
                <div key={term} className="flex flex-col gap-0.5">
                  <dt className="text-[13px] font-[590]">{term}</dt>
                  <dd className="text-muted-foreground text-[12.5px] leading-[1.5]">{detail}</dd>
                </div>
              ))}
            </dl>
          </Card>
        </div>
      </div>
    </div>
  )
}
