import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'
export const demoTimelineRoutes = [{ path: paths.demoTimeline, protected: true, load: () => import('./pages/TimelineDemoPage') }] satisfies FeatureRoute[]
