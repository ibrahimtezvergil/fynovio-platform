import { createElement } from 'react'
import { PlaceholderPage } from './pages/PlaceholderPage'
import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'

/** Global utility pages — mounted with no sidebar (see `routes/index.tsx`'s utility route group). */
export const placeholderUtilityRoutes = [
  { path: paths.reports, protected: true, element: createElement(PlaceholderPage, { titleKey: 'reports' }) },
  { path: paths.files, protected: true, element: createElement(PlaceholderPage, { titleKey: 'files' }) },
  { path: paths.feedback, protected: true, element: createElement(PlaceholderPage, { titleKey: 'feedback' }) },
] satisfies FeatureRoute[]

/** Platform administration pages — mounted on the System rail. */
export const placeholderSystemRoutes = [
  { path: paths.members, protected: true, element: createElement(PlaceholderPage, { titleKey: 'members' }) },
] satisfies FeatureRoute[]
