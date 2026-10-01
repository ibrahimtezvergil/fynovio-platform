import { CalendarPlus, MoreHorizontal, RefreshCw } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { openLinkedOpportunityEntryDialog } from '@/features/calendar'
import { PageHeader } from '@/components/common/PageHeader'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { CommandDialog } from '../components/CommandDialog'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Skeleton } from '@/components/ui/skeleton'
import { paths } from '@/routes/paths'
import { useAvailableActions, useOpportunity, usePartyNames, usePipelineStages, useReloadOpportunity, useSetOpportunityArchive } from '../api'
import { ActivityCard } from '../components/ActivityCard'
import { CustomFieldsCard } from '../components/CustomFieldsCard'
import { LinesCard } from '../components/LinesCard'
import { LoseDialog, OpenDialog, WinDialog } from '../components/LifecycleDialogs'
import { ReassignDialog } from '../components/ReassignDialog'
import { OpportunityStatusBadge } from '../components/OpportunityStatusBadge'
import { useKeyedCommand } from '../lib/useKeyedCommand'
import { PipelineCard } from '../components/PipelineCard'
import { QueryProblemState } from '../components/QueryProblemState'
import { SummaryCard } from '../components/SummaryCard'

type LifecycleAction = 'open' | 'win' | 'lose' | 'reassign' | 'archive' | 'restore'

