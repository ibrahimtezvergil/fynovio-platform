import { MonitorCog } from 'lucide-react'
import { useAppStore } from '@/store/useAppStore'
import { registerAction } from './registry'

registerAction({
  id: 'appearance.toggle-theme',
  label: 'Toggle theme',
  icon: MonitorCog,
  when: () => true,
  run: () => {
    const { setTheme, theme } = useAppStore.getState()
    setTheme(theme === 'dark' ? 'light' : 'dark')
  },
})
