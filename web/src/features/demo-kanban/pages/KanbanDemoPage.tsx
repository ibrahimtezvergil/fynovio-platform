import { Columns3, LayoutGrid, ListTodo, SquareKanban } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { PageHeader } from '@/components/common/PageHeader'
import { DemoSection } from '@/components/common/DemoSection'
import { SectionNav, type NavSection } from '@/components/common/SectionNav'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { KanbanBoard } from '@/features/demo-kanban/components/KanbanBoard'
import { CRM_CARDS, CRM_COLUMNS } from '@/features/demo-kanban/data/crm'
import { ERP_CARDS, ERP_COLUMNS } from '@/features/demo-kanban/data/erp'

/**
 * Two boards, one interaction model. The sales board is the pipeline turned
 * sideways — same stages, same order, same tones as the grid; the task board
 * is a process with WIP limits, which is the part a funnel never has.
 */
export default function KanbanDemoPage() {
  const { t } = useTranslation('demo-kanban')

  const sections: readonly NavSection[] = [
    { id: 'satis-panosu', label: t('page.sections.salesBoard'), icon: SquareKanban },
    { id: 'gorev-panosu', label: t('page.sections.taskBoard'), icon: ListTodo },
    { id: 'kart-anatomisi', label: t('page.sections.cardAnatomy'), icon: LayoutGrid },
  ]

  const cardSlots = t('page.cardSlots', { returnObjects: true }) as { term: string; detail: string }[]
  const rules = t('page.rules', { returnObjects: true }) as { term: string; detail: string }[]

  return (
    <div className="mx-auto flex w-full max-w-[1320px] flex-col gap-5">
      <PageHeader
        eyebrow={t('page.eyebrow')}
        title={t('page.title')}
        description={t('page.description')}
        actions={<Badge variant="secondary">{t('page.badge', { count: sections.length })}</Badge>}
      />

      <div className="grid grid-cols-1 items-start gap-5 lg:grid-cols-[236px_minmax(0,1fr)]">
        <SectionNav sections={sections} label={t('page.sectionNavLabel')} />

        <div className="flex min-w-0 flex-col gap-4">
          <DemoSection
            id="satis-panosu"
            title={t('page.sales.title')}
            description={t('page.sales.description')}
            icon={Columns3}
            columns={1}
          >
            <KanbanBoard
              columns={CRM_COLUMNS}
              cards={CRM_CARDS}
              metricLabel={t('page.sales.metricLabel')}
            />
          </DemoSection>

          <DemoSection
            id="gorev-panosu"
            title={t('page.task.title')}
            description={t('page.task.description')}
            icon={ListTodo}
            columns={1}
          >
            <KanbanBoard
              columns={ERP_COLUMNS}
              cards={ERP_CARDS}
              metricLabel={t('page.task.metricLabel')}
            />
          </DemoSection>

          <DemoSection
            id="kart-anatomisi"
            title={t('page.anatomy.title')}
            description={t('page.anatomy.description')}
            icon={LayoutGrid}
          >
            <dl className="lg:col-span-2 grid gap-x-8 gap-y-3 sm:grid-cols-2">
              {cardSlots.map(({ term, detail }) => (
                <div key={term} className="flex flex-col gap-0.5">
                  <dt className="text-[13px] font-[590]">{term}</dt>
                  <dd className="text-muted-foreground text-[12.5px] leading-[1.5]">{detail}</dd>
                </div>
              ))}
            </dl>
          </DemoSection>

          <Card className="gap-3.5 px-5 pt-[19px] pb-5">
            <h2 className="font-heading text-[15.5px] leading-snug font-[620] tracking-[-0.022em]">
              {t('page.rulesHeading')}
            </h2>
            <dl className="grid gap-x-8 gap-y-3 sm:grid-cols-2">
              {rules.map(({ term, detail }) => (
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
