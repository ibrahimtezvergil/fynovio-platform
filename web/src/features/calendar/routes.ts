import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'

export const calendarRoutes = [
  {
    path: paths.calendar,
    protected: true,
    load: () => import('./pages/CalendarPage'),
  },
] satisfies FeatureRoute[]
