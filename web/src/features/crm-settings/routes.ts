import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'

export const crmSettingsRoutes = [
  { path: paths.crmSettings, protected: true, load: () => import('./pages/CrmSettingsPage') },
  { path: `${paths.crmSettings}/:section`, protected: true, load: () => import('./pages/CrmSettingsPage') },
] satisfies FeatureRoute[]
