import { ArrowDown, ArrowUp, ChevronsUpDown } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { StatusBadge } from '@/components/common/StatusBadge'
import { formatDay, formatMoney } from '@/features/demo-filters/lib/query'
import {
  useOrderStatusMeta,
  useSortLabels,
  type OrderRecord,
  type SortField,
  type SortRule,
} from '@/features/demo-filters/types'
import { cn } from '@/lib/utils'

interface ResultGridProps {
  records: readonly OrderRecord[]
  rules: readonly SortRule[]
  /** `additive` comes from shift-click: append the rule instead of replacing. */
  onSort: (field: SortField, additive: boolean) => void
}

/**
 * The grid the filters narrow.
 *
 * Sortable headers and the sort menu are two doors to one state: clicking a
 * header replaces the rules, shift-clicking appends one, and either way the
 * menu shows the result. A header that sorted independently of the menu would
 * be a second, invisible sort order.
 */
export function ResultGrid({ records, rules, onSort }: ResultGridProps) {
  const { t } = useTranslation('demo-filters')
  const orderStatus = useOrderStatusMeta()
  const sortLabels = useSortLabels()

  const columns: { field: SortField | null; label: string; align?: 'num' | 'end' }[] = [
    { field: null, label: t('grid.order') },
    { field: 'account', label: t('grid.account') },
    { field: null, label: t('grid.owner') },
    { field: 'createdAt', label: sortLabels.createdAt, align: 'end' },
    { field: 'items', label: sortLabels.items, align: 'num' },
    { field: 'amount', label: sortLabels.amount, align: 'num' },
    { field: 'status', label: sortLabels.status, align: 'end' },
  ]

  const ruleFor = (field: SortField) => rules.find((rule) => rule.field === field)

  return (
    <div className="overflow-x-auto rounded-lg border border-[var(--nx-hairline)]">
      {/* Eight columns is one more than the grid's default 16px gutters fit on
          a 1016px card, and the status column is the one that would fall off
          the edge. Tightening the gutters keeps it on screen without dropping
          a column the sort menu needs. */}
      <table className="nx-grid [&_td]:whitespace-nowrap [&_td]:px-3 [&_th]:px-3 [&_td:first-child]:pl-5 [&_th:first-child]:pl-5 [&_td:last-child]:pr-5 [&_th:last-child]:pr-5">
        <caption className="sr-only">{t('grid.caption')}</caption>
        <thead>
          <tr>
            {columns.map((column) => {
              const rule = column.field ? ruleFor(column.field) : undefined
              const priority = rule ? rules.indexOf(rule) + 1 : 0
              return (
                <th
                  key={column.label}
                  scope="col"
                  className={column.align}
                  aria-sort={
                    rule ? (rule.direction === 'asc' ? 'ascending' : 'descending') : undefined
                  }
                >
                  {column.field ? (
                    <button
                      type="button"
                      onClick={(event) => onSort(column.field!, event.shiftKey)}
                      aria-label={t('grid.sortByField', { field: column.label })}
                      className={cn(
                        'inline-flex items-center gap-1 rounded-sm transition-colors hover:text-foreground',
                        column.align && 'flex-row-reverse',
                        rule && 'text-[var(--nx-tint)]',
                      )}
                    >
                      {column.label}
                      {rule ? (
                        rule.direction === 'asc' ? (
                          <ArrowUp aria-hidden className="size-3" strokeWidth={2.4} />
                        ) : (
                          <ArrowDown aria-hidden className="size-3" strokeWidth={2.4} />
                        )
                      ) : (
                        <ChevronsUpDown
                          aria-hidden
                          className="size-3 opacity-45"
                          strokeWidth={2}
                        />
                      )}
                      {rules.length > 1 && priority > 0 && (
                        <span className="tnum text-[9px] font-[700]">{priority}</span>
                      )}
                    </button>
                  ) : (
                    column.label
                  )}
                </th>
              )
            })}
          </tr>
        </thead>
        <tbody>
          {records.map((record) => (
            <tr key={record.id}>
              <td className="name font-mono text-[12.5px]">{record.id}</td>
              <td className="name">{record.account}</td>
              <td>{record.owner}</td>
              <td className="end tnum">{formatDay(record.createdAt)}</td>
              <td className="num">{record.items}</td>
              <td className="num">{formatMoney(record.amount)}</td>
              <td className="end">
                <StatusBadge {...orderStatus[record.status]} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
