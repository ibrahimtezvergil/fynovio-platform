import { Gauge } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'

/** The legacy sales dashboard — a CRM module, on the CRM application's own rail. */
export const dashboardNav = {
  group: 'workspace',
  item: { id: 'crmDashboard', labelKey: 'items.crmDashboard', to: paths.crmDashboard, icon: Gauge },
  scope: 'crm',
} satisfies NavContribution
