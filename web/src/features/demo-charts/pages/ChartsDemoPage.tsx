import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { PageHeader } from '@/components/common/PageHeader'
import { SegmentedControl, type Segment } from '@/components/common/SegmentedControl'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { ConversionTrendChart } from '@/features/demo-charts/components/crm/ConversionTrendChart'
import { LeadSourceChart } from '@/features/demo-charts/components/crm/LeadSourceChart'
import { OutcomesChart } from '@/features/demo-charts/components/crm/OutcomesChart'
import { PipelineFunnelChart } from '@/features/demo-charts/components/crm/PipelineFunnelChart'
import { QuotaMeterCard } from '@/features/demo-charts/components/crm/QuotaMeterCard'
import { RepPerformanceChart } from '@/features/demo-charts/components/crm/RepPerformanceChart'
import { RevenueTrendChart } from '@/features/demo-charts/components/crm/RevenueTrendChart'
import { CashFlowChart } from '@/features/demo-charts/components/erp/CashFlowChart'
import { LeadTimeChart } from '@/features/demo-charts/components/erp/LeadTimeChart'
import { ProductionScatterChart } from '@/features/demo-charts/components/erp/ProductionScatterChart'
import { StockAgingChart } from '@/features/demo-charts/components/erp/StockAgingChart'
import { SupplierRadarChart } from '@/features/demo-charts/components/erp/SupplierRadarChart'
import { WarehouseStockChart } from '@/features/demo-charts/components/erp/WarehouseStockChart'

type Tab = 'crm' | 'erp'

/**
 * The chart gallery: the forms the CRM and the ERP screens report with, each
 * one built from the same `ChartContainer` + recharts pair and the same three
 * series colours. Data is deterministic mock data under `data/`; swapping in a
 * query changes the import, not the chart.
 */
export default function ChartsDemoPage() {
  const { t } = useTranslation('demo-charts')
  const [searchParams, setSearchParams] = useSearchParams()
  const tab: Tab = searchParams.get('tab') === 'erp' ? 'erp' : 'crm'

  const tabs: readonly Segment<Tab>[] = [
    { value: 'crm', label: t('page.tabs.crm') },
    { value: 'erp', label: t('page.tabs.erp') },
  ]

  const rules = t('page.rules', { returnObjects: true }) as [string, string][]

  const setTab = (next: Tab) => {
    setSearchParams(
      (prev) => {
        const params = new URLSearchParams(prev)
        if (next === 'crm') params.delete('tab')
        else params.set('tab', next)
        return params
      },
      { replace: true },
    )
  }

  return (
    <div className="mx-auto flex w-full max-w-[1320px] flex-col gap-5">
      <PageHeader
        eyebrow={t('page.eyebrow')}
        title={t('page.title')}
        description={t('page.description')}
        actions={
          <div className="flex items-center gap-2.5">
            <Badge variant="secondary">{t('page.badgeSummary')}</Badge>
            <SegmentedControl aria-label={t('page.moduleAriaLabel')} segments={tabs} value={tab} onChange={setTab} />
          </div>
        }
      />

      {tab === 'crm' ? (
        <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
          <div className="xl:col-span-2">
            <RevenueTrendChart />
          </div>
          <PipelineFunnelChart />
          <OutcomesChart />
          <LeadSourceChart />
          <RepPerformanceChart />
          <ConversionTrendChart />
          <QuotaMeterCard />
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
          <div className="xl:col-span-2">
            <CashFlowChart />
          </div>
          <WarehouseStockChart />
          <SupplierRadarChart />
          <LeadTimeChart />
          <ProductionScatterChart />
          <div className="xl:col-span-2">
            <StockAgingChart />
          </div>
        </div>
      )}

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
  )
}
