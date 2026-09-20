import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { returnUrlQuery, useSessionStore } from '@/lib/auth'
import { paths } from '@/routes/paths'
import { RouteFallback } from '@/routes/RouteFallback'

/** Pages for someone who is signed in but may not have a tenant yet (tenant selection, no-access). */
export function SessionRoute() {
  const status = useSessionStore((s) => s.status)
  const explicitSignOut = useSessionStore((s) => s.explicitSignOut)
  const location = useLocation()

  if (status === 'unknown') return <RouteFallback />
  if (status === 'unauthenticated' || status === 'expired') {
    const current = `${location.pathname}${location.search}${location.hash}`
    return <Navigate to={`${paths.login}${explicitSignOut ? '' : returnUrlQuery(current)}`} replace />
  }
  return <Outlet />
}
