import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Skeleton } from '@/components/ui/skeleton'
import { paths } from '@/routes/paths'
import { formatMoney } from '../lib/format'
import type { OpportunitySummary } from '../schema'
import { OpportunityStatusBadge } from './OpportunityStatusBadge'

interface OpportunityTableProps {
  items: readonly OpportunitySummary[] | undefined
  /** Dims the rows during a background refetch instead of blanking them. */
  refreshing: boolean
}

/**
 * Read-only by design: the list contract carries no `rowVersion`, party or pipeline version, so a row cannot
 * issue a versioned command. Mutations start from the detail view.
 */
export function OpportunityTable({ items, refreshing }: OpportunityTableProps) {
  const { t } = useTranslation('opportunities')

  return (
    <div className="w-full overflow-x-auto">
      <table className="w-full text-[13px]" aria-busy={items === undefined || refreshing}>
        <caption className="sr-only">{t('list.title')}</caption>
        <thead>
          <tr className="text-muted-foreground border-b text-left text-[12px]">
            <th scope="col" className="px-3 py-2.5 font-[550]">{t('list.columns.id')}</th>
            <th scope="col" className="px-3 py-2.5 font-[550]">{t('list.columns.status')}</th>
            <th scope="col" className="px-3 py-2.5 text-right font-[550]">{t('list.columns.amount')}</th>
            <th scope="col" className="px-3 py-2.5 font-[550]">{t('list.columns.owner')}</th>
            <th scope="col" className="px-3 py-2.5 font-[550]">{t('list.columns.stage')}</th>
          </tr>
        </thead>
        <tbody className={refreshing ? 'opacity-60 transition-opacity' : undefined}>
          {items === undefined
            ? Array.from({ length: 6 }, (_, index) => (
                <tr key={index} className="border-b">
                  <td colSpan={5} className="px-3 py-2.5">
                    <Skeleton className="h-6 rounded-md" />
                  </td>
                </tr>
              ))
            : items.map((row) => (
                <tr key={row.id} className="hover:bg-muted/40 border-b transition-colors" data-testid="opportunity-row">
                  <td className="px-3 py-2.5 tabular-nums">
                    <Link to={paths.crmOpportunity(row.id)} className="text-primary font-[590] hover:underline">
                      #{row.id}
                    </Link>
                  </td>
                  <td className="px-3 py-2.5">
                    <OpportunityStatusBadge status={row.status} size="sm" />
                  </td>
                  <td className="px-3 py-2.5 text-right tabular-nums">{formatMoney(row.estimatedAmount, row.currency) ?? '—'}</td>
                  <td className="px-3 py-2.5">
                    <code className="text-[12px]">{row.assignedPrincipalSubject ?? '—'}</code>
                  </td>
                  <td className="text-muted-foreground px-3 py-2.5">
                    {row.pipelineStageId == null ? '—' : t('list.stageId', { id: row.pipelineStageId })}
                  </td>
                </tr>
              ))}
        </tbody>
      </table>
    </div>
  )
}
