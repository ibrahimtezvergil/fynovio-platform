import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'

/** Public pages: only reachable while signed out. */
export const authRoutes = [
  { path: paths.login, protected: false, load: () => import('./pages/LoginPage') },
  { path: paths.forgotPassword, protected: false, load: () => import('./pages/ForgotPasswordPage') },
  { path: paths.register, protected: false, load: () => import('./pages/RegisterPage') },
] satisfies FeatureRoute[]

/**
 * Pages opened from an e-mailed link. Reachable whatever the session state (no `PublicOnlyRoute`): the link's
 * single-use token is the credential, and bouncing a signed-in tester away would lose it.
 */
export const authLinkRoutes = [
  { path: paths.acceptInvite, protected: false, load: () => import('./pages/AcceptInvitePage') },
  { path: paths.resetPassword, protected: false, load: () => import('./pages/ResetPasswordPage') },
] satisfies FeatureRoute[]

/** Pages for a signed-in session that has no active tenant yet — assembled under `SessionRoute`, not the tenant-scoped shell. */
export const authSessionRoutes = [
  { path: paths.selectTenant, protected: true, load: () => import('./pages/TenantSelectorPage') },
  { path: paths.noAccess, protected: true, load: () => import('./pages/NoAccessPage') },
] satisfies FeatureRoute[]

/** Account pages inside the application shell (a tenant is active). */
export const authAccountRoutes = [
  { path: paths.accountSecurity, protected: true, load: () => import('./pages/AccountSecurityPage') },
] satisfies FeatureRoute[]
