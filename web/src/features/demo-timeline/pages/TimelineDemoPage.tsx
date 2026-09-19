import { History, PanelRight, ScrollText } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { DemoSection } from '@/components/common/DemoSection'
import { PageHeader } from '@/components/common/PageHeader'
import { SectionNav, type NavSection } from '@/components/common/SectionNav'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { ActivityComposer } from '@/features/demo-timeline/components/ActivityComposer'
import { CompactTimeline } from '@/features/demo-timeline/components/CompactTimeline'
import { FeedFilter } from '@/features/demo-timeline/components/FeedFilter'
import { TimelineFeed } from '@/features/demo-timeline/components/TimelineFeed'
import { AUDIT_FEED } from '@/features/demo-timeline/data/audit'
import { CUSTOMER_FEED } from '@/features/demo-timeline/data/customer'
import type { ActivityType, TimelineEntry } from '@/features/demo-timeline/types'

/**
 * The customer history and the audit trail are the same component with
 * different content weights: the CRM feed is mostly touchpoints with prose,
 * the ERP feed is mostly field changes with almost none. Keeping them on one
 * page is what stops the second one from growing its own timeline.
 */
export default function TimelineDemoPage() {
  const { t } = useTranslation('demo-timeline')
  const [filter, setFilter] = useState<ActivityType[]>([])
  const [extra, setExtra] = useState<TimelineEntry[]>([])

  const sections: readonly NavSection[] = useMemo(
    () => [
      { id: 'musteri-akisi', label: t('page.sections.customerHistory'), icon: History },
      { id: 'denetim-izi', label: t('page.sections.auditTrail'), icon: ScrollText },
      { id: 'yogun-akis', label: t('page.sections.compactFeed'), icon: PanelRight },
    ],
    [t],
  )

  const rules = t('page.rules', { returnObjects: true }) as [string, string][]

  const customerEntries = useMemo(() => [...extra, ...CUSTOMER_FEED], [extra])

  const availableTypes = useMemo(() => {
    const seen = new Set<ActivityType>()
    for (const entry of customerEntries) seen.add(entry.type)
    return [...seen]
  }, [customerEntries])

  const visible = useMemo(
    () =>
      filter.length === 0
        ? customerEntries
        : customerEntries.filter((entry) => filter.includes(entry.type)),
    [customerEntries, filter],
  )

  return (
    <div className="mx-auto flex w-full max-w-[1320px] flex-col gap-5">
      <PageHeader
        eyebrow={t('page.eyebrow')}
        title={t('page.title')}
        description={t('page.description')}
        actions={<Badge variant="secondary">{t('page.summary', { count: sections.length })}</Badge>}
      />

      <div className="grid grid-cols-1 items-start gap-5 lg:grid-cols-[236px_minmax(0,1fr)]">
        <SectionNav sections={sections} label={t('page.navLabel')} />

        <div className="flex min-w-0 flex-col gap-4">
          <DemoSection
            id="musteri-akisi"
            title={t('page.customerSection.title')}
            description={t('page.customerSection.description')}
            icon={History}
            columns={1}
          >
            <div className="flex flex-col gap-4">
              <ActivityComposer onSubmit={(entry) => setExtra((current) => [entry, ...current])} />

              <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
                <FeedFilter available={availableTypes} value={filter} onChange={setFilter} />
                <div className="flex-1" />
                <span className="text-muted-foreground tnum text-[11.5px]">
                  {t('page.visibleCount', { visible: visible.length, total: customerEntries.length })}
                </span>
              </div>

              <TimelineFeed
                entries={visible}
                emptyDescription={t('page.customerEmptyDescription')}
              />
            </div>
          </DemoSection>

          <DemoSection
            id="denetim-izi"
            title={t('page.auditSection.title')}
            description={t('page.auditSection.description')}
            icon={ScrollText}
            columns={1}
          >
            <TimelineFeed entries={AUDIT_FEED} showPinned={false} />
          </DemoSection>

          <DemoSection
            id="yogun-akis"
            title={t('page.compactSection.title')}
            description={t('page.compactSection.description')}
            icon={PanelRight}
          >
            <div className="rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-4">
              <p className="nx-eyebrow mb-2">{t('page.compactSection.recentHeading')}</p>
              <CompactTimeline entries={CUSTOMER_FEED.slice(0, 6)} />
            </div>
            <div className="rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-4">
              <p className="nx-eyebrow mb-2">{t('page.compactSection.auditHeading')}</p>
              <CompactTimeline entries={AUDIT_FEED} />
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
