import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'
export const demoOverlaysRoutes = [{ path: paths.demoOverlays, protected: true, load: () => import('./pages/OverlaysDemoPage') }] satisfies FeatureRoute[]
