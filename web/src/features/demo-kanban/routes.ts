import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'
export const demoKanbanRoutes = [{ path: paths.demoKanban, protected: true, load: () => import('./pages/KanbanDemoPage') }] satisfies FeatureRoute[]
