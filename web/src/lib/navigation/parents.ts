import { Blocks, Database } from 'lucide-react'
import type { NavParentDefinition } from './types'

export const navParents = {
  'demo-data': { id: 'demo-data', labelKey: 'items.demoData', icon: Database },
  'demo-components': { id: 'demo-components', labelKey: 'items.demoComponents', icon: Blocks },
} satisfies Record<string, NavParentDefinition>
