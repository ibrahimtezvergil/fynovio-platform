import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'
export const demoChartsRoutes = [{ path: paths.demoCharts, protected: true, load: () => import('./pages/ChartsDemoPage') }] satisfies FeatureRoute[]
