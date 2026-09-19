import { Funnel } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'
export const demoFiltersNav = { group: 'developer', scope: 'developer', parentId: 'demo-data', item: { id: 'demo-filters', labelKey: 'items.demoFilters', to: paths.demoFilters, icon: Funnel } } satisfies NavContribution
