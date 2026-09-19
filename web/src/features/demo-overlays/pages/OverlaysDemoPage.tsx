import {
  GitCompareArrows,
  Info,
  Layers,
  MousePointerClick,
  PanelBottom,
  PanelRight,
  ShieldAlert,
  SquareStack,
} from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { PageHeader } from '@/components/common/PageHeader'
import { SectionNav, type NavSection } from '@/components/common/SectionNav'
import { Badge } from '@/components/ui/badge'
import { AlertDialogSection } from '@/features/demo-overlays/components/AlertDialogSection'
import { ChoosingSection } from '@/features/demo-overlays/components/ChoosingSection'
import { DialogSection } from '@/features/demo-overlays/components/DialogSection'
import { DrawerSection } from '@/features/demo-overlays/components/DrawerSection'
import { PopoverSection } from '@/features/demo-overlays/components/PopoverSection'
import { RecipeSection } from '@/features/demo-overlays/components/RecipeSection'
import { SheetSection } from '@/features/demo-overlays/components/SheetSection'
import { TooltipSection } from '@/features/demo-overlays/components/TooltipSection'

/**
 * The overlay gallery: every layer that opens on top of the page, with the
 * thing that actually distinguishes them printed next to each one — what it
 * takes away from the user while it is open. The last section is the point of
 * the first six: two patterns where the layers stack.
 */
export default function OverlaysDemoPage() {
  const { t } = useTranslation('demo-overlays')

  const sections: readonly NavSection[] = [
    { id: 'secim', label: t('page.sections.secim'), icon: GitCompareArrows },
    { id: 'dialog', label: t('page.sections.dialog'), icon: SquareStack },
    { id: 'alert-dialog', label: t('page.sections.alertDialog'), icon: ShieldAlert },
    { id: 'sheet', label: t('page.sections.sheet'), icon: PanelRight },
    { id: 'drawer', label: t('page.sections.drawer'), icon: PanelBottom },
    { id: 'popover', label: t('page.sections.popover'), icon: MousePointerClick },
    { id: 'tooltip', label: t('page.sections.tooltip'), icon: Info },
    { id: 'kaliplar', label: t('page.sections.kaliplar'), icon: Layers },
  ]

  return (
    <div className="mx-auto flex w-full max-w-[1320px] flex-col gap-5">
      <PageHeader
        eyebrow={t('page.eyebrow')}
        title={t('page.title')}
        description={t('page.description')}
        actions={<Badge variant="secondary">{t('page.badge', { count: sections.length })}</Badge>}
      />

      <div className="grid grid-cols-1 items-start gap-5 lg:grid-cols-[236px_minmax(0,1fr)]">
        <SectionNav sections={sections} label={t('page.navLabel')} />

        <div className="flex min-w-0 flex-col gap-4">
          <ChoosingSection />
          <DialogSection />
          <AlertDialogSection />
          <SheetSection />
          <DrawerSection />
          <PopoverSection />
          <TooltipSection />
          <RecipeSection />
        </div>
      </div>
    </div>
  )
}
