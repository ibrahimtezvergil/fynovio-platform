import { useEffect, useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { useShallow } from 'zustand/react/shallow'
import { toast } from 'sonner'
import { useSearchParams } from 'react-router-dom'
import { Can } from '@/components/common/Can'
import { PageHeader } from '@/components/common/PageHeader'
import { SegmentedControl, type Segment } from '@/components/common/SegmentedControl'
import { Button } from '@/components/ui/button'
import { useRemoveDeals, useUpdateDeals, usePipelineDeals } from '@/features/pipeline/api'
import { PipelineFilters } from '@/features/pipeline/components/PipelineFilters'
import { PipelineGrid } from '@/features/pipeline/components/PipelineGrid'
import { PipelineBoard } from '@/features/pipeline/components/PipelineBoard'
import { NO_DEALS } from '@/features/pipeline/data/deals'
import { money, weightedValue } from '@/features/pipeline/data/format'
import { usePipelineStore, type PipelineView } from '@/features/pipeline/store/usePipelineStore'
import { useCapability } from '@/lib/capabilities'
import type { PipelineRowActions } from '@/features/pipeline/table/pipelineTable'
import type { TFunction } from 'i18next'
import { STAGES, type Deal, type Stage } from '@/types'

/** Start and end of the calendar quarter `now` falls in. */
function currentQuarter(now: Date): { start: Date; end: Date } {
  const quarter = Math.floor(now.getMonth() / 3)
  return {
    start: new Date(now.getFullYear(), quarter * 3, 1),
    end: new Date(now.getFullYear(), quarter * 3 + 3, 0, 23, 59, 59, 999),
  }
}

export default function PipelinePage() {
  const { t } = useTranslation('pipeline')
  const VIEW_SEGMENTS: readonly Segment<PipelineView>[] = useMemo(
    () => [
      { value: 'grid', label: t('page.viewGrid') },
      { value: 'board', label: t('page.viewBoard') },
    ],
    [t],
  )
  const { data: deals = NO_DEALS, isLoading } = usePipelineDeals()
  const [searchParams] = useSearchParams()
  // A selector per slice: an un-selected `usePipelineStore()` re-renders the
  // page on every filter tick, and zustand v5 no longer shallow-compares for you.
  const { view, query, stages, owner, closeWindow, minValue } = usePipelineStore(
    useShallow((state) => ({
      view: state.view,
      query: state.query,
      stages: state.stages,
      owner: state.owner,
      closeWindow: state.closeWindow,
      minValue: state.minValue,
    })),
  )
  const setView = usePipelineStore((state) => state.setView)
  const setStages = usePipelineStore((state) => state.setStages)
  const reset = usePipelineStore((state) => state.reset)
  const canApprovePipeline = useCapability('pipeline.approval')

  // Dashboard drill-through is shareable: the filter is carried in the URL,
  // while the store remains the owner of the live filter controls.
  const stageFromUrl = searchParams.get('stage')
  useEffect(() => {
    setStages(stageFromUrl && STAGES.includes(stageFromUrl as Stage) ? [stageFromUrl as Stage] : [])
  }, [setStages, stageFromUrl])

  const updateDeals = useUpdateDeals()
  const removeDeals = useRemoveDeals()

  // Filtering happens before the table because two of the four pills are
  // predicates over the whole row, not over a single column.
  const visible = useMemo(() => {
    const { start, end } = currentQuarter(new Date())
    // `tr` locale casing, not the default: a Turkish "İSTANBUL" lowercases to
    // "istanbul" only under the Turkish rules, and this is a Turkish UI.
    const needle = query.trim().toLocaleLowerCase('tr')
    return deals.filter((deal) => {
      if (stages.length > 0 && !stages.includes(deal.stage)) return false
      if (owner !== 'all' && deal.owner !== owner) return false
      if (deal.value < minValue) return false
      if (closeWindow === 'quarter') {
        if (!deal.closeDate) return false
        const date = new Date(deal.closeDate)
        if (date < start || date > end) return false
      }
      if (
        needle !== '' &&
        ![deal.title, deal.account, deal.owner].some((field) =>
          field.toLocaleLowerCase('tr').includes(needle),
        )
      ) {
        return false
      }
      return true
    })
  }, [deals, query, stages, owner, closeWindow, minValue])

  const actions: PipelineRowActions = useMemo(
    () => ({
      onChangeStage: (ids: string[], stage: Stage) => {
        updateDeals.mutate({ ids, patch: { stage } })
      },
      onAssign: (ids: string[], nextOwner: string) => {
        updateDeals.mutate({ ids, patch: { owner: nextOwner } })
      },
      onRemove: (ids: string[]) => {
        removeDeals.mutate(ids)
      },
    }),
    [updateDeals, removeDeals],
  )

  const filtered = visible.length !== deals.length

  return (
    <div className="flex flex-col gap-[18px]">
      <PageHeader
        title="Pipeline"
        description={summarize(visible, t)}
        actions={
          <>
            <SegmentedControl
              aria-label={t('page.viewLabel')}
              segments={VIEW_SEGMENTS}
              value={view}
              onChange={setView}
            />
            {canApprovePipeline && (
              <Can action="deal.approve">
                <Button size="sm" onClick={() => toast.success(t('page.toastApproved'))}>
                  {t('page.approve')}
                </Button>
              </Can>
            )}
          </>
        }
      />

      <PipelineFilters />

      {view === 'grid' ? (
        <PipelineGrid deals={visible} isLoading={isLoading} actions={actions} />
      ) : (
        <PipelineBoard deals={visible} />
      )}

      {filtered && (
        <p className="text-muted-foreground text-[12.5px]">
          {t('page.filteredSummary', { total: deals.length, visible: visible.length })}{' '}
          <button
            type="button"
            onClick={reset}
            className="cursor-pointer font-[550] text-[var(--nx-tint)] underline-offset-4 hover:underline"
          >
            {t('page.clearFilters')}
          </button>
        </p>
      )}
    </div>
  )
}

/** The artboard's subtitle, derived: count plus probability-weighted value. */
function summarize(deals: Deal[], t: TFunction<'pipeline'>): string {
  const weighted = money.format(Math.round(weightedValue(deals)))
  return t('page.summary', { count: deals.length, value: weighted })
}
