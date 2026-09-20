import { BarChart3, ClipboardList, Folder, Users } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'

/** Global utility destinations — no sidebar scope, they never own a rail. */
export const placeholderNav = [
  { group: 'operations', item: { id: 'reports', labelKey: 'items.reports', to: paths.reports, icon: BarChart3 } },
  { group: 'operations', item: { id: 'files', labelKey: 'items.files', to: paths.files, icon: Folder } },
  { group: 'operations', item: { id: 'feedback', labelKey: 'items.feedback', to: paths.feedback, icon: ClipboardList } },
] satisfies NavContribution[]

/** Platform administration — a global page like the utilities above, no rail. */
export const membersNav = {
  group: 'system', item: { id: 'members', labelKey: 'items.members', to: paths.members, icon: Users },
} satisfies NavContribution
