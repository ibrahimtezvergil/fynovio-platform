import { Inbox, Plus } from 'lucide-react'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useLocation, useSearchParams } from 'react-router-dom'
import { EmptyState } from '@/components/common/EmptyState'
import { PageHeader } from '@/components/common/PageHeader'
import { SegmentedControl, type Segment } from '@/components/common/SegmentedControl'
import { Button } from '@/components/ui/button'
import { paths } from '@/routes/paths'
import { usePartyNames, usePipelineStageNames, useOpportunityList, stageKey, type OpportunityListFilter } from '../api'
import { OpportunitiesBoard } from '../components/OpportunitiesBoard'
import { OpportunitiesFilters } from '../components/OpportunitiesFilters'
import { OpportunitiesGrid } from '../components/OpportunitiesGrid'
import { QueryProblemState } from '../components/QueryProblemState'
import { NO_ROW_FILTERS, filterRows, hasRowFilters, toRows, totalAmountLabel, type RowFilters } from '../lib/rows'
import { OPPORTUNITY_STATUSES, type OpportunityStatus } from '../schema'

type StatusFilter = OpportunityStatus | 'all'
type ListView = 'grid' | 'board'

/** The view rides in the URL, so a reload or a shared link lands on the same one; the grid is the default and stays out of it. */
const readView = (params: URLSearchParams): ListView => (params.get('view') === 'board' ? 'board' : 'grid')

function readFilter(params: URLSearchParams): OpportunityListFilter {
  const status = params.get('status')
  const page = Number(params.get('page') ?? 0)
  const archivedOnly = params.get('archive') === '1'
  return {
    status: OPPORTUNITY_STATUSES.filter((candidate) => !archivedOnly || candidate === 'Draft' || candidate === 'Open').find((candidate) => candidate === status),
    page: Number.isInteger(page) && page > 0 ? page : 0,
    archivedOnly,
  }
}

function readRowFilters(params: URLSearchParams): RowFilters {
  return {
    query: params.get('q') ?? '',
    owner: params.get('owner') ?? 'all',
    quarterOnly: params.get('quarter') === '1',
  }
}

