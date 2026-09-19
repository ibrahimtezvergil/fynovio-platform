import { CalendarClock, ChevronDown, Coins } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { DensityToggle } from '@/components/common/DensityToggle'
import { StageBadge } from '@/components/common/StageBadge'
import { Toolbar, ToolbarGroup, ToolbarSearch, ToolbarSpacer } from '@/components/common/Toolbar'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { money } from '@/features/pipeline/data/format'
import { OWNERS } from '@/features/pipeline/data/deals'
import { usePipelineStore } from '@/features/pipeline/store/usePipelineStore'
import { STAGES } from '@/types'
import { useShallow } from 'zustand/react/shallow'

/**
 * The pill reads tinted exactly while it is narrowing the result set.
 *
 * `size="sm"` is a fixed 30px step in the button scale, so the height is
 * overridden with the density token: the pills sit one step under the search
 * box at both densities instead of freezing at 30px while it shrinks.
 */
function pill(active: boolean) {
  return {
    variant: active ? ('tinted' as const) : ('secondary' as const),
    size: 'sm' as const,
    className: 'h-[var(--nx-d-control-sm)] rounded-[var(--nx-r-pill)]',
  }
}

export const MIN_VALUE_STEP = 50_000

/**
 * The strip above the grid: search and filter pills left, density right.
 *
 * It is a `Toolbar`, so it is a density region — the search box and every pill
 * in it drop from 38px to 32px on the switch, while the page header above and
 * the KPI cards elsewhere on the page do not move.
 */
export function PipelineFilters() {
  const { t } = useTranslation('pipeline')
  const { query, stages, owner, closeWindow, minValue } = usePipelineStore(
    useShallow((state) => ({
      query: state.query,
      stages: state.stages,
      owner: state.owner,
      closeWindow: state.closeWindow,
      minValue: state.minValue,
    })),
  )
  // Actions are stable identities — selecting them costs nothing per render.
  const setQuery = usePipelineStore((state) => state.setQuery)
  const toggleStage = usePipelineStore((state) => state.toggleStage)
  const setStages = usePipelineStore((state) => state.setStages)
  const setOwner = usePipelineStore((state) => state.setOwner)
  const setCloseWindow = usePipelineStore((state) => state.setCloseWindow)
  const setMinValue = usePipelineStore((state) => state.setMinValue)

  return (
    <Toolbar>
      <ToolbarSearch value={query} onChange={setQuery} placeholder={t('filters.searchPlaceholder')} />

      <ToolbarGroup>
        <DropdownMenu>
          <DropdownMenuTrigger
            render={
              <Button {...pill(stages.length > 0)}>
                {stages.length === 0
                  ? t('filters.stageAll')
                  : t('filters.stageSelected', { count: stages.length })}
                <ChevronDown aria-hidden strokeWidth={1.7} />
              </Button>
            }
          />
          <DropdownMenuContent className="w-56">
            <DropdownMenuLabel>{t('filters.stagesLabel')}</DropdownMenuLabel>
            <DropdownMenuSeparator />
            {STAGES.map((stage) => (
              <DropdownMenuCheckboxItem
                key={stage}
                checked={stages.includes(stage)}
                onCheckedChange={() => toggleStage(stage)}
                closeOnClick={false}
              >
                <StageBadge stage={stage} />
              </DropdownMenuCheckboxItem>
            ))}
            <DropdownMenuSeparator />
            <DropdownMenuItem disabled={stages.length === 0} onClick={() => setStages([])}>
              {t('filters.showAll')}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>

        <DropdownMenu>
          <DropdownMenuTrigger
            render={
              <Button {...pill(owner !== 'all')}>
                {t('filters.ownerPrefix')} {owner === 'all' ? t('filters.all') : owner}
                <ChevronDown aria-hidden strokeWidth={1.7} />
              </Button>
            }
          />
          <DropdownMenuContent className="w-56">
            <DropdownMenuRadioGroup value={owner} onValueChange={(next) => setOwner(next)}>
              <DropdownMenuRadioItem value="all">{t('filters.all')}</DropdownMenuRadioItem>
              <DropdownMenuSeparator />
              {OWNERS.map((name) => (
                <DropdownMenuRadioItem key={name} value={name}>
                  {name}
                </DropdownMenuRadioItem>
              ))}
            </DropdownMenuRadioGroup>
          </DropdownMenuContent>
        </DropdownMenu>

        <Button
          {...pill(closeWindow === 'quarter')}
          aria-pressed={closeWindow === 'quarter'}
          onClick={() => setCloseWindow(closeWindow === 'quarter' ? 'all' : 'quarter')}
        >
          <CalendarClock aria-hidden strokeWidth={1.7} />
          {t('filters.closeWindowPrefix')}{' '}
          {closeWindow === 'quarter' ? t('filters.thisQuarter') : t('filters.all')}
        </Button>

        <Button
          {...pill(minValue > 0)}
          aria-pressed={minValue > 0}
          onClick={() => setMinValue(minValue > 0 ? 0 : MIN_VALUE_STEP)}
        >
          <Coins aria-hidden strokeWidth={1.7} />
          {t('filters.valuePrefix')}{' '}
          {minValue > 0 ? `${money.format(minValue)}+` : t('filters.all')}
        </Button>
      </ToolbarGroup>

      <ToolbarSpacer />

      <DensityToggle />
    </Toolbar>
  )
}
