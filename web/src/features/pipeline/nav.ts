import { Columns3 } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'

/** The CRM application's own rail — see `scope`, not the shell's shared one. */
export const pipelineNav = {
  group: 'workspace',
  item: { id: 'pipeline', labelKey: 'items.pipeline', to: paths.crmPipeline, icon: Columns3, badge: 38 },
  scope: 'crm',
} satisfies NavContribution
