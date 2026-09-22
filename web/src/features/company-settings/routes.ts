import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'

export const companySettingsRoutes = [
  { path: paths.companySettings, protected: true, load: () => import('./pages/CompanySettingsPage') },
] satisfies FeatureRoute[]
