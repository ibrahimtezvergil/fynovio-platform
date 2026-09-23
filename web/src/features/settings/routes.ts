import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'

export const settingsRoutes = [
  { path: paths.settings, protected: true, load: () => import('./pages/SettingsPage') },
  { path: `${paths.settings}/:section`, protected: true, load: () => import('./pages/SettingsPage') },
] satisfies FeatureRoute[]
