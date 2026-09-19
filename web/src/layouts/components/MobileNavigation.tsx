import { Menu } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useLocation } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet'
import type { NavScope } from '@/lib/navigation/types'
import { ApplicationsMenu } from './ApplicationsMenu'
import { SidebarNav } from './SidebarNav'
import { ToolsMenu } from './ToolsMenu'

/**
 * The adaptive shell navigation. Below xl the full topbar directory
 * (Applications, Tools) moves here; below lg it also carries the scope's nav,
 * because the persistent sidebar is a rail (sm–lg) or absent (below sm).
 */
export function MobileNavigation({ scope }: { scope?: NavScope }) {
  const { t } = useTranslation('nav')
  const { pathname } = useLocation()
  const [open, setOpen] = useState(false)
  const [openedAt, setOpenedAt] = useState(pathname)

  // Close after a navigation through the sheet's own `open` state, so the
  // primitive still plays its exit and returns focus to the trigger — a
  // remount (`key={pathname}`) would skip both. Adjusted during render, like
  // `ExpandedParent` in SidebarNav, so it never paints one frame stale.
  if (pathname !== openedAt) {
    setOpenedAt(pathname)
    setOpen(false)
  }

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger
        render={<Button variant="secondary" size="icon" className="xl:hidden" aria-label={t('topbar.openNavigation')} />}
      >
        <Menu aria-hidden className="size-[18px]" strokeWidth={1.7} />
      </SheetTrigger>

      <SheetContent
        side="left"
        // Same-variant classes so twMerge replaces the primitive's `w-3/4` / `sm:max-w-md` defaults.
        className="gap-0 p-0 data-[side=left]:w-[85vw] data-[side=left]:max-w-[20rem] data-[side=left]:sm:max-w-[20rem]"
      >
        <SheetHeader className="border-b border-[var(--nx-hairline)] pb-4">
          <SheetTitle>Fynovio</SheetTitle>
        </SheetHeader>

        <div className="flex items-center gap-2 px-5 py-4 [&_button]:min-h-11">
          <ApplicationsMenu />
          <ToolsMenu />
        </div>

        {/* From `lg:` the persistent sidebar already shows this navigation. */}
        {scope && (
          <div className="min-h-0 flex-1 overflow-hidden px-5 pb-5 lg:hidden">
            <SidebarNav collapsed={false} scope={scope} />
          </div>
        )}
      </SheetContent>
    </Sheet>
  )
}
