import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'
export const demoFiltersRoutes = [{ path: paths.demoFilters, protected: true, load: () => import('./pages/FiltersDemoPage') }] satisfies FeatureRoute[]
