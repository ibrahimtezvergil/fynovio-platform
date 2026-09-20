import { Handshake } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'

export const opportunitiesNav = {
  group: 'workspace',
  item: { id: 'opportunities', labelKey: 'items.opportunities', to: paths.crmOpportunities, icon: Handshake },
  scope: 'crm',
} satisfies NavContribution
