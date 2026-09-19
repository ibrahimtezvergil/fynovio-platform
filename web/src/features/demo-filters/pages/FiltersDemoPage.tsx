import { ArrowUpDown, Funnel, ListFilter, Tags } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { DemoSection } from '@/components/common/DemoSection'
import { PageHeader } from '@/components/common/PageHeader'
import { SectionNav, type NavSection } from '@/components/common/SectionNav'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { OrderExplorer } from '@/features/demo-filters/components/OrderExplorer'
import { ORDER_RECORDS } from '@/features/demo-filters/data/records'
import { SAVED_VIEWS } from '@/features/demo-filters/data/views'

/**
 * A complex filter is three surfaces working as one: a bar that is always
 * there, a panel that isn't, and a chip row that reports what the other two
 * did. Take away the chips and the panel becomes a place where lists go to
 * shrink for reasons nobody can see.
 */
export default function FiltersDemoPage() {
  const { t } = useTranslation('demo-filters')

  const sections: readonly NavSection[] = [
    { id: 'filtre-modulu', label: t('page.sections.filterModule'), icon: Funnel },
    { id: 'katmanlar', label: t('page.sections.layers'), icon: ListFilter },
    { id: 'siralama', label: t('page.sections.sorting'), icon: ArrowUpDown },
    { id: 'gorunumler', label: t('page.sections.views'), icon: Tags },
  ]

  const layers = t('page.layers', { returnObjects: true }) as [string, string][]
  const rules = t('page.rules', { returnObjects: true }) as [string, string][]

  return (
    <div className="mx-auto flex w-full max-w-[1320px] flex-col gap-5">
      <PageHeader
        eyebrow={t('page.eyebrow')}
        title={t('page.title')}
        description={t('page.description')}
        actions={
          <Badge variant="secondary">
            {t('page.summary', { count: ORDER_RECORDS.length, views: SAVED_VIEWS.length })}
          </Badge>
        }
      />

      <div className="grid grid-cols-1 items-start gap-5 lg:grid-cols-[236px_minmax(0,1fr)]">
        <SectionNav sections={sections} label={t('page.sectionsNavLabel')} />

        <div className="flex min-w-0 flex-col gap-4">
          <DemoSection
            id="filtre-modulu"
            title={t('page.filterModuleTitle')}
            description={t('page.filterModuleDescription')}
            icon={Funnel}
            columns={1}
          >
            <OrderExplorer />
          </DemoSection>

          <DemoSection
            id="katmanlar"
            title={t('page.layersTitle')}
            description={t('page.layersDescription')}
            icon={ListFilter}
            columns={1}
          >
            <dl className="grid gap-x-8 gap-y-3 sm:grid-cols-3">
              {layers.map(([term, detail]) => (
                <div key={term} className="flex flex-col gap-0.5">
                  <dt className="text-[13px] font-[590]">{term}</dt>
                  <dd className="text-muted-foreground text-[12.5px] leading-[1.5]">{detail}</dd>
                </div>
              ))}
            </dl>
          </DemoSection>

          <DemoSection
            id="siralama"
            title={t('page.sortingTitle')}
            description={t('page.sortingDescription')}
            icon={ArrowUpDown}
          >
            <div className="flex flex-col gap-2 rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-4">
              <p className="text-[13px] font-[590]">{t('page.sortingCard1Title')}</p>
              <p className="text-muted-foreground text-[12.5px] leading-[1.5]">
                {t('page.sortingCard1Body')}
              </p>
            </div>
            <div className="flex flex-col gap-2 rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-4">
              <p className="text-[13px] font-[590]">{t('page.sortingCard2Title')}</p>
              <p className="text-muted-foreground text-[12.5px] leading-[1.5]">
                {t('page.sortingCard2Body')}
              </p>
            </div>
          </DemoSection>

          <DemoSection
            id="gorunumler"
            title={t('page.viewsTitle')}
            description={t('page.viewsDescription')}
            icon={Tags}
          >
            {SAVED_VIEWS.map((view) => (
              <div
                key={view.id}
                className="flex flex-col gap-1.5 rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-4"
              >
                <p className="text-[13px] font-[590]">{view.label}</p>
                <p className="text-muted-foreground text-[12px] leading-[1.5]">
                  {view.description}
                </p>
                <code className="text-brand-graphic mt-0.5 rounded-sm bg-[var(--nx-tint-fill)] px-1.5 py-1 font-mono text-[10.5px] break-all">
                  {view.sort.map((rule) => `${rule.field} ${rule.direction}`).join(' · ') ||
                    t('page.viewsNoSort')}
                </code>
              </div>
            ))}
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
