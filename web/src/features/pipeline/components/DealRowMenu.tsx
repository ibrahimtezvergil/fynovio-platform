import { MoreHorizontal, Trash2, UserRoundCog, Workflow } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { StageBadge } from '@/components/common/StageBadge'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { OWNERS } from '@/features/pipeline/data/deals'
import { dealToEntityRef } from '@/features/pipeline/data/format'
import type { PipelineRowActions } from '@/features/pipeline/table/pipelineTable'
import { stageForAction, type ChangeDealStageContext } from '@/features/pipeline/actions'
import { getActions } from '@/lib/actions'
import { initialsOf } from '@/lib/utils'
import type { Deal } from '@/types'

/**
 * Per-row actions. Every entry mutates for real — the same three verbs the
 * bulk bar offers, scoped to one deal, so there is no menu item here that
 * does nothing.
 */
export function DealRowMenu({ deal, actions }: { deal: Deal; actions: PipelineRowActions }) {
  const { t } = useTranslation('pipeline')
  const stageContext: ChangeDealStageContext = {
    entity: dealToEntityRef(deal),
    deal,
    changeStage: (stage) => actions.onChangeStage([deal.id], stage),
  }
  const stageActions = getActions(stageContext)
  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button variant="ghost" size="icon-sm" aria-label={t('rowMenu.actionsFor', { title: deal.title })}>
            <MoreHorizontal aria-hidden strokeWidth={1.7} />
          </Button>
        }
      />
      <DropdownMenuContent align="end" className="w-52">
        <DropdownMenuSub>
          <DropdownMenuSubTrigger>
            <Workflow aria-hidden strokeWidth={1.7} />
            {t('bulkBar.changeStage')}
          </DropdownMenuSubTrigger>
          <DropdownMenuSubContent className="w-52">
            {stageActions.map((action) => {
              const stage = stageForAction(action.id)
              if (!stage) return null

              return (
                <DropdownMenuItem key={action.id} onClick={() => void action.run(stageContext)}>
                  <StageBadge stage={stage} />
                </DropdownMenuItem>
              )
            })}
          </DropdownMenuSubContent>
        </DropdownMenuSub>

        <DropdownMenuSub>
          <DropdownMenuSubTrigger>
            <UserRoundCog aria-hidden strokeWidth={1.7} />
            {t('bulkBar.assign')}
          </DropdownMenuSubTrigger>
          <DropdownMenuSubContent className="w-52">
            {OWNERS.map((owner) => (
              <DropdownMenuItem
                key={owner}
                disabled={owner === deal.owner}
                onClick={() => actions.onAssign([deal.id], owner)}
              >
                <span aria-hidden className="nx-avatar size-5 text-[9.5px]">
                  {initialsOf(owner)}
                </span>
                {owner}
              </DropdownMenuItem>
            ))}
          </DropdownMenuSubContent>
        </DropdownMenuSub>

        <DropdownMenuSeparator />

        <DropdownMenuItem
          className="text-[var(--nx-st-red-fg)]"
          onClick={() => actions.onRemove([deal.id])}
        >
          <Trash2 aria-hidden strokeWidth={1.7} />
          {t('bulkBar.remove')}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
