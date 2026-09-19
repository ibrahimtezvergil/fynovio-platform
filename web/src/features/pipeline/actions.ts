import { Workflow } from 'lucide-react'
import { registerAction, type ActionContext } from '@/lib/actions'
import { STAGES, type Deal, type Stage } from '@/types'

export interface ChangeDealStageContext extends ActionContext {
  deal: Deal
  changeStage: (stage: Stage) => void
}

function isChangeDealStageContext(ctx: ActionContext): ctx is ChangeDealStageContext {
  return ctx.entity?.type === 'deal' && 'deal' in ctx && 'changeStage' in ctx
}

for (const stage of STAGES) {
  registerAction<ChangeDealStageContext>({
    id: `deal.change-stage.${stage}`,
    label: `Change stage to ${stage}`,
    icon: Workflow,
    when: (ctx) => isChangeDealStageContext(ctx) && ctx.deal.stage !== stage,
    run: (ctx) => ctx.changeStage(stage),
  })
}

export function stageForAction(actionId: string): Stage | undefined {
  const stage = actionId.replace('deal.change-stage.', '')
  return STAGES.find((candidate) => candidate === stage)
}
