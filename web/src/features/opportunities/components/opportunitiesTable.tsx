import {
  createColumnHelper,
  rowSelectionFeature,
  tableFeatures,
} from '@tanstack/react-table'
import type { TFunction } from 'i18next'
import { Link } from 'react-router-dom'
import { SelectAllHeaderCheckbox, SelectRowCheckbox, type DataTableColumnMeta } from '@/components/data-table'
import { paths } from '@/routes/paths'
import { formatDate, formatMoney } from '../lib/format'
import type { OpportunityRow } from '../lib/rows'
import { OpportunityStatusBadge } from './OpportunityStatusBadge'

/**
 * Selection over the loaded page. Sorting is intentionally unavailable until the list API can sort the full result
 * set; allowing it here would make a page-local reorder look like a complete-list order.
 */
export const opportunityFeatures = tableFeatures({
  rowSelectionFeature,

  columnMeta: {} as DataTableColumnMeta,
})

const helper = createColumnHelper<typeof opportunityFeatures, OpportunityRow>()

/** `t` comes in as an argument (this is a factory, not a component) so the caller's `useMemo` rebuilds the columns when the language changes. */
export function createOpportunityColumns(t: TFunction<'opportunities'>, returnTo: string) {
  return helper.columns([
    helper.display({
      id: 'select',
      // The only way out of a selection — never hidden, never sorted.
      meta: { label: t('list.grid.selection'), align: 'center', headerClassName: 'w-11', cellClassName: 'w-11' },
      header: ({ table }) => <SelectAllHeaderCheckbox table={table} />,
      cell: ({ row }) => <SelectRowCheckbox row={row} />,
    }),
    helper.accessor('id', {
      header: t('list.columns.id'),
      meta: { label: t('list.columns.id') },
      cell: (info) => (
        <Link to={paths.crmOpportunity(info.getValue(), returnTo)} className="text-primary font-[590] tabular-nums hover:underline">
          #{info.getValue()}
        </Link>
      ),
    }),
    helper.accessor((row) => row.party ?? '', {
      id: 'party',
      header: t('list.columns.party'),
      meta: { label: t('list.columns.party'), cellClassName: 'text-foreground font-[550] tracking-[-0.012em]' },
      cell: (info) => info.getValue() || (info.row.original.partyId == null ? '—' : t('list.partyId', { id: info.row.original.partyId })),
    }),
    helper.accessor((row) => row.owner ?? '', {
      id: 'owner',
      header: t('list.columns.owner'),
      meta: { label: t('list.columns.owner'), cellClassName: 'max-w-[200px]' },
      cell: (info) => (info.getValue() ? <code className="text-[12px]">{info.getValue()}</code> : '—'),
    }),
    helper.accessor('status', {
      header: t('list.columns.status'),
      meta: { label: t('list.columns.status'), align: 'right' },
      cell: (info) => <OpportunityStatusBadge status={info.getValue()} size="sm" />,
    }),
    helper.accessor((row) => row.stage ?? '', {
      id: 'stage',
      header: t('list.columns.stage'),
      meta: { label: t('list.columns.stage') },
      cell: (info) => info.getValue() || (info.row.original.stageId == null ? '—' : t('list.stageId', { id: info.row.original.stageId })),
    }),
    helper.accessor((row) => row.amount ?? 0, {
      id: 'amount',
      header: t('list.columns.amount'),
      meta: { label: t('list.columns.amount'), align: 'right', cellClassName: 'text-foreground font-[550]' },
      cell: (info) => formatMoney(info.row.original.amount, info.row.original.currency) ?? '—',
    }),
    helper.accessor('expiryDate', {
      header: t('list.columns.expiry'),
      meta: { label: t('list.columns.expiry'), align: 'right', cellClassName: 'font-[450]' },
      cell: (info) => formatDate(info.getValue()) ?? '—',
    }),
  ])
}

export const getOpportunityRowId = (row: OpportunityRow) => String(row.id)
