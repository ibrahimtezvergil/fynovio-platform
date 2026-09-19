import { ChartColumnBig } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'
export const demoChartsNav = { group: 'developer', scope: 'developer', parentId: 'demo-data', item: { id: 'demo-charts', labelKey: 'items.demoCharts', to: paths.demoCharts, icon: ChartColumnBig } } satisfies NavContribution
