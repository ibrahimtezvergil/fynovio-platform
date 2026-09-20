import { Navigate, Outlet, useSearchParams } from 'react-router-dom'
import { returnUrlQuery, sanitizeReturnUrl, useSessionStore } from '@/lib/auth'
import { paths } from '@/routes/paths'
import { RouteFallback } from '@/routes/RouteFallback'

/**
 * Keeps signed-in users off /login. It is also what carries a fresh sign-in to
 * its destination: the login form only updates the session, and this guard
 * reacts — so there is exactly one place that decides where a session goes.
 */
export function PublicOnlyRoute() {
  const status = useSessionStore((s) => s.status)
  const noMembership = useSessionStore((s) => s.noMembership)
  const [params] = useSearchParams()
  const returnUrl = sanitizeReturnUrl(params.get('returnUrl'))

  switch (status) {
    case 'unknown':
      return <RouteFallback />
    case 'authenticated':
      return <Navigate to={returnUrl} replace />
    case 'tenant_unresolved':
      return <Navigate to={noMembership ? paths.noAccess : `${paths.selectTenant}${returnUrlQuery(returnUrl)}`} replace />
    default:
      return <Outlet />
  }
}
