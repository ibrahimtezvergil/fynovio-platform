import {
  createColumnHelper,
  createSortedRowModel,
  rowSelectionFeature,
  rowSortingFeature,
  sortFn_alphanumeric,
  sortFn_datetime,
  sortFn_text,
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
 * Selection + sorting over the loaded page. Paging and the status filter are the server's (`useOpportunityList`),
 * so there is no pagination feature here; sorting reorders the rows already on screen. Module scope, so the table
 * never sees a new feature set.
 */
export const opportunityFeatures = tableFeatures({
  rowSelectionFeature,

  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
  sortFns: {
    alphanumeric: sortFn_alphanumeric,
    text: sortFn_text,
    datetime: sortFn_datetime,
  },

  columnMeta: {} as DataTableColumnMeta,
})

const helper = createColumnHelper<typeof opportunityFeatures, OpportunityRow>()

/** `t` comes in as an argument (this is a factory, not a component) so the caller's `useMemo` rebuilds the columns when the language changes. */
export function createOpportunityColumns(t: TFunction<'opportunities'>) {
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
      sortFn: 'alphanumeric',
      meta: { label: t('list.columns.id') },
      cell: (info) => (
        <Link to={paths.crmOpportunity(info.getValue())} className="text-primary font-[590] tabular-nums hover:underline">
          #{info.getValue()}
        </Link>
      ),
    }),
    helper.accessor((row) => row.party ?? '', {
      id: 'party',
      header: t('list.columns.party'),
      sortFn: 'text',
      meta: { label: t('list.columns.party'), cellClassName: 'text-foreground font-[550] tracking-[-0.012em]' },
      cell: (info) => info.getValue() || (info.row.original.partyId == null ? '—' : t('list.partyId', { id: info.row.original.partyId })),
    }),
    helper.accessor((row) => row.owner ?? '', {
      id: 'owner',
      header: t('list.columns.owner'),
      sortFn: 'text',
      meta: { label: t('list.columns.owner'), cellClassName: 'max-w-[200px]' },
      cell: (info) => (info.getValue() ? <code className="text-[12px]">{info.getValue()}</code> : '—'),
    }),
    helper.accessor('status', {
      header: t('list.columns.status'),
      enableSorting: false,
      meta: { label: t('list.columns.status'), align: 'right' },
      cell: (info) => <OpportunityStatusBadge status={info.getValue()} size="sm" />,
    }),
    helper.accessor((row) => row.stage ?? '', {
      id: 'stage',
      header: t('list.columns.stage'),
      sortFn: 'text',
      meta: { label: t('list.columns.stage') },
      cell: (info) => info.getValue() || (info.row.original.stageId == null ? '—' : t('list.stageId', { id: info.row.original.stageId })),
    }),
    helper.accessor((row) => row.amount ?? 0, {
      id: 'amount',
      header: t('list.columns.amount'),
      sortFn: 'alphanumeric',
      meta: { label: t('list.columns.amount'), align: 'right', cellClassName: 'text-foreground font-[550]' },
      cell: (info) => formatMoney(info.row.original.amount, info.row.original.currency) ?? '—',
    }),
    helper.accessor('expiryDate', {
      header: t('list.columns.expiry'),
      sortFn: 'datetime',
      meta: { label: t('list.columns.expiry'), align: 'right', cellClassName: 'font-[450]' },
      cell: (info) => formatDate(info.getValue()) ?? '—',
    }),
  ])
}

export const getOpportunityRowId = (row: OpportunityRow) => String(row.id)
