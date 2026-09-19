import { Table2 } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'
export const demoTablesNav = { group: 'developer', scope: 'developer', parentId: 'demo-data', item: { id: 'demo-tables', labelKey: 'items.demoTables', to: paths.demoTables, icon: Table2 } } satisfies NavContribution
