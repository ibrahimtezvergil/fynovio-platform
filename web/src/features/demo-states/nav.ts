import { LoaderCircle } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'
export const demoStatesNav = { group: 'developer', scope: 'developer', parentId: 'demo-components', item: { id: 'demo-states', labelKey: 'items.demoStates', to: paths.demoStates, icon: LoaderCircle } } satisfies NavContribution
