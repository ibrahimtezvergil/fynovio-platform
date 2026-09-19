import { Calendar } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'

export const calendarNav = {
  group: 'workspace',
  item: { id: 'calendar', labelKey: 'items.calendar', to: paths.calendar, icon: Calendar },
  surface: 'topbar',
} satisfies NavContribution
