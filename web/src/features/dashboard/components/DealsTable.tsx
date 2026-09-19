import { Inbox } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { DensityScope } from '@/components/common/DensityScope'
import { EmptyState } from '@/components/common/EmptyState'
import { StageBadge } from '@/components/common/StageBadge'
import type { Deal } from '@/types'

import { Skeleton } from '@/components/ui/skeleton'
const money = new Intl.NumberFormat('tr-TR', {
  style: 'currency',
  currency: 'TRY',
  maximumFractionDigits: 0,
})
const day = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'short', year: 'numeric' })

interface DealsTableProps {
  deals: Deal[]
  isLoading?: boolean
}

/**
 * Alignment contract: text and entity names left; every number, currency
 * amount, percentage and status badge right.
 *
 * Row height, padding and type scale come from the density region this sits
 * in — 52px comfortable, 32px compact — so the component takes no density prop.
 */
export function DealsTable({ deals, isLoading }: DealsTableProps) {
  const { t } = useTranslation('dashboard')
  if (isLoading) {
    return (
      <div className="flex flex-col gap-1.5 p-4">
        {Array.from({ length: 6 }).map((_, i) => (
          <Skeleton key={i} className="h-[52px] rounded-md" />
        ))}
      </div>
    )
  }

  if (deals.length === 0) {
    return (
      <EmptyState
        icon={Inbox}
        title={t('dealsTable.emptyTitle')}
        description={t('dealsTable.emptyDescription')}
      />
    )
  }

  return (
    <DensityScope className="overflow-x-auto">
      <table className="nx-grid min-w-[700px]">
        <thead>
          <tr>
            <th scope="col">{t('dealsTable.opportunity')}</th>
            <th scope="col">{t('dealsTable.customer')}</th>
            <th scope="col">{t('dealsTable.owner')}</th>
            <th scope="col" className="end">
              {t('dealsTable.stage')}
            </th>
            <th scope="col" className="num">
              {t('dealsTable.value')}
            </th>
            <th scope="col" className="num">
              {t('dealsTable.closeDate')}
            </th>
          </tr>
        </thead>
        <tbody>
          {deals.map((deal) => (
            <tr key={deal.id}>
              <td className="name">{deal.title}</td>
              <td>{deal.account}</td>
              <td>{deal.owner}</td>
              <td className="end">
                <StageBadge stage={deal.stage} />
              </td>
              <td className="num">{money.format(deal.value)}</td>
              <td className="num">
                {deal.closeDate ? day.format(new Date(deal.closeDate)) : '—'}
              </td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <td colSpan={4} className="font-[550]">
              {t('dealsTable.total')}
            </td>
            <td className="num font-[650]">
              {money.format(deals.reduce((sum, d) => sum + d.value, 0))}
            </td>
            <td className="num text-muted-foreground">
              {t('dealsTable.dealCount', { count: deals.length })}
            </td>
          </tr>
        </tfoot>
      </table>
    </DensityScope>
  )
}
