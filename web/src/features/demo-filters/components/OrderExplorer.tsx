import { ChevronDown, Inbox, SlidersHorizontal } from 'lucide-react'
import { useId, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { EmptyState } from '@/components/common/EmptyState'
import { SearchInput } from '@/components/common/inputs'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { ActiveFilterChips } from '@/features/demo-filters/components/ActiveFilterChips'
import { FilterPanel } from '@/features/demo-filters/components/FilterPanel'
import { ResultGrid } from '@/features/demo-filters/components/ResultGrid'
import { SavedViews } from '@/features/demo-filters/components/SavedViews'
import { SortMenu } from '@/features/demo-filters/components/SortMenu'
import { ORDER_RECORDS } from '@/features/demo-filters/data/records'
import { SAVED_VIEWS } from '@/features/demo-filters/data/views'
import { usePersonalViews } from '@/features/demo-filters/lib/personalViews'
import {
  applySort,
  formatMoney,
} from '@/features/demo-filters/lib/query'
import { applyFilterNodes, describeFilterNodes, filterStateToNodes } from '@/features/demo-filters/lib/filterAst'
import {
  EMPTY_FILTER,
  SORT_DESC_FIRST,
  type FilterState,
  type SavedView,
  type SortField,
  type SortRule,
} from '@/features/demo-filters/types'
import { cn } from '@/lib/utils'

const DEFAULT_SORT: SortRule[] = [{ field: 'createdAt', direction: 'desc' }]

/**
 * Filter bar, expandable panel, chip row and grid, wired to one query object.
 *
 * The state lives here rather than in a store on purpose: a filter is a
 * question about the current screen, and it dies with it. What outlives the
 * screen is the *saved view*, and that is data, not UI state.
 */
export function OrderExplorer() {
  const { t } = useTranslation('demo-filters')
  const panelId = useId()
  const [filter, setFilter] = useState<FilterState>(EMPTY_FILTER)
  const [rules, setRules] = useState<SortRule[]>(DEFAULT_SORT)
  const [panelOpen, setPanelOpen] = useState(false)
  const [viewId, setViewId] = useState<string | null>(SAVED_VIEWS[0]!.id)
  const personalViews = usePersonalViews()

  /** Any manual edit drops the saved-view badge — the query is no longer that view. */
  const patch = (next: Partial<FilterState>) => {
    setFilter((current) => ({ ...current, ...next }))
    setViewId(null)
  }

  const selectView = (view: SavedView) => {
    setFilter({ ...EMPTY_FILTER, ...view.filter })
    setRules(view.sort)
    setViewId(view.id)
  }

  const changeSort = (next: SortRule[]) => {
    setRules(next)
    setViewId(null)
  }

  /** Header click: replace the rules. Shift-click: append, or flip if present. */
  const sortByColumn = (field: SortField, additive: boolean) => {
    const existing = rules.find((rule) => rule.field === field)
    if (!additive) {
      changeSort([
        {
          field,
          direction: existing
            ? existing.direction === 'asc'
              ? 'desc'
              : 'asc'
            : SORT_DESC_FIRST[field]
              ? 'desc'
              : 'asc',
        },
      ])
      return
    }
    changeSort(
      existing
        ? rules.map((rule) =>
            rule.field === field
              ? { ...rule, direction: rule.direction === 'asc' ? 'desc' : 'asc' }
              : rule,
          )
        : [...rules, { field, direction: SORT_DESC_FIRST[field] ? 'desc' : 'asc' }],
    )
  }

  const filterNodes = useMemo(() => filterStateToNodes(filter), [filter])
  const chips = useMemo(() => describeFilterNodes(filterNodes), [filterNodes])
  const results = useMemo(
    () => applySort(applyFilterNodes(filterNodes, ORDER_RECORDS), rules),
    [filterNodes, rules],
  )
  const total = useMemo(() => results.reduce((sum, row) => sum + row.amount, 0), [results])

  return (
    <div className="flex min-w-0 flex-col gap-3.5">
      <SavedViews
        activeId={viewId}
        onSelect={selectView}
        personalViews={personalViews.views}
        onSaveCurrent={(label) => setViewId(personalViews.save(label, filter, rules).id)}
        onRemovePersonal={(id) => {
          personalViews.remove(id)
          if (viewId === id) setViewId(null)
        }}
      />

      <div className="flex flex-wrap items-center gap-2.5">
        <SearchInput
          value={filter.search}
          onValueChange={(value) => patch({ search: value })}
          placeholder={t('explorer.searchPlaceholder')}
          aria-label={t('explorer.searchAria')}
          className="w-[320px] max-w-full"
        />

        <Button
          variant={chips.length > 0 ? 'tinted' : 'secondary'}
          size="sm"
          className="rounded-[var(--nx-r-pill)]"
          aria-expanded={panelOpen}
          aria-controls={panelId}
          onClick={() => setPanelOpen((open) => !open)}
        >
          <SlidersHorizontal aria-hidden strokeWidth={1.7} />
          {t('explorer.filtersButton')}
          {chips.length > 0 && <Badge variant="secondary">{chips.length}</Badge>}
          <ChevronDown
            aria-hidden
            strokeWidth={1.7}
            className={cn('transition-transform duration-[250ms] ease-fluid', panelOpen && 'rotate-180')}
          />
        </Button>

        <SortMenu rules={rules} onChange={changeSort} />

        <div className="flex-1" />

        <p className="text-muted-foreground text-[12px]">
          <span className="tnum text-foreground font-[590]">{results.length}</span>
          <span className="tnum"> / {ORDER_RECORDS.length}</span> {t('explorer.recordsLabel')} ·{' '}
          <span className="tnum text-foreground font-[590]">{formatMoney(total)}</span>
        </p>
      </div>

      <ActiveFilterChips
        chips={chips}
        onRemove={patch}
        onClearAll={() => {
          setFilter(EMPTY_FILTER)
          setViewId(null)
        }}
      />

      {panelOpen && (
        <FilterPanel
          id={panelId}
          filter={filter}
          onPatch={patch}
          onClear={() => {
            setFilter(EMPTY_FILTER)
            setViewId(null)
          }}
        />
      )}

      {results.length === 0 ? (
        <div className="rounded-lg border border-[var(--nx-hairline)] bg-[var(--nx-fill)]">
          <EmptyState
            icon={Inbox}
            title={t('explorer.emptyTitle')}
            description={t('explorer.emptyDescription')}
            action={
              <Button
                onClick={() => {
                  setFilter(EMPTY_FILTER)
                  setViewId(SAVED_VIEWS[0]!.id)
                }}
              >
                {t('explorer.emptyAction')}
              </Button>
            }
          />
        </div>
      ) : (
        <ResultGrid records={results} rules={rules} onSort={sortByColumn} />
      )}
    </div>
  )
}
