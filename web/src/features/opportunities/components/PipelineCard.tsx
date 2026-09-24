import { Loader2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Field } from '@/components/common/Field'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Select } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { useChangeStage, usePipelineStages } from '../api'
import { useKeyedCommand } from '../lib/useKeyedCommand'
import type { AvailableActions, Opportunity } from '../schema'
import { ProblemNotice } from './ProblemNotice'

interface PipelineCardProps {
  opportunity: Opportunity
  /** `undefined` while unknown or failed to load: nothing is offered then (fail closed), never guessed. */
  actions: AvailableActions | undefined
  onReload: () => void
}

/**
 * The stage is tenant-configurable data. Names come from the pipeline-stages query; the selectable targets are
 * exactly `allowedTargetStageIds` from the action projection — never "every other stage", never a hard-coded
 * name, never a first-by-sort-order fallback.
 */
export function PipelineCard({ opportunity, actions, onReload }: PipelineCardProps) {
  const { t } = useTranslation('opportunities')
  const stages = usePipelineStages(opportunity.pipelineDefinitionVersionId)
  const command = useKeyedCommand(useChangeStage())
  const [target, setTarget] = useState('')

  const stageLabel = (id: number) => stages.data?.find((stage) => stage.id === id)?.name ?? t('pipeline.stageFallback', { id })

  const targets = useMemo(() => {
    const allowed = new Set(actions?.allowedTargetStageIds ?? [])
    const named = (stages.data ?? []).filter((stage) => allowed.has(stage.id))
    // Ids the stage list could not name are still real, backend-allowed targets: keep them, labelled by id.
    const unnamed = [...allowed].filter((id) => !named.some((stage) => stage.id === id))
    return [...named.map((stage) => stage.id), ...unnamed]
  }, [actions, stages.data])

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    if (!target) return
    const result = await command.run({ id: opportunity.id, expectedVersion: opportunity.rowVersion, targetStageId: Number(target) })
    if (result) setTarget('')
  }

  const currentStageId = opportunity.pipelineStageId
  let body: React.ReactNode
  if (opportunity.pipelineDefinitionVersionId == null || currentStageId == null) {
    body = <p className="text-muted-foreground text-[13px]">{opportunity.status === 'Draft' ? t('pipeline.draftNextStep') : t('pipeline.none')}</p>
  } else {
    body = (
      <>
        <div className="text-[13px]">
          <span className="text-muted-foreground">{t('pipeline.current')}: </span>
          {stages.isLoading ? (
            <Skeleton aria-label={t('pipeline.loadingStage')} className="inline-block h-4 w-28 align-middle" />
          ) : (
            <strong data-testid="current-stage">{stageLabel(currentStageId)}</strong>
          )}
        </div>
        {actions?.canChangeStage ? (
          targets.length > 0 ? (
            <form onSubmit={submit} className="flex flex-wrap items-end gap-2">
              <Field label={t('pipeline.move.label')} className="min-w-[180px] flex-1">
                {(props) => (
                  <Select {...props} value={target} onChange={(event) => setTarget(event.target.value)}>
                    <option value="">{t('pipeline.move.placeholder')}</option>
                    {targets.map((id) => (
                      <option key={id} value={id}>
                        {stageLabel(id)}
                      </option>
                    ))}
                  </Select>
                )}
              </Field>
              <Button type="submit" disabled={!target || command.isPending}>
                {command.isPending && <Loader2 aria-hidden className="animate-spin" />}
                {t('pipeline.move.submit')}
              </Button>
            </form>
          ) : (
            <p className="text-muted-foreground text-[13px]">{t('pipeline.noTargets')}</p>
          )
        ) : (
          <p className="text-muted-foreground text-[13px]">{t('pipeline.transitionsUnavailable')}</p>
        )}
        {command.problem && <ProblemNotice problem={command.problem} onReload={onReload} />}
      </>
    )
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{opportunity.status === 'Draft' ? t('pipeline.nextStepTitle') : t('pipeline.title')}</CardTitle>
        {opportunity.status !== 'Draft' && <CardDescription>{t('pipeline.description')}</CardDescription>}
      </CardHeader>
      <CardContent className="grid gap-3">{body}</CardContent>
    </Card>
  )
}
