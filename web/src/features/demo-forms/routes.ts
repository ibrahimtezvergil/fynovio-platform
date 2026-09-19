import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'
export const demoFormsRoutes = [{ path: paths.demoForms, protected: true, load: () => import('./pages/FormsDemoPage') }] satisfies FeatureRoute[]
