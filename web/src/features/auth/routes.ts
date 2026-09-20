import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'

/** Public pages: only reachable while signed out. */
export const authRoutes = [
  { path: paths.login, protected: false, load: () => import('./pages/LoginPage') },
] satisfies FeatureRoute[]

/** Pages for a signed-in session that has no active tenant yet — assembled under `SessionRoute`, not the tenant-scoped shell. */
export const authSessionRoutes = [
  { path: paths.selectTenant, protected: true, load: () => import('./pages/TenantSelectorPage') },
  { path: paths.noAccess, protected: true, load: () => import('./pages/NoAccessPage') },
] satisfies FeatureRoute[]
