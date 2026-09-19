import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'

export const dashboardRoutes = [
  {
    path: paths.crmDashboard,
    protected: true,
    load: () => import('./pages/DashboardPage'),
  },
] satisfies FeatureRoute[]
