import { RefreshCw } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { PageHeader } from '@/components/common/PageHeader'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { paths } from '@/routes/paths'
import { useAvailableActions, useOpportunity, useReloadOpportunity } from '../api'
import { ApiModeChip } from '../components/ApiModeChip'
import { LinesCard } from '../components/LinesCard'
import { LoseDialog, OpenDialog, WinDialog } from '../components/LifecycleDialogs'
import { ReassignDialog } from '../components/ReassignDialog'
import { OpportunityStatusBadge } from '../components/OpportunityStatusBadge'
import { PipelineCard } from '../components/PipelineCard'
import { QueryProblemState } from '../components/QueryProblemState'
import { SummaryCard } from '../components/SummaryCard'

type LifecycleAction = 'open' | 'win' | 'lose' | 'reassign'

export default function OpportunityDetailPage() {
  const { t } = useTranslation('opportunities')
  const navigate = useNavigate()
  const params = useParams()
  const id = Number(params.id)
  const validId = Number.isInteger(id) && id > 0

  const opportunity = useOpportunity(validId ? id : 0)
  const actionsQuery = useAvailableActions(validId ? id : 0, validId && opportunity.isSuccess)
  const reload = useReloadOpportunity(id)
  const [dialog, setDialog] = useState<LifecycleAction | null>(null)

  const backToList = (
    <Button variant="outline" render={<Link to={paths.crmOpportunities} />} nativeButton={false}>
      {t('common.backToList')}
    </Button>
  )

  // A malformed id is answered exactly like a record that does not exist or is not accessible.
  if (!validId) return <QueryProblemState error={{ status: 404, message: '' }} onRetry={() => {}} action={backToList} />
  if (opportunity.isError) return <QueryProblemState error={opportunity.error} onRetry={() => void opportunity.refetch()} retrying={opportunity.isFetching} action={backToList} />
  if (!opportunity.data) {
    return (
      <div className="flex flex-col gap-4" aria-busy="true" aria-label={t('detail.loading')}>
        <Skeleton className="h-10 w-72 rounded-md" />
        <Skeleton className="h-56 rounded-xl" />
        <Skeleton className="h-40 rounded-xl" />
      </div>
    )
  }

  const data = opportunity.data
  // Fail closed: while the projection is loading or failed, no lifecycle control is offered.
  const actions = actionsQuery.data
  const terminal = data.status === 'Won' || data.status === 'Lost'
  const noActionAvailable = actions !== undefined && !actions.canOpen && !actions.canWin && !actions.canLose && !actions.canReassign && !terminal

  return (
    <div className="flex flex-col gap-5">
      <PageHeader
        eyebrow={t('detail.eyebrow')}
        title={t('detail.title', { id: data.id })}
        onBack={() => navigate(paths.crmOpportunities)}
        backLabel={t('common.backToList')}
        actions={
          <>
            <ApiModeChip />
            <OpportunityStatusBadge status={data.status} />
            {opportunity.isFetching && <RefreshCw aria-label={t('detail.refreshing')} className="text-muted-foreground size-4 animate-spin" />}
            {actions?.canOpen && <Button onClick={() => setDialog('open')}>{t('open.action')}</Button>}
            {actions?.canWin && <Button onClick={() => setDialog('win')}>{t('win.action')}</Button>}
            {actions?.canReassign && (
              <Button variant="outline" onClick={() => setDialog('reassign')}>
                {t('summary.reassign')}
              </Button>
            )}
            {actions?.canLose && (
              <Button variant="destructive" onClick={() => setDialog('lose')}>
                {t('lose.action')}
              </Button>
            )}
          </>
        }
      />

      {actionsQuery.isError && (
        <Alert data-testid="actions-unavailable">
          <AlertTitle>{t('detail.actionsUnavailable.title')}</AlertTitle>
          <AlertDescription>
            <p>{t('detail.actionsUnavailable.description')}</p>
            <Button type="button" variant="outline" size="sm" className="mt-2" onClick={() => void actionsQuery.refetch()}>
              {t('state.error.retry')}
            </Button>
          </AlertDescription>
        </Alert>
      )}
      {noActionAvailable && (
        <p data-testid="no-actions" className="text-muted-foreground text-[13px]">
          {t('detail.noActions')}
        </p>
      )}
      {terminal && (
        <Alert data-testid="terminal-state">
          <AlertTitle>{data.status === 'Won' ? t('detail.terminal.won') : t('detail.terminal.lost')}</AlertTitle>
          <AlertDescription>{t('detail.terminal.description')}</AlertDescription>
        </Alert>
      )}

      <div className="grid gap-5 lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
        <div className="grid content-start gap-5">
          <SummaryCard opportunity={data} />
          <LinesCard opportunity={data} onReload={() => void reload()} />
        </div>
        <div className="grid content-start gap-5">
          <PipelineCard opportunity={data} actions={actions} onReload={() => void reload()} />
        </div>
      </div>

      {dialog === 'open' && <OpenDialog opportunity={data} onClose={() => setDialog(null)} onReload={() => void reload()} />}
      {dialog === 'win' && <WinDialog opportunity={data} onClose={() => setDialog(null)} onReload={() => void reload()} />}
      {dialog === 'reassign' && <ReassignDialog opportunity={data} onClose={() => setDialog(null)} onReload={() => void reload()} />}
      {dialog === 'lose' && <LoseDialog opportunity={data} onClose={() => setDialog(null)} onReload={() => void reload()} />}
    </div>
  )
}
