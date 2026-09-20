import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { returnUrlQuery, useSessionStore } from '@/lib/auth'
import { paths } from '@/routes/paths'
import { RouteFallback } from '@/routes/RouteFallback'

/**
 * Gate for the private tree — UX/navigation control, not the security
 * boundary (the API authorises every request). Renders the app only for a
 * session WITH an active tenant; every other state is sent where it can be
 * resolved, remembering the deep link through a sanitized `returnUrl`.
 */
export function ProtectedRoute() {
  const status = useSessionStore((s) => s.status)
  const noMembership = useSessionStore((s) => s.noMembership)
  const explicitSignOut = useSessionStore((s) => s.explicitSignOut)
  const location = useLocation()
  const current = `${location.pathname}${location.search}${location.hash}`

  switch (status) {
    case 'unknown':
      return <RouteFallback />
    case 'authenticated':
      return <Outlet />
    case 'tenant_unresolved':
      return <Navigate to={noMembership ? paths.noAccess : `${paths.selectTenant}${returnUrlQuery(current)}`} replace />
    default:
      // After an explicit "sign out" the login screen must not carry the page the user just left.
      return <Navigate to={`${paths.login}${explicitSignOut ? '' : returnUrlQuery(current)}`} replace />
  }
}
