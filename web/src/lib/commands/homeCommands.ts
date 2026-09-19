import { Plus } from 'lucide-react'
import { paths } from '@/routes/paths'
import { registerCommand } from './registry'

/**
 * The command bar's one "quick action" (master prompt §7). No quote-creation
 * flow exists yet, so this navigates to Pipeline rather than faking a create —
 * the honest placeholder the brief asks for when a real action needs a backend.
 */
registerCommand({
  id: 'home.quick-new-quote',
  label: 'Yeni teklif oluştur',
  group: 'Hızlı İşlemler',
  icon: Plus,
  when: () => true,
  run: ({ navigate }) => navigate(paths.crmPipeline),
})
