import {
  CalendarClock,
  CalendarRange,
  ClipboardCheck,
  Coins,
  Fingerprint,
  ListChecks,
  Network,
  Paperclip,
  PenLine,
  Table2,
  Type,
} from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { PageHeader } from '@/components/common/PageHeader'
import { SectionNav, type NavSection } from '@/components/common/SectionNav'
import { Badge } from '@/components/ui/badge'
import { AdvancedSelectSection } from '@/features/demo-forms/components/AdvancedSelectSection'
import { ChoiceSection } from '@/features/demo-forms/components/ChoiceSection'
import { DateSection } from '@/features/demo-forms/components/DateSection'
import { DemoSection } from '@/components/common/DemoSection'
import { EditorSection } from '@/features/demo-forms/components/EditorSection'
import { IdentitySection } from '@/features/demo-forms/components/IdentitySection'
import { InstallmentPlanEditor } from '@/features/demo-forms/components/InstallmentPlanEditor'
import { LineItemsEditor } from '@/features/demo-forms/components/LineItemsEditor'
import { MediaSection } from '@/features/demo-forms/components/MediaSection'
import { MoneySection } from '@/features/demo-forms/components/MoneySection'
import { QuoteFormDemo } from '@/features/demo-forms/components/QuoteFormDemo'
import { TextSection } from '@/features/demo-forms/components/TextSection'
import { ImportWizard } from '@/components/common/ImportWizard'
import { quoteSchema } from '@/features/demo-forms/schema'

/**
 * The control gallery: every input the CRM and ERP screens are built from, on
 * one page, each with the value it actually emits printed underneath. The last
 * two sections are the point of the first six — a quote's line table and a
 * validated form, assembled entirely from the controls above.
 */
export default function FormsDemoPage() {
  const { t } = useTranslation('demo-forms')

  const SECTIONS: readonly NavSection[] = [
    { id: 'metin', label: t('page.sections.metin'), icon: Type },
    { id: 'sayisal', label: t('page.sections.sayisal'), icon: Coins },
    { id: 'kimlik', label: t('page.sections.kimlik'), icon: Fingerprint },
    { id: 'secim', label: t('page.sections.secim'), icon: ListChecks },
    { id: 'gelismis-secim', label: t('page.sections.gelismisSecim'), icon: Network },
    { id: 'tarih', label: t('page.sections.tarih'), icon: CalendarRange },
    { id: 'editor', label: t('page.sections.editor'), icon: PenLine },
    { id: 'medya', label: t('page.sections.medya'), icon: Paperclip },
    { id: 'satirlar', label: t('page.sections.satirlar'), icon: Table2 },
    { id: 'taksit', label: t('page.sections.taksit'), icon: CalendarClock },
    { id: 'form', label: t('page.sections.form'), icon: ClipboardCheck },
  ]

  return (
    <div className="mx-auto flex w-full max-w-[1320px] flex-col gap-5">
      <PageHeader
        eyebrow={t('page.eyebrow')}
        title={t('page.title')}
        description={t('page.description')}
        actions={<Badge variant="secondary">{t('page.badge', { count: SECTIONS.length })}</Badge>}
      />

      <div className="grid grid-cols-1 items-start gap-5 lg:grid-cols-[236px_minmax(0,1fr)]">
        <SectionNav sections={SECTIONS} label={t('page.navLabel')} />

        <div className="flex min-w-0 flex-col gap-4">
          <TextSection />
          <MoneySection />
          <IdentitySection />
          <ChoiceSection />
          <AdvancedSelectSection />
          <DateSection />
          <EditorSection />
          <MediaSection />

          <DemoSection
            id="satirlar"
            title={t('page.lineItems.title')}
            description={t('page.lineItems.description')}
            icon={Table2}
            columns={1}
          >
            <LineItemsEditor />
          </DemoSection>

          <DemoSection
            id="taksit"
            title={t('page.installments.title')}
            description={t('page.installments.description')}
            icon={CalendarClock}
            columns={1}
          >
            <InstallmentPlanEditor />
          </DemoSection>

          <QuoteFormDemo />
          <DemoSection id="ice-aktar" title="CSV içe aktarma" description="Yükleme, kolon eşleme ve Zod doğrulama akışı." icon={Table2} columns={1}>
            <ImportWizard schema={quoteSchema} fields={['customer', 'taxId', 'phone', 'amount', 'currency', 'terms']} onCommit={() => undefined} />
          </DemoSection>
        </div>
      </div>
    </div>
  )
}
