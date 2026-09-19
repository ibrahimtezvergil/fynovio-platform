import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'
export const demoTablesRoutes = [{ path: paths.demoTables, protected: true, load: () => import('./pages/TablesDemoPage') }] satisfies FeatureRoute[]
