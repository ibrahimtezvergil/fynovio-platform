import { useTranslation } from 'react-i18next'
import { ApplicationCard } from '@/features/home/components/ApplicationCard'
import { applicationRegistry } from '@/lib/applications/registry'
import type { ApplicationItem } from '@/lib/applications/types'
import { Card } from '@/components/ui/card'
// Restore these imports with the all-applications button and drawer below.
// import { LayoutGrid, X } from 'lucide-react'
// import { Button } from '@/components/ui/button'
// import { DrawerClose, DrawerDescription, DrawerHeader, DrawerTitle } from '@/components/ui/drawer'
// import { openDrawer, useOverlayStore } from '@/lib/overlay'

/** The primary launcher surface, with applications and tools visibly separate. */
export function ApplicationsSection() {
  const { t } = useTranslation('home')
  const apps = [...applicationRegistry].sort((a, b) => a.order - b.order)
  const applications = apps.filter((app) => app.kind === 'application')
  const utilities = apps.filter((app) => app.kind === 'utility')

  return (
    <Card className="h-full min-h-0 gap-0 p-0">
      <div className="min-h-0 flex-1 space-y-6 overflow-y-auto p-5">
        <AppGroup heading={t('applications.heading')} apps={applications} />
        {utilities.length > 0 && (
          <>
            <div aria-hidden className="h-px bg-border" />
            <AppGroup heading={t('applications.utilitiesHeading')} apps={utilities} />
          </>
        )}
      </div>
      {/* Restore when the application/tool catalog grows.
      <div className="flex shrink-0 justify-end border-t px-4 py-2">
        <Button variant="ghost" onClick={() => void openDrawer({ content: <ApplicationsDrawer /> })}>
          <LayoutGrid aria-hidden className="size-4" />
          <span>{t('applications.viewAll')}</span>
        </Button>
      </div>
      */}
    </Card>
  )
}

/** Keeps the same application/utility distinction as the launcher, with no tabs. */
/* Temporarily parked with its launcher above; restore together.
function ApplicationsDrawer() {
  const { t } = useTranslation('home')
  const overlayId = useOverlayStore((state) => state.entries.at(-1)?.id)
  const close = useOverlayStore((state) => state.close)
  const apps = [...applicationRegistry].sort((a, b) => a.order - b.order)

  return (
    <>
      <DrawerHeader className="relative pr-16">
        <DrawerTitle>{t('applications.viewAll')}</DrawerTitle>
        <DrawerDescription>{t('applications.description')}</DrawerDescription>
        <DrawerClose render={<Button variant="ghost" size="icon" />} className="absolute top-4 right-4" aria-label={t('applications.close')}><X aria-hidden /></DrawerClose>
      </DrawerHeader>
      <div className="min-h-0 space-y-6 overflow-y-auto px-5 pb-6">
        {(['application', 'utility'] as const).map((kind) => {
          const entries = apps.filter((app) => app.kind === kind)
          if (!entries.length) return null
          return (
            <section key={kind} aria-label={t(kind === 'application' ? 'applications.heading' : 'applications.utilitiesHeading')}>
              <h2 className="mb-3 text-sm font-semibold">{t(kind === 'application' ? 'applications.heading' : 'applications.utilitiesHeading')}</h2>
              <div className="flex flex-wrap gap-3 p-1">
                {entries.map((app) => <ApplicationCard key={app.id} app={app} onNavigate={() => { if (overlayId) close(overlayId) }} />)}
              </div>
            </section>
          )
        })}
      </div>
    </>
  )
}
*/

function AppGroup({ heading, apps }: { heading: string; apps: ApplicationItem[] }) {
  if (apps.length === 0) return null

  return (
    <section aria-label={heading} className="min-w-0 space-y-3">
      <h2 className="font-heading text-[15.5px] leading-5 font-[620] tracking-[-0.022em]">
        {heading}
      </h2>
      <div className="flex flex-wrap gap-3">
        {apps.map((app) => (
          <ApplicationCard key={app.id} app={app} />
        ))}
      </div>
    </section>
  )
}