export default function OpportunityDetailPage() {
  const { t } = useTranslation('opportunities')
  const navigate = useNavigate()
  const location = useLocation()
  const params = useParams()
  const id = Number(params.id)
  const validId = Number.isInteger(id) && id > 0

  const opportunity = useOpportunity(validId ? id : 0)
  const actionsQuery = useAvailableActions(validId ? id : 0, validId && opportunity.isSuccess)
  const reload = useReloadOpportunity(id)
  const partyNames = usePartyNames(opportunity.data?.partyId == null ? [] : [opportunity.data.partyId])
  const pipelineStages = usePipelineStages(opportunity.data?.pipelineDefinitionVersionId)
  const [dialog, setDialog] = useState<LifecycleAction | null>(null)
  // Arriving from "create and add lines": remember it once, then clear the history state so a reload does not reopen the dialog.
  const [autoAddLine] = useState(() => Boolean((location.state as { addLine?: boolean } | null)?.addLine))
  useEffect(() => {
    if (autoAddLine) navigate(`${location.pathname}${location.search}`, { replace: true, state: null })
  }, [autoAddLine, location.pathname, location.search, navigate])

  const returnTo = new URLSearchParams(location.search).get('from')
  const listUrl = returnTo ? `${paths.crmOpportunities}?${returnTo}` : paths.crmOpportunities
  const backToList = (
    <Button variant="outline" render={<Link to={listUrl} />} nativeButton={false}>
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
  const hasActiveRequiredLine = data.lines.some((line) => !line.isCanceled && !line.isOptional)
  const partyName = partyNames.data?.get(data.partyId ?? -1)
  const customer = partyName ?? (data.partyId == null ? null : t('summary.partyValue', { id: data.partyId }))
  // Fail closed: while the projection is loading or failed, no lifecycle control is offered.
  const actions = actionsQuery.data
  const terminal = data.status === 'Won' || data.status === 'Lost'
  const noActionAvailable = actions !== undefined && !actions.canOpen && !actions.canWin && !actions.canLose && !actions.canReassign && !terminal

  return (
    <div className="flex flex-col gap-5">
      <PageHeader
        eyebrow={t('detail.eyebrow')}
        title={customer ? t('detail.titleWithParty', { party: customer, id: data.id }) : t('detail.title', { id: data.id })}
        onBack={() => navigate(listUrl)}
        backLabel={t('common.backToList')}
        actions={
          <>
            <OpportunityStatusBadge status={data.status} />
            {data.isArchived && <span className="rounded-full border px-2.5 py-1 text-xs font-medium">{t('list.archive.archived')}</span>}
            {/* Calendar owns the linked-entry form; opening its overlay leaves the opportunity route in place. */}
            <Button type="button" variant="outline" onClick={() => openLinkedOpportunityEntryDialog(data.id)}>
              <CalendarPlus aria-hidden />
              {t('detail.addToCalendar')}
            </Button>
            {opportunity.isFetching && <RefreshCw aria-label={t('detail.refreshing')} className="text-muted-foreground size-4 animate-spin" />}
            {actions?.canOpen && <Button variant={hasActiveRequiredLine ? 'default' : 'outline'} onClick={() => setDialog('open')}>{t('open.action')}</Button>}
            {actions?.canWin && <Button onClick={() => setDialog('win')}>{t('win.action')}</Button>}
            {actions?.canLose && (
              <DropdownMenu>
                <DropdownMenuTrigger render={<Button variant="outline" size="icon-sm" aria-label={t('detail.moreActions')}><MoreHorizontal aria-hidden /></Button>} />
                <DropdownMenuContent align="end">
                  <DropdownMenuItem variant="destructive" onClick={() => setDialog('lose')}>{t('lose.action')}</DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            )}
            {actions?.canArchive && <Button variant="outline" onClick={() => setDialog('archive')}>{t('archive.action')}</Button>}
            {actions?.canRestore && <Button onClick={() => setDialog('restore')}>{t('archive.restore')}</Button>}
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
          <SummaryCard opportunity={data} canReassign={actions?.canReassign ?? false} onReassign={() => setDialog('reassign')} />
          <CustomFieldsCard opportunity={data} onSaved={() => void reload()} />
          <LinesCard opportunity={data} onReload={() => void reload()} autoAdd={autoAddLine} />
        </div>
        <div className="grid content-start gap-5">
          <PipelineCard opportunity={data} actions={actions} onReload={() => void reload()} />
          <ActivityCard opportunityId={data.id} />
        </div>
      </div>

      {dialog === 'open' && <OpenDialog opportunity={data} onClose={() => setDialog(null)} onReload={() => void reload()} />}
      {dialog === 'win' && <WinDialog opportunity={data} onClose={() => setDialog(null)} onReload={() => void reload()} />}
      {dialog === 'reassign' && <ReassignDialog opportunity={data} onClose={() => setDialog(null)} onReload={() => void reload()} />}
      {dialog === 'lose' && <LoseDialog opportunity={data} onClose={() => setDialog(null)} onReload={() => void reload()} />}
      {(dialog === 'archive' || dialog === 'restore') && <ArchiveDialog
        opportunity={data}
        archive={dialog === 'archive'}
        stages={(pipelineStages.data ?? []).filter((stage) => stage.isActive && !stage.isArchived)}
        onClose={() => setDialog(null)}
        onReload={() => void reload()}
      />}
    </div>
  )
}

function ArchiveDialog({ opportunity, archive, stages, onClose, onReload }: {
  opportunity: NonNullable<ReturnType<typeof useOpportunity>['data']>
  archive: boolean
  stages: { id: number; name: string }[]
  onClose: () => void
  onReload: () => void
}) {
  const { t } = useTranslation('opportunities')
  const command = useKeyedCommand(useSetOpportunityArchive(archive))
  const originalStageAvailable = opportunity.pipelineStageId != null && stages.some((stage) => stage.id === opportunity.pipelineStageId)
  const [stageId, setStageId] = useState<number | undefined>(originalStageAvailable ? undefined : stages[0]?.id)
  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    const result = await command.run({
      id: opportunity.id,
      expectedVersion: opportunity.rowVersion,
      confirmOpenOpportunity: archive && opportunity.status === 'Open',
      ...(!archive && !originalStageAvailable ? { stageId } : {}),
    })
    if (result) onClose()
  }
  const stageName = stages.find((stage) => stage.id === opportunity.pipelineStageId)?.name ?? t('archive.stageFallback', { id: opportunity.pipelineStageId ?? '?' })
  const description = archive && opportunity.status === 'Open'
    ? t('archive.openConfirmation', { stage: stageName })
    : archive ? t('archive.draftConfirmation') : t('archive.restoreDescription')

  return (
    <CommandDialog open onOpenChange={(open) => !open && onClose()}
      title={archive ? t('archive.title') : t('archive.restoreTitle')}
      description={description}
      submitLabel={command.isPending ? t('archive.submitting') : archive ? t('archive.confirm') : t('archive.restore')}
      destructive={archive} pending={command.isPending} problem={command.problem} onReload={onReload} onSubmit={submit}>
      {!archive && opportunity.status === 'Open' && !originalStageAvailable && (
        stages.length > 0
          ? <label className="grid gap-1.5 text-sm">{t('archive.chooseStage')}<select className="h-10 rounded-md border bg-background px-3" value={stageId ?? ''} required onChange={(event) => setStageId(Number(event.target.value))}>{stages.map((stage) => <option key={stage.id} value={stage.id}>{stage.name}</option>)}</select></label>
          : <p role="alert" className="text-sm text-destructive">{t('archive.noActiveStages')}</p>
      )}
    </CommandDialog>
  )
}
