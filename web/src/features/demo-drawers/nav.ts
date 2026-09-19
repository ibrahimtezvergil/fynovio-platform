import { PanelRight } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'
export const demoDrawersNav = { group: 'developer', scope: 'developer', parentId: 'demo-components', item: { id: 'demo-drawers', labelKey: 'items.demoDrawers', to: paths.demoDrawers, icon: PanelRight } } satisfies NavContribution
