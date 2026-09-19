import { LayoutDashboard } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'

export const homeNav = {
  group: 'workspace',
  item: { id: 'dashboard', labelKey: 'items.dashboard', to: paths.dashboard, icon: LayoutDashboard },
} satisfies NavContribution
