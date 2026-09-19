import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'
export const demoBadgesRoutes = [{ path: paths.demoBadges, protected: true, load: () => import('./pages/BadgesDemoPage') }] satisfies FeatureRoute[]
