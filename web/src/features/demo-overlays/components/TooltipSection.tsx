import { Archive, Copy, Info, Pencil, Star, Trash2 } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { DemoSection } from '@/components/common/DemoSection'
import { OverlayDemo } from '@/components/common/OverlayDemo'

const ROW_ACTION_ICONS = [Pencil, Copy, Star, Archive, Trash2] as const
const ROW_ACTION_KEYS = ['edit', 'copy', 'follow', 'archive', 'delete'] as const

export function TooltipSection() {
  const { t } = useTranslation('demo-overlays')
  const months = t('tooltipSection.delayGroup.months', { returnObjects: true }) as string[]

  return (
    <DemoSection
      id="tooltip"
      title={t('tooltipSection.sectionTitle')}
      description={t('tooltipSection.sectionDescription')}
      icon={Info}
    >
      <OverlayDemo
        name="<Tooltip>"
        title={t('tooltipSection.iconLabels.title')}
        description={t('tooltipSection.iconLabels.description')}
        wide
      >
        {ROW_ACTION_KEYS.map((key, index) => {
          const Icon = ROW_ACTION_ICONS[index]
          const label = t(`tooltipSection.rowActions.${key}.label`)
          return (
            <Tooltip key={key}>
              <TooltipTrigger
                render={<Button variant="ghost" size="icon-sm" />}
                aria-label={label}
              >
                <Icon />
              </TooltipTrigger>
              <TooltipContent>{t(`tooltipSection.rowActions.${key}.hint`)}</TooltipContent>
            </Tooltip>
          )
        })}
      </OverlayDemo>

      <OverlayDemo
        name="side"
        title={t('tooltipSection.side.title')}
        description={t('tooltipSection.side.description')}
        wide
      >
        {(['top', 'right', 'bottom', 'left'] as const).map((side) => (
          <Tooltip key={side}>
            <TooltipTrigger render={<Button variant="outline" size="sm" />}>{side}</TooltipTrigger>
            <TooltipContent side={side}>side="{side}"</TooltipContent>
          </Tooltip>
        ))}
      </OverlayDemo>

      <OverlayDemo
        name="TooltipProvider delay"
        title={t('tooltipSection.delayGroup.title')}
        description={t('tooltipSection.delayGroup.description')}
        wide
      >
        <div className="flex flex-wrap items-center gap-1">
          {months.map((month, index) => (
            <Tooltip key={month}>
              <TooltipTrigger render={<Button variant="ghost" size="sm" />}>{month}</TooltipTrigger>
              <TooltipContent>
                ₺{(index + 3) * 145}.000 · {12 + index * 3} {t('tooltipSection.delayGroup.dealsSuffix')}
              </TooltipContent>
            </Tooltip>
          ))}
        </div>
      </OverlayDemo>
    </DemoSection>
  )
}
