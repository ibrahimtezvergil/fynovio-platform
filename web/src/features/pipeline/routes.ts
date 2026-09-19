import type { FeatureRoute } from '@/lib/routes/types'
import { paths } from '@/routes/paths'

export const pipelineRoutes = [
  {
    path: paths.crmPipeline,
    protected: true,
    load: () => import('./pages/PipelinePage'),
  },
] satisfies FeatureRoute[]
