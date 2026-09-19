import { Layers } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'
export const demoOverlaysNav = { group: 'developer', scope: 'developer', parentId: 'demo-components', item: { id: 'demo-overlays', labelKey: 'items.demoOverlays', to: paths.demoOverlays, icon: Layers } } satisfies NavContribution
