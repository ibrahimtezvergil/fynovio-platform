import { Inbox, Plus } from 'lucide-react'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router-dom'
import { EmptyState } from '@/components/common/EmptyState'
import { PageHeader } from '@/components/common/PageHeader'
import { SegmentedControl, type Segment } from '@/components/common/SegmentedControl'
import { Button } from '@/components/ui/button'
import { paths } from '@/routes/paths'
import { PAGE_SIZE, useOpportunityList, type OpportunityListFilter } from '../api'
import { ApiModeChip } from '../components/ApiModeChip'
import { OpportunityTable } from '../components/OpportunityTable'
import { QueryProblemState } from '../components/QueryProblemState'
import { OPPORTUNITY_STATUSES, type OpportunityStatus } from '../schema'

type StatusFilter = OpportunityStatus | 'all'

function readFilter(params: URLSearchParams): OpportunityListFilter {
  const status = params.get('status')
  const page = Number(params.get('page') ?? 0)
  return {
    status: OPPORTUNITY_STATUSES.find((candidate) => candidate === status),
    page: Number.isInteger(page) && page > 0 ? page : 0,
  }
}

export default function OpportunitiesPage() {
  const { t } = useTranslation('opportunities')
  const [searchParams, setSearchParams] = useSearchParams()
  const filter = readFilter(searchParams)
  const list = useOpportunityList(filter)

  const segments = useMemo<readonly Segment<StatusFilter>[]>(
    () => [{ value: 'all', label: t('list.filter.all') }, ...OPPORTUNITY_STATUSES.map((status) => ({ value: status, label: t(`status.${status}`) }))],
    [t],
  )

  const goTo = (next: OpportunityListFilter) => {
    const params = new URLSearchParams()
    if (next.status) params.set('status', next.status)
    if (next.page > 0) params.set('page', String(next.page))
    setSearchParams(params)
  }

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
        title={filter.status || filter.page > 0 ? t('list.empty.filteredTitle') : t('list.empty.title')}
        description={filter.status || filter.page > 0 ? t('list.empty.filteredDescription') : t('list.empty.description')}
        action={filter.status || filter.page > 0 ? <Button variant="outline" onClick={() => goTo({ page: 0 })}>{t('list.empty.clearFilter')}</Button> : createAction}
      />
    )
  } else {
    body = (
      <>
        <OpportunityTable items={list.data?.items} refreshing={list.isFetching && list.data !== undefined} />
        <div className="flex items-center justify-between gap-3 pt-2">
          <span className="text-muted-foreground text-[12.5px]" aria-live="polite">
            {list.isFetching && list.data ? t('list.refreshing') : t('list.page', { page: filter.page + 1, size: PAGE_SIZE })}
          </span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={filter.page === 0} onClick={() => goTo({ ...filter, page: filter.page - 1 })}>
              {t('list.previous')}
            </Button>
            <Button variant="outline" size="sm" disabled={!list.data?.hasNext} onClick={() => goTo({ ...filter, page: filter.page + 1 })}>
              {t('list.next')}
            </Button>
          </div>
        </div>
      </>
    )
  }

  return (
    <div className="flex flex-col gap-5">
      <PageHeader eyebrow={t('list.eyebrow')} title={t('list.title')} description={t('list.description')} actions={<><ApiModeChip />{createAction}</>} />
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
