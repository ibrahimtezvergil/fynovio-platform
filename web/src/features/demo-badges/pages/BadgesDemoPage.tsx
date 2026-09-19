import { Palette, Shapes, Table2, Tag } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { DemoSection } from '@/components/common/DemoSection'
import { PageHeader } from '@/components/common/PageHeader'
import { SectionNav, type NavSection } from '@/components/common/SectionNav'
import { StatusBadge } from '@/components/common/StatusBadge'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { InvoiceStatusGrid } from '@/features/demo-badges/components/InvoiceStatusGrid'
import { VocabularyCard } from '@/features/demo-badges/components/VocabularyCard'
import { useStatusRegistries, useToneSemantics, useVocabularies } from '@/features/demo-badges/data/registries'

/**
 * Colour-coded process state. Everything on this page is one primitive —
 * `src/components/common/StatusBadge.tsx` — plus the vocabularies that give
 * its eight tones their meaning per domain.
 */
export default function BadgesDemoPage() {
  const { t } = useTranslation('demo-badges')
  const vocabularies = useVocabularies()
  const toneSemantics = useToneSemantics()
  const status = useStatusRegistries()
  const totalStatuses = vocabularies.reduce((sum, entry) => sum + entry.entries.length, 0)

  const sections: readonly NavSection[] = [
    { id: 'ton-anlamlari', label: t('page.sections.toneMeanings'), icon: Palette },
    { id: 'soz-dagarciklari', label: t('page.sections.vocabularies'), icon: Tag },
    { id: 'bicimler', label: t('page.sections.formats'), icon: Shapes },
    { id: 'tabloda', label: t('page.sections.inTable'), icon: Table2 },
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
            {t('page.summary', { count: vocabularies.length, total: totalStatuses })}
          </Badge>
        }
      />

      <div className="grid grid-cols-1 items-start gap-5 lg:grid-cols-[236px_minmax(0,1fr)]">
        <SectionNav sections={sections} label={t('page.sections.toneMeanings')} />

        <div className="flex min-w-0 flex-col gap-4">
          <DemoSection
            id="ton-anlamlari"
            title={t('page.toneLadderTitle')}
            description={t('page.toneLadderDescription')}
            icon={Palette}
            columns={1}
          >
            <div className="overflow-hidden rounded-lg border border-[var(--nx-hairline)]">
              <table className="nx-grid">
                <thead>
                  <tr>
                    <th scope="col">{t('page.toneTable.tone')}</th>
                    <th scope="col">{t('page.toneTable.when')}</th>
                    <th scope="col">{t('page.toneTable.examples')}</th>
                  </tr>
                </thead>
                <tbody>
                  {toneSemantics.map(([tone, meaning, examples]) => (
                    <tr key={tone}>
                      <td>
                        <StatusBadge label={tone} tone={tone} />
                      </td>
                      <td className="name">{meaning}</td>
                      <td>{examples}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </DemoSection>

          <DemoSection
            id="soz-dagarciklari"
            title={t('page.vocabTitle')}
            description={t('page.vocabDescription')}
            icon={Tag}
          >
            {vocabularies.map((vocabulary) => (
              <VocabularyCard key={vocabulary.id} vocabulary={vocabulary} />
            ))}
          </DemoSection>

          <DemoSection
            id="bicimler"
            title={t('page.formatsTitle')}
            description={t('page.formatsDescription')}
            icon={Shapes}
          >
            <div className="flex flex-col gap-3 rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-4">
              <p className="text-[13px] font-[590]">{t('page.dotTitle')}</p>
              <p className="text-muted-foreground -mt-2 text-[11.5px] leading-4">{t('page.dotDescription')}</p>
              <div className="flex flex-wrap gap-1.5">
                <StatusBadge {...status.invoice.pending} />
                <StatusBadge {...status.invoice.paid} />
                <StatusBadge {...status.invoice.overdue} />
                <StatusBadge {...status.invoice.draft} />
              </div>
            </div>

            <div className="flex flex-col gap-3 rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-4">
              <p className="text-[13px] font-[590]">{t('page.iconTitle')}</p>
              <p className="text-muted-foreground -mt-2 text-[11.5px] leading-4">{t('page.iconDescription')}</p>
              <div className="flex flex-wrap gap-1.5">
                <StatusBadge {...status.approval.waiting} />
                <StatusBadge {...status.approval.approved} />
                <StatusBadge {...status.approval.rejected} />
                <StatusBadge {...status.approval.revision} />
              </div>
            </div>

            <div className="flex flex-col gap-3 rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-4">
              <p className="text-[13px] font-[590]">{t('page.smTitle')}</p>
              <p className="text-muted-foreground -mt-2 text-[11.5px] leading-4">{t('page.smDescription')}</p>
              <div className="flex flex-wrap items-center gap-1.5">
                <StatusBadge {...status.invoice.partial} size="sm" />
                <StatusBadge {...status.approval.approved} size="sm" />
                <StatusBadge label={t('page.urgent')} tone="red" size="sm" />
                <span className="text-[var(--nx-label-3)] ml-1 text-[11px]">{t('page.smHint')}</span>
                <StatusBadge {...status.invoice.partial} />
              </div>
            </div>

            <div className="flex flex-col gap-3 rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)] p-4">
              <p className="text-[13px] font-[590]">{t('page.outlineTitle')}</p>
              <p className="text-muted-foreground -mt-2 text-[11.5px] leading-4">{t('page.outlineDescription')}</p>
              <div className="flex flex-wrap gap-1.5">
                <StatusBadge label={t('page.outlineDiscontinued')} tone="outline" />
                <StatusBadge label={t('page.outlineOutOfScope')} tone="outline" />
                <StatusBadge label={t('page.outlineArchive')} tone="outline" />
              </div>
            </div>
          </DemoSection>

          <DemoSection
            id="tabloda"
            title={t('page.tableTitle')}
            description={t('page.tableDescription')}
            icon={Table2}
            columns={1}
          >
            <InvoiceStatusGrid />
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
