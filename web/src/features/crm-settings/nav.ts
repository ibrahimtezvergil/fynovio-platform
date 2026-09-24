import { Settings2 } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'

export const crmSettingsNav = {
  group: 'operations',
  item: { id: 'crm-settings', labelKey: 'items.crmSettings', to: paths.crmSettings, icon: Settings2 },
  scope: 'crm',
} satisfies NavContribution
