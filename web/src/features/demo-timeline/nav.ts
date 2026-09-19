import { History } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'
export const demoTimelineNav = { group: 'developer', scope: 'developer', parentId: 'demo-components', item: { id: 'demo-timeline', labelKey: 'items.demoTimeline', to: paths.demoTimeline, icon: History } } satisfies NavContribution
