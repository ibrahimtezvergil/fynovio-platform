import { Navigate, Outlet } from 'react-router-dom'
import { useAuthStore } from '@/features/auth/store/useAuthStore'
import { paths } from '@/routes/paths'

/** Keeps signed-in users off /login. */
export function PublicOnlyRoute() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  return isAuthenticated ? <Navigate to={paths.dashboard} replace /> : <Outlet />
}
