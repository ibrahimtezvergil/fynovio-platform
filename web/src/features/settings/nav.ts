import { Settings } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'

export const settingsNav = {
  group: 'system', item: { id: 'settings', labelKey: 'items.settings', to: paths.settings, icon: Settings },
} satisfies NavContribution
