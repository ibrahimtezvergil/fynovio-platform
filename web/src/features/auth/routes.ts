import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'

export const authRoutes = [
  { path: paths.login, protected: false, load: () => import('./pages/LoginPage') },
] satisfies FeatureRoute[]
