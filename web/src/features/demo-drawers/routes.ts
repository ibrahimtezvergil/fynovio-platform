import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'
export const demoDrawersRoutes = [{ path: paths.demoDrawers, protected: true, load: () => import('./pages/DrawersDemoPage') }] satisfies FeatureRoute[]
