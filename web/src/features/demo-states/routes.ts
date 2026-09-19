import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'
export const demoStatesRoutes = [{ path: paths.demoStates, protected: true, load: () => import('./pages/StatesDemoPage') }] satisfies FeatureRoute[]
