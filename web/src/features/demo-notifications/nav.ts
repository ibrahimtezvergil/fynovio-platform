import { BellRing } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'
export const demoNotificationsNav = { group: 'developer', scope: 'developer', parentId: 'demo-components', item: { id: 'demo-notifications', labelKey: 'items.demoNotifications', to: paths.demoNotifications, icon: BellRing } } satisfies NavContribution
