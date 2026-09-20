import { useEffect } from 'react'
import { Outlet, useLocation, useMatches } from 'react-router-dom'
import { OverlayHost } from '@/components/common/OverlayHost'
import { useRouteChangeTracking } from '@/lib/telemetry/useRouteChangeTracking'
import type { NavScope, RouteSurface } from '@/lib/navigation/types'
import { useAppStore } from '@/store/useAppStore'
import { Sidebar } from './components/Sidebar'
import { Topbar } from './components/Topbar'

/** A route (or route group) declares its shell classification by attaching this to its `handle`. */
interface RouteHandle {
  surface?: RouteSurface
  navScope?: NavScope
}

/**
 * Surfaces that own a sidebar. Home and Utility are deliberately absent — see
 * `RouteSurface`. Developer is also absent: it's a global Tool now (reached
 * from the Topbar's Tools menu), not an application with its own rail — see
 * `buildSidebarNav` in `layouts/navigation.ts`.
 */
const SIDEBAR_SURFACES: ReadonlySet<RouteSurface> = new Set(['application'])

/**
 * Rail + header + scrolling content, over an ambient depth wash. The wash
 * sits at z-0 and the chrome at z-1, so every translucent surface has
 * something to blur.
 *
 * Which rail (if any) renders is read off the matched route tree, not the
 * pathname: `routes/index.tsx` tags every top-level route group with a
 * `{ surface, navScope }` handle. Only an 'application' surface mounts a
 * `Sidebar`, scoped to its `navScope` — Home, every Utility route and the
 * Developer tools render with none. Adding a second domain application
 * later means tagging its route group, not touching this component.
 */
export function DashboardLayout() {
  useRouteChangeTracking()
  const matches = useMatches()
  const { pathname } = useLocation()
  const handle = matches.map((match) => match.handle as RouteHandle | undefined).find((value) => value?.surface)
  const rail = handle?.surface && SIDEBAR_SURFACES.has(handle.surface) ? handle.navScope : undefined
  const setLastVisitedRoute = useAppStore((s) => s.setLastVisitedRoute)

  // Remember where the user was in each application, so the Applications menu
  // can resume it instead of always landing on the app's default page.
  useEffect(() => {
    if (handle?.surface === 'application' && handle.navScope) setLastVisitedRoute(handle.navScope, pathname)
  }, [handle?.surface, handle?.navScope, pathname, setLastVisitedRoute])

  return (
    <div className="bg-background relative isolate flex min-h-screen">
      <div aria-hidden className="nx-ambient" />

      {rail && (
        <div className="sticky top-0 z-[2] hidden h-screen sm:block">
          <Sidebar scope={rail} />
        </div>
      )}

      <div className="relative z-[1] flex min-w-0 flex-1 flex-col">
        <Topbar scope={rail} />
        <main className="flex-1 px-4 py-5 sm:px-7 sm:py-6">
          <Outlet />
        </main>
        <OverlayHost />
      </div>
    </div>
  )
}
