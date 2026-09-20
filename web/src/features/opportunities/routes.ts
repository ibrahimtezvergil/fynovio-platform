import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'

/** Order matters to nothing (react-router ranks by specificity), but `new` is listed before `:id` for the reader. */
export const opportunityRoutes = [
  { path: paths.crmOpportunities, protected: true, load: () => import('./pages/OpportunitiesPage') },
  { path: paths.crmOpportunityNew, protected: true, load: () => import('./pages/OpportunityNewPage') },
  { path: paths.crmOpportunityPattern, protected: true, load: () => import('./pages/OpportunityDetailPage') },
] satisfies FeatureRoute[]
