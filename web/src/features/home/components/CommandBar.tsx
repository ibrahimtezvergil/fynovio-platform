import { Search } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useCommandPaletteStore } from '@/lib/commands'

/**
 * The dashboard's own launcher for the *same* command palette the topbar's
 * search opens (`src/layouts/components/Topbar.tsx`) — one underlying
 * surface (`useCommandPaletteStore`), two entry points, per the brief's
 * explicit "don't build a second search system" rule. This button does not
 * mount its own `CommandPalette`; the Topbar's instance is the only one.
 */
export function CommandBar() {
  const { t } = useTranslation('home')
  const setOpen = useCommandPaletteStore((s) => s.setOpen)

  return (
    <button
      type="button"
      onClick={() => setOpen(true)}
      aria-label={t('commandBar.ariaLabel')}
      aria-keyshortcuts="Meta+K Control+K"
      className="nx-search h-11 w-full shrink-0 text-left"
    >
      <Search aria-hidden className="text-muted-foreground size-[18px] shrink-0" strokeWidth={1.7} />
      <span className="flex-1 truncate text-[14px] text-muted-foreground">{t('commandBar.placeholder')}</span>
      <kbd aria-hidden className="nx-kbd">
        ⌘K
      </kbd>
    </button>
  )
}
