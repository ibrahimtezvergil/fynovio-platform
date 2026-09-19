import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'

export const homeRoutes = [
  {
    path: paths.dashboard,
    protected: true,
    load: () => import('./pages/HomePage'),
  },
] satisfies FeatureRoute[]
