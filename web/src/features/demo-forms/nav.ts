import { TextCursorInput } from 'lucide-react'
import type { NavContribution } from '@/lib/navigation/types'
import { paths } from '@/routes/paths'
export const demoFormsNav = { group: 'developer', scope: 'developer', item: { id: 'demo-forms', labelKey: 'items.demoForms', to: paths.demoForms, icon: TextCursorInput } } satisfies NavContribution
