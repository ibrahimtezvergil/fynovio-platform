import { useTranslation } from 'react-i18next'
import { SegmentedControl, type Segment } from '@/components/common/SegmentedControl'
import { useAppStore } from '@/store/useAppStore'
import type { ThemePreference } from '@/types'

export function ThemeToggle() {
  const { t } = useTranslation('common')
  const theme = useAppStore((s) => s.theme)
  const setTheme = useAppStore((s) => s.setTheme)

  const segments: readonly Segment<ThemePreference>[] = [
    { value: 'light', label: t('themeToggle.light') },
    { value: 'dark', label: t('themeToggle.dark') },
    { value: 'system', label: t('themeToggle.system') },
  ]

  return (
    <SegmentedControl
      aria-label={t('themeToggle.ariaLabel')}
      segments={segments}
      value={theme}
      onChange={setTheme}
      fullWidth
    />
  )
}