export default function OpportunitiesPage() {
  const { t } = useTranslation('opportunities')
  const location = useLocation()
  const [searchParams, setSearchParams] = useSearchParams()
  const filter = readFilter(searchParams)
  const view = readView(searchParams)
  const list = useOpportunityList(filter)
  const rowFilters = readRowFilters(searchParams)

  const items = list.data?.items
  const partyIds = useMemo(() => [...new Set((items ?? []).flatMap((item) => (item.partyId == null ? [] : [item.partyId])))].toSorted((a, b) => a - b), [items])
  const versionIds = useMemo(() => [...new Set((items ?? []).flatMap((item) => (item.pipelineDefinitionVersionId == null ? [] : [item.pipelineDefinitionVersionId])))].toSorted((a, b) => a - b), [items])
  const partyNames = usePartyNames(partyIds).data
  const stageNames = usePipelineStageNames(versionIds)

  const loadedRows = useMemo(() => toRows(items ?? [], partyNames, (versionId, stageId) => stageNames.get(stageKey(versionId, stageId))), [items, partyNames, stageNames])
  const rows = useMemo(() => filterRows(loadedRows, rowFilters), [loadedRows, rowFilters])
  const owners = useMemo(() => [...new Set(loadedRows.flatMap((row) => (row.owner ? [row.owner] : [])))].toSorted(), [loadedRows])
  const total = totalAmountLabel(rows)

  const segments = useMemo<readonly Segment<StatusFilter>[]>(
    () => [{ value: 'all', label: t('list.filter.all') }, ...OPPORTUNITY_STATUSES.filter((status) => !filter.archivedOnly || status === 'Draft' || status === 'Open').map((status) => ({ value: status, label: t(`status.${status}`) }))],
    [t, filter.archivedOnly],
  )

  const viewSegments = useMemo<readonly Segment<ListView>[]>(
    () => [{ value: 'grid', label: t('list.view.grid') }, { value: 'board', label: t('list.view.board') }],
    [t],
  )

  const navigateTo = (next: OpportunityListFilter, nextView: ListView, nextRowFilters = rowFilters) => {
    const params = new URLSearchParams()
    if (next.status) params.set('status', next.status)
    if (next.page > 0) params.set('page', String(next.page))
    if (next.archivedOnly) params.set('archive', '1')
    if (nextView === 'board') params.set('view', 'board')
    if (nextRowFilters.query.trim()) params.set('q', nextRowFilters.query)
    if (nextRowFilters.owner !== 'all') params.set('owner', nextRowFilters.owner)
    if (nextRowFilters.quarterOnly) params.set('quarter', '1')
    setSearchParams(params)
  }
  const goTo = (next: OpportunityListFilter) => navigateTo(next, view)
  const changeRowFilters = (next: RowFilters) => navigateTo({ ...filter, page: 0 }, view, next)
  const returnTo = `${location.pathname}${location.search}`

  const createAction = (
    <Button render={<Link to={paths.crmOpportunityNew} />} nativeButton={false}>
      <Plus aria-hidden />
      {t('list.newAction')}
    </Button>
  )

  let body: React.ReactNode
  if (list.isError) {
    body = <QueryProblemState error={list.error} onRetry={() => void list.refetch()} retrying={list.isFetching} />
  } else if (list.data && list.data.items.length === 0) {
    body = (
      <EmptyState
        icon={Inbox}
        title={filter.archivedOnly ? t('list.archive.emptyTitle') : filter.status || filter.page > 0 ? t('list.empty.filteredTitle') : t('list.empty.title')}
        description={filter.archivedOnly ? t('list.archive.emptyDescription') : filter.status || filter.page > 0 ? t('list.empty.filteredDescription') : t('list.empty.description')}
        action={filter.archivedOnly ? undefined : filter.status || filter.page > 0 ? <Button variant="outline" onClick={() => goTo({ page: 0 })}>{t('list.empty.clearFilter')}</Button> : createAction}
      />
    )
  } else {
    body = (
      <>
        <OpportunitiesFilters filters={rowFilters} onChange={changeRowFilters} owners={owners} showDensity={view === 'grid'} />
        {view === 'grid' ? (
          <OpportunitiesGrid
            rows={rows}
            isLoading={list.data === undefined}
            refreshing={list.isFetching && list.data !== undefined}
            page={filter.page}
            hasNext={list.data?.hasNext ?? false}
            loadedCount={loadedRows.length}
            onPageChange={(page) => goTo({ ...filter, page })}
            returnTo={returnTo}
          />
        ) : (
          <OpportunitiesBoard
            rows={rows}
            refreshing={list.isFetching && list.data !== undefined}
            page={filter.page}
            hasNext={list.data?.hasNext ?? false}
            loadedCount={loadedRows.length}
            onPageChange={(page) => goTo({ ...filter, page })}
            returnTo={returnTo}
          />
        )}
        {hasRowFilters(rowFilters) && (
          <p className="text-muted-foreground text-[12.5px]">
            <button
              type="button"
              onClick={() => changeRowFilters(NO_ROW_FILTERS)}
              className="cursor-pointer font-[550] text-[var(--nx-tint)] underline-offset-4 hover:underline"
            >
              {t('list.filters.clear')}
            </button>
          </p>
        )}
      </>
    )
  }

  return (
    <div className="flex flex-col gap-5">
      <PageHeader eyebrow={t('list.eyebrow')} title={filter.archivedOnly ? t('list.archive.title') : t('list.title')} description={list.data ? (total ? t('list.summary', { count: rows.length, total }) : t('list.summaryCount', { count: rows.length })) : t('list.description')} actions={
          <>
            <SegmentedControl<'active' | 'archive'> aria-label={t('list.archive.label')} segments={[{ value: 'active', label: t('list.archive.active') }, { value: 'archive', label: t('list.archive.archived') }]} value={filter.archivedOnly ? 'archive' : 'active'} onChange={(scope) => navigateTo({ ...filter, archivedOnly: scope === 'archive', status: undefined, page: 0 }, view)} />
            <SegmentedControl<ListView> aria-label={t('list.view.label')} segments={viewSegments} value={view} onChange={(next) => navigateTo(filter, next)} />
            {!filter.archivedOnly && createAction}
          </>
        } />
      <SegmentedControl<StatusFilter>
        aria-label={t('list.filter.label')}
        segments={segments}
        value={filter.status ?? 'all'}
        onChange={(value) => goTo({ status: value === 'all' ? undefined : value, page: 0 })}
      />
      {body}
    </div>
  )
}
