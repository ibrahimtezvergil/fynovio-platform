import { PanelLeftClose, PanelLeftOpen } from 'lucide-react'
import { useEffect, useState, useSyncExternalStore } from 'react'
import { useTranslation } from 'react-i18next'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { cn } from '@/lib/utils'
import type { NavScope } from '@/lib/navigation/types'
import { useSidebar } from '@/store/useAppStore'
import { registerShortcut } from '@/lib/shortcuts'
import { SidebarNav } from './SidebarNav'
import { TenantMenu } from './TenantMenu'

/** The physical key to the right of P, whatever this layout prints on it. */
const BRACKET_CODE = 'BracketLeft'
/** Tailwind's `lg` (64rem), inverted: below it the shell forces the rail. Keep in step with `lg:` in DashboardLayout/MobileNavigation. */
const RAIL_QUERY = '(width < 64rem)'

const IS_APPLE = /Mac|iP(hone|ad|od)/.test(navigator.userAgent)

/**
 * ⌘[ / Ctrl+[ flips the rail — bound to the key's *position*, not its character.
 *
 * `event.key` alone does not survive a Turkish layout. There `[` is AltGr+8
 * (Option+8 on macOS), and on Windows AltGr reports as Ctrl+Alt — so matching
 * the character would be indistinguishable from someone simply typing a
 * bracket, and requiring the modifier turns a two-key chord into a stretch.
 * `event.code` sidesteps both: the same physical key prints Ğ on a Turkish Q
 * board, so the chord stays two keys everywhere and `useShortcutHint` is what
 * tells the user which cap to look for.
 *
 * Alt is excluded deliberately — that is what keeps a typed `[` from firing it.
 */
function useSidebarShortcut(toggle: () => void, enabled: boolean) {
  useEffect(() => {
    if (!enabled) return
    return registerShortcut({
      id: 'shell.sidebar',
      matches: (event) => {
      // `key` stays as a fallback for layouts that print a bracket elsewhere.
      const isBracket = event.code === BRACKET_CODE || event.key === '['
        return isBracket && (event.metaKey || event.ctrlKey) && !event.altKey
      },
      run: toggle,
    })
  }, [enabled, toggle])
}

function subscribeToRailBreakpoint(onStoreChange: () => void) {
  const media = window.matchMedia(RAIL_QUERY)
  media.addEventListener('change', onStoreChange)
  return () => media.removeEventListener('change', onStoreChange)
}

function useRailBreakpoint() {
  return useSyncExternalStore(subscribeToRailBreakpoint, () => window.matchMedia(RAIL_QUERY).matches, () => false)
}

/**
 * What to print on the key cap.
 *
 * The shortcut is bound to a position, so the hint has to name whatever that
 * position prints here — `⌘[` is a lie on a Turkish board, where the key is Ğ.
 * The layout is an external system, read once and cached; where `getLayoutMap`
 * is missing (Safari, Firefox) the default is already right, because those are
 * the engines whose layouts print the bracket.
 */
function useShortcutHint(): string {
  const [keyCap, setKeyCap] = useState('[')

  useEffect(() => {
    const { keyboard } = navigator as Navigator & {
      keyboard?: { getLayoutMap?: () => Promise<Map<string, string>> }
    }
    if (!keyboard?.getLayoutMap) return

    let active = true
    keyboard
      .getLayoutMap()
      .then((layout) => {
        const printed = layout.get(BRACKET_CODE)
        if (active && printed) setKeyCap(printed.toLocaleUpperCase('tr'))
      })
      .catch(() => {
        // Layout unreadable — the default reads fine.
      })

    return () => {
      active = false
    }
  }, [])

  return keyCap
}

/** One rail component, reused per rail: the shell's shared one and every domain application's own — `scope` picks its nav slice, nothing else about it varies. */
export function Sidebar({ scope }: { scope: NavScope }) {
  const { t } = useTranslation('nav')
  const { collapsed: preferredCollapsed, toggle } = useSidebar()
  const railOnly = useRailBreakpoint()
  const collapsed = railOnly || preferredCollapsed
  useSidebarShortcut(toggle, !railOnly)
  const keyCap = useShortcutHint()

  const shortcutHint = IS_APPLE ? `⌘${keyCap}` : `Ctrl+${keyCap}`
  const toggleLabel = collapsed ? t('sidebar.expandMenu') : t('sidebar.collapseMenu')
  const ToggleIcon = collapsed ? PanelLeftOpen : PanelLeftClose

  return (
    <aside
      data-collapsed={collapsed || undefined}
      className={cn(
        'nx-material flex h-full shrink-0 flex-col gap-5 rounded-none border-0 border-r p-[18px_14px_14px] shadow-[inset_-1px_0_0_var(--nx-specular)]',
        'transition-[width] duration-[300ms] ease-fluid',
        collapsed ? 'w-sidebar-collapsed' : 'w-sidebar',
      )}
    >
      {/* brand — the mark holds its place, the wordmark is what the rail drops */}
      <div className={cn('flex items-center gap-[11px] pt-1.5', collapsed ? 'px-0.5' : 'px-2')}>
        <span
          aria-hidden
          className="flex size-[34px] shrink-0 items-center justify-center rounded-[var(--nx-r-ctl-sm)] bg-[image:var(--nx-accent-grad)] text-[16px] font-[650] tracking-[-0.02em] text-white shadow-[var(--nx-accent-glow),inset_0_1px_0_rgb(255_255_255/0.35)]"
        >
          F
        </span>
        {!collapsed && (
          <span className="font-heading truncate text-[16.5px] font-semibold tracking-[-0.026em]">
            Fynovio
          </span>
        )}
      </div>

      <TenantMenu collapsed={collapsed} />

      <SidebarNav collapsed={collapsed} scope={scope} />

      <div className={cn('flex flex-col gap-2', collapsed && 'items-center', railOnly && 'hidden')}>
        <Tooltip>
          <TooltipTrigger
            disabled={!collapsed}
            render={
              <button
                type="button"
                onClick={toggle}
                aria-label={toggleLabel}
                aria-keyshortcuts={`Meta+${keyCap} Control+${keyCap}`}
                className={cn(
                  'nx-nav-item w-full cursor-pointer',
                  collapsed && 'nx-nav-item--rail',
                )}
              />
            }
          >
            <ToggleIcon aria-hidden className="size-[18px] shrink-0" strokeWidth={1.7} />
            {!collapsed && (
              <>
                <span className="flex-1 truncate text-left">{toggleLabel}</span>
                <kbd aria-hidden className="nx-kbd">
                  {shortcutHint}
                </kbd>
              </>
            )}
          </TooltipTrigger>
          <TooltipContent side="right" sideOffset={10}>
            {toggleLabel}
            <kbd data-slot="kbd" className="nx-kbd">
              {shortcutHint}
            </kbd>
          </TooltipContent>
        </Tooltip>

      </div>
    </aside>
  )
}
