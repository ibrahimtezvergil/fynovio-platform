import { dashboardHandlers } from '@/mocks/handlers/dashboard'
import { demoFormsHandlers } from '@/mocks/handlers/demo-forms'
import { demoTableHandlers } from '@/mocks/handlers/demo-tables'
import { homeHandlers } from '@/mocks/handlers/home'
import { pipelineHandlers } from '@/mocks/handlers/pipeline'

/** Every feature's network mock, combined at the boundary MSW intercepts. */
export const handlers = [
  ...homeHandlers,
  ...dashboardHandlers,
  ...pipelineHandlers,
  ...demoTableHandlers,
  ...demoFormsHandlers,
]
