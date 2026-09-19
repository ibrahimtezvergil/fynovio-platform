import { GitCompareArrows, Layers, PanelRight, Ruler, SquarePen } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { DemoSection } from '@/components/common/DemoSection'
import { PageHeader } from '@/components/common/PageHeader'
import { SectionNav, type NavSection } from '@/components/common/SectionNav'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { ChoosingSection } from '@/features/demo-drawers/components/ChoosingSection'
import { RecordExplorer } from '@/features/demo-drawers/components/RecordExplorer'
import { SizeSection } from '@/features/demo-drawers/components/SizeSection'
import { StackedSection } from '@/features/demo-drawers/components/StackedSection'

/**
 * The detail panel, end to end: a grid whose rows open one, prev/next through
 * the list, an editing variant with a dirty guard, and the layering rule that
 * keeps a stack of panels from forming.
 */
export default function DrawersDemoPage() {
  const { t } = useTranslation('demo-drawers')

  const sections: readonly NavSection[] = [
    { id: 'secim', label: t('page.sections.choosing'), icon: GitCompareArrows },
    { id: 'satir-detay', label: t('page.sections.rowToDetail'), icon: PanelRight },
    { id: 'olculer', label: t('page.sections.sizeAndSide'), icon: Ruler },
    { id: 'katmanli', label: t('page.sections.stacked'), icon: Layers },
  ]

  const rules = t('page.rules', { returnObjects: true }) as [string, string][]

  return (
    <div className="mx-auto flex w-full max-w-[1320px] flex-col gap-5">
      <PageHeader
        eyebrow={t('page.eyebrow')}
        title={t('page.title')}
        description={t('page.description')}
        actions={
          <Badge variant="secondary">{t('page.summary', { count: sections.length, surfaces: 4 })}</Badge>
        }
      />

      <div className="grid grid-cols-1 items-start gap-5 lg:grid-cols-[236px_minmax(0,1fr)]">
        <SectionNav sections={sections} label={t('page.sectionsNavLabel')} />

        <div className="flex min-w-0 flex-col gap-4">
          <ChoosingSection />

          <DemoSection
            id="satir-detay"
            title={t('page.rowToDetailTitle')}
            description={t('page.rowToDetailDescription')}
            icon={PanelRight}
            columns={1}
          >
            <RecordExplorer />
          </DemoSection>

          <SizeSection />
          <StackedSection />

          <DemoSection
            id="duzenleme-notu"
            title={t('page.readVsEditTitle')}
            description={t('page.readVsEditDescription')}
            icon={SquarePen}
          >
            <div className="flex flex-col gap-2 rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-4">
              <p className="text-[13px] font-[590]">{t('page.readPanelTitle')}</p>
              <p className="text-muted-foreground text-[12.5px] leading-[1.5]">
                {t('page.readPanelBody')}
              </p>
            </div>
            <div className="flex flex-col gap-2 rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-4">
              <p className="text-[13px] font-[590]">{t('page.editPanelTitle')}</p>
              <p className="text-muted-foreground text-[12.5px] leading-[1.5]">
                {t('page.editPanelBodyPre')}{' '}
                <span className="text-foreground font-[550]">{t('page.editPanelBackToPanel')}</span>{' '}
                {t('page.editPanelBodyMid')}{' '}
                <span className="text-foreground font-[550]">{t('page.editPanelDiscard')}</span>.{' '}
                {t('page.editPanelBodyPost')}
              </p>
            </div>
          </DemoSection>

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
