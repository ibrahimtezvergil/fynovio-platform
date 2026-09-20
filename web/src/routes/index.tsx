import { lazy, Suspense, type ReactElement } from 'react'
import { createBrowserRouter, Navigate, Outlet, type RouteObject } from 'react-router-dom'
import { authAccountRoutes, authLinkRoutes, authRoutes, authSessionRoutes } from '@/features/auth/routes'
import { calendarRoutes } from '@/features/calendar/routes'
import { dashboardRoutes } from '@/features/dashboard/routes'
import { demoBadgesRoutes } from '@/features/demo-badges/routes'
import { demoChartsRoutes } from '@/features/demo-charts/routes'
import { demoDrawersRoutes } from '@/features/demo-drawers/routes'
import { demoFiltersRoutes } from '@/features/demo-filters/routes'
import { demoFormsRoutes } from '@/features/demo-forms/routes'
import { demoKanbanRoutes } from '@/features/demo-kanban/routes'
import { demoNotificationsRoutes } from '@/features/demo-notifications/routes'
import { demoOverlaysRoutes } from '@/features/demo-overlays/routes'
import { demoStatesRoutes } from '@/features/demo-states/routes'
import { demoTablesRoutes } from '@/features/demo-tables/routes'
import { demoTimelineRoutes } from '@/features/demo-timeline/routes'
import { homeRoutes } from '@/features/home/routes'
import { opportunityRoutes } from '@/features/opportunities/routes'
import { placeholderUtilityRoutes } from '@/features/placeholder/routes'
import { settingsRoutes } from '@/features/settings/routes'
import type { FeatureRoute } from '@/lib/routes/types'
import { AuthLayout } from '@/layouts/AuthLayout'
import { DashboardLayout } from '@/layouts/DashboardLayout'
import { paths } from '@/routes/paths'
import { ProtectedRoute } from '@/routes/ProtectedRoute'
import { PublicOnlyRoute } from '@/routes/PublicOnlyRoute'
import { RouteErrorBoundary } from '@/routes/RouteErrorBoundary'
import { RouteFallback } from '@/routes/RouteFallback'
import { SessionRoute } from '@/routes/SessionRoute'

const NotFoundPage = lazy(() => import('@/routes/NotFoundPage'))

const suspended = (element: ReactElement) => <Suspense fallback={<RouteFallback />}>{element}</Suspense>
const routeElement = (route: FeatureRoute): ReactElement => {
  if ('element' in route) return route.element
  const Page = lazy(route.load)
  return suspended(<Page />)
}
/** Feature routes turned into router objects, keeping only the ones matching `protected`. */
const toRouteObjects = (routes: FeatureRoute[], isProtected: boolean): RouteObject[] =>
  routes.filter((route) => route.protected === isProtected).map((route) => ({ path: route.path, element: routeElement(route) }))

const publicRoutes = toRouteObjects(authRoutes, false)
const linkRoutes = toRouteObjects(authLinkRoutes, false)
const sessionRoutes = toRouteObjects(authSessionRoutes, true)

/**
 * Global Home. Its own pathless group purely for symmetry with the others
 * below — `surface: 'home'` isn't load-bearing for `DashboardLayout` (no
 * sidebar is the default for any surface outside `SIDEBAR_SURFACES`), but
 * keeping it explicit here is what makes the shell's four-surface model
 * (`RouteSurface`) a complete, self-documenting route tree rather than one
 * with an implicit "everything else" case.
 */
const homeRouteGroup: RouteObject = {
  handle: { surface: 'home' },
  children: toRouteObjects(homeRoutes, true),
}

/**
 * Global utilities — cross-domain tools that are not a business application,
 * plus platform pages that own no rail: the signed-in user's Settings
 * (`/profile/settings`, reached from the user menu) and Members.
 * No `navScope`, so `DashboardLayout` renders no sidebar for any of them:
 * opening Calendar should feel like opening a tool, not entering CRM. Add a
 * new utility page's feature routes here, not a new group.
 */
const utilityRoutes: RouteObject = {
  handle: { surface: 'utility' },
  children: toRouteObjects([...calendarRoutes, ...placeholderUtilityRoutes, ...settingsRoutes, ...authAccountRoutes], true),
}

/**
 * CRM — the first domain application. Its own route group, tagged
 * `navScope: 'crm'` so `DashboardLayout` renders CRM's own rail instead of
 * any other. A second domain application (e.g. an eventual Inventory) is a
 * sibling of this object with its own path/handle/children, not a branch
 * grafted into it — nothing here special-cases "crm" by name.
 */
const crmRoutes: RouteObject = {
  path: paths.crm,
  element: <Outlet />,
  handle: { surface: 'application', navScope: 'crm' },
  children: [
    { index: true, element: <Navigate to={paths.crmDashboard} replace /> },
    ...toRouteObjects([...opportunityRoutes, ...dashboardRoutes], true),
  ],
}

/**
 * Cross-cutting dev tooling (demo/playground pages) — never a business
 * domain. Excluded from the router entirely outside `import.meta.env.DEV`
 * (see below), not just hidden from nav: a production build must not let
 * someone reach these by typing the URL.
 */
const developerRoutes: RouteObject = {
  handle: { surface: 'developer', navScope: 'developer' },
  children: toRouteObjects(
    [
      ...demoTablesRoutes, ...demoFormsRoutes, ...demoChartsRoutes, ...demoOverlaysRoutes,
      ...demoNotificationsRoutes, ...demoStatesRoutes, ...demoDrawersRoutes, ...demoKanbanRoutes,
      ...demoTimelineRoutes, ...demoBadgesRoutes, ...demoFiltersRoutes,
    ],
    true,
  ),
}

/** Moved URLs (pre-CRM, and Settings before `/profile/settings`) — kept working as redirects, not navigable anywhere. */
const legacyRedirects: RouteObject[] = [
  { path: paths.pipeline, element: <Navigate to={paths.crmOpportunities} replace /> },
  { path: paths.crmLegacyPipeline, element: <Navigate to={paths.crmOpportunities} replace /> },
  { path: paths.pipelineDashboard, element: <Navigate to={paths.crmDashboard} replace /> },
  { path: paths.legacySettings, element: <Navigate to={paths.settings} replace /> },
]

export const router = createBrowserRouter([{
  errorElement: <RouteErrorBoundary />,
  children: [
    { path: '/', element: <Navigate to={paths.dashboard} replace /> },
    { element: <PublicOnlyRoute />, children: [{ element: <AuthLayout />, children: publicRoutes }] },
    { element: <AuthLayout />, children: linkRoutes },
    { element: <SessionRoute />, children: [{ element: <AuthLayout />, children: sessionRoutes }] },
    {
      element: <ProtectedRoute />,
      children: [{
        element: <DashboardLayout />,
        children: [
          homeRouteGroup,
          utilityRoutes,
          crmRoutes,
          ...(import.meta.env.DEV ? [developerRoutes] : []),
          ...legacyRedirects,
        ],
      }],
    },
    { path: '*', element: suspended(<NotFoundPage />) },
  ],
}])
