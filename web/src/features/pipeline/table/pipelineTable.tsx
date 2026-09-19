import {
  createColumnHelper,
  createPaginatedRowModel,
  createSortedRowModel,
  rowPaginationFeature,
  rowSelectionFeature,
  rowSortingFeature,
  sortFn_alphanumeric,
  sortFn_datetime,
  sortFn_text,
  tableFeatures,
} from '@tanstack/react-table'
import type { TFunction } from 'i18next'
import { StageBadge } from '@/components/common/StageBadge'
import {
  SelectAllHeaderCheckbox,
  SelectRowCheckbox,
  type DataTableColumnMeta,
} from '@/components/data-table'
import { formatCloseDate, money } from '@/features/pipeline/data/format'
import { initialsOf } from '@/lib/utils'
import type { Deal, Stage } from '@/types'
import { DealRowMenu } from '@/features/pipeline/components/DealRowMenu'

/**
 * The pipeline grid: selection + sorting + pagination.
 *
 * No `columnFilteringFeature` — the filter pills above the grid narrow the
 * array before it reaches the table, because two of them ("closes this
 * quarter", "at least ₺X") are predicates over the row, not over one column.
 * Defined at module scope so the table never sees a new feature set.
 */
export const pipelineFeatures = tableFeatures({
  rowSelectionFeature,

  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
  sortFns: {
    alphanumeric: sortFn_alphanumeric,
    text: sortFn_text,
    datetime: sortFn_datetime,
  },

  rowPaginationFeature,
  paginatedRowModel: createPaginatedRowModel(),

  columnMeta: {} as DataTableColumnMeta,
})

const helper = createColumnHelper<typeof pipelineFeatures, Deal>()

export interface PipelineRowActions {
  onChangeStage: (ids: string[], stage: Stage) => void
  onAssign: (ids: string[], owner: string) => void
  onRemove: (ids: string[]) => void
}

/**
 * Columns take the row actions as an argument rather than reaching for a
 * store, so the grid stays the only place that knows how a mutation is run.
 * `t` is likewise passed in (rather than read via a hook here) because this
 * is a plain factory, not a component — the caller's `useMemo` depends on it
 * so columns rebuild when the language changes.
 */
export function createPipelineColumns(actions: PipelineRowActions, t: TFunction<'pipeline'>) {
  return helper.columns([
    helper.display({
      id: 'select',
      // The only way out of a selection — never hidden, never sorted.
      meta: {
        label: t('table.selection'),
        align: 'center',
        headerClassName: 'w-11',
        cellClassName: 'w-11',
      },
      header: ({ table }) => <SelectAllHeaderCheckbox table={table} />,
      cell: ({ row }) => <SelectRowCheckbox row={row} />,
    }),
    helper.accessor('title', {
      header: t('table.opportunity'),
      sortFn: 'text',
      meta: {
        label: t('table.opportunity'),
        cellClassName: 'text-foreground font-[550] tracking-[-0.012em]',
      },
    }),
    helper.accessor('account', {
      header: t('table.customer'),
      sortFn: 'text',
      meta: { label: t('table.customer') },
    }),
    helper.accessor('owner', {
      header: t('table.owner'),
      sortFn: 'text',
      meta: { label: t('table.owner') },
      cell: (info) => (
        <span className="inline-flex items-center gap-2">
          <span aria-hidden className="nx-avatar size-6 text-[10px]">
            {initialsOf(info.getValue())}
          </span>
          {info.getValue()}
        </span>
      ),
    }),
    helper.accessor('stage', {
      header: t('table.stage'),
      enableSorting: false,
      meta: { label: t('table.stage'), align: 'right' },
      cell: (info) => <StageBadge stage={info.getValue()} />,
    }),
    helper.accessor('probability', {
      header: t('table.probability'),
      sortFn: 'alphanumeric',
      meta: { label: t('table.probability'), align: 'right' },
      cell: (info) => `%${info.getValue()}`,
    }),
    helper.accessor('value', {
      header: t('table.value'),
      sortFn: 'alphanumeric',
      meta: {
        label: t('table.value'),
        align: 'right',
        cellClassName: 'text-foreground font-[550]',
      },
      cell: (info) => money.format(info.getValue()),
    }),
    helper.accessor('closeDate', {
      header: t('table.closeDate'),
      sortFn: 'datetime',
      meta: { label: t('table.closeDate'), align: 'right', cellClassName: 'font-[450]' },
      cell: (info) => formatCloseDate(info.getValue()),
    }),
    helper.display({
      id: 'actions',
      meta: {
        label: t('table.actions'),
        align: 'right',
        headerClassName: 'w-13',
        cellClassName: 'w-13',
      },
      header: () => <span className="sr-only">{t('table.actions')}</span>,
      cell: ({ row }) => <DealRowMenu deal={row.original} actions={actions} />,
    }),
  ])
}

export const getDealRowId = (deal: Deal) => deal.id
