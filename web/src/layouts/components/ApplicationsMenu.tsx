import { ChevronDown, Home, LayoutGrid } from 'lucide-react'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { buildNavGroups, findNavTrail } from '@/layouts/navigation'
import { APPLICATION_ICON } from '@/lib/applications/icons'
import { applicationRegistry } from '@/lib/applications/registry'
import { paths } from '@/routes/paths'
import { useAppStore } from '@/store/useAppStore'

/**
 * The Topbar's "Uygulamalar" launcher — the one reliable way back to Master
 * Home and into every business application from anywhere in the shell,
 * including a sidebar-free utility. Reads the same `applicationRegistry` the
 * Home page's launcher does, so the two never drift apart.
 */
export function ApplicationsMenu() {
  const { t } = useTranslation('nav')
  const { t: tHome } = useTranslation('home')
  const navigate = useNavigate()
  const lastVisitedRouteByScope = useAppStore((s) => s.lastVisitedRouteByScope)

  const applications = useMemo(() => {
    const entries = applicationRegistry
      .filter((app) => app.kind === 'application')
      .sort((a, b) => a.order - b.order)
    return entries.map((app) => {
      const lastVisited = app.navScope ? lastVisitedRouteByScope[app.navScope] : undefined
      const trail = lastVisited && app.navScope ? findNavTrail(buildNavGroups(t, 'sidebar', app.navScope), lastVisited) : undefined
      return { app, target: lastVisited ?? app.route, lastVisitedLabel: trail?.leaf?.label ?? trail?.item.label }
    })
  }, [lastVisitedRouteByScope, t])

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button variant="secondary" size="sm">
            <LayoutGrid aria-hidden strokeWidth={1.7} />
            {t('topbar.applications')}
            <ChevronDown aria-hidden className="text-muted-foreground" strokeWidth={1.7} />
          </Button>
        }
      />
      <DropdownMenuContent align="start" className="w-64">
        <DropdownMenuItem onClick={() => navigate(paths.dashboard)}>
          <Home aria-hidden strokeWidth={1.7} />
          {t('topbar.applicationsHome')}
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuLabel>{t('topbar.applications')}</DropdownMenuLabel>
        {applications.map(({ app, target, lastVisitedLabel }) => {
          const Icon = APPLICATION_ICON[app.icon]
          const comingSoon = app.status === 'comingSoon'
          return (
            <DropdownMenuItem
              key={app.id}
              disabled={comingSoon}
              onClick={() => target && navigate(target)}
              className="flex-col items-stretch gap-0.5 py-1.5"
            >
              <span className="flex w-full items-center gap-1.5">
                <Icon aria-hidden strokeWidth={1.7} />
                <span className="flex-1">{tHome(app.labelKey)}</span>
                {comingSoon ? (
                  <span className="text-[10px] font-[600] text-muted-foreground">{tHome('applications.comingSoon')}</span>
                ) : (
                  app.badge != null && (
                    <span className="tnum rounded-full bg-[image:var(--nx-accent-grad)] px-1.5 text-[10px] leading-[17px] font-[750] text-[var(--nx-on-accent)]">
                      {app.badge > 99 ? '99+' : app.badge}
                    </span>
                  )
                )}
              </span>
              {lastVisitedLabel && !comingSoon && (
                <span className="pl-[21.5px] text-[11px] text-muted-foreground">
                  {t('topbar.lastVisited', { label: lastVisitedLabel })}
                </span>
              )}
            </DropdownMenuItem>
          )
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
