import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'
export const demoNotificationsRoutes = [{ path: paths.demoNotifications, protected: true, load: () => import('./pages/NotificationsDemoPage') }] satisfies FeatureRoute[]
