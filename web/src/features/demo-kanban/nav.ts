import { SquareKanban } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'
export const demoKanbanNav = { group: 'developer', scope: 'developer', parentId: 'demo-components', item: { id: 'demo-kanban', labelKey: 'items.demoKanban', to: paths.demoKanban, icon: SquareKanban } } satisfies NavContribution
