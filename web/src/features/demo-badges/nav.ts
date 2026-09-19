import { Tag } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'
export const demoBadgesNav = { group: 'developer', scope: 'developer', parentId: 'demo-components', item: { id: 'demo-badges', labelKey: 'items.demoBadges', to: paths.demoBadges, icon: Tag } } satisfies NavContribution
