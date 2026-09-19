import { ChevronRight, Moon, Search, Sun } from 'lucide-react'
import { Fragment, useEffect, useMemo } from 'react'
import type { TFunction } from 'i18next'
import { useTranslation } from 'react-i18next'
import { NavLink, useLocation } from 'react-router-dom'
import { Button, buttonVariants } from '@/components/ui/button'
import { CommandPalette } from '@/components/common/CommandPalette'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { NotificationCenter } from '@/features/notifications/components/NotificationCenter'
import '@/lib/actions/appActions'
import '@/lib/commands/homeCommands'
import { applicationRegistry } from '@/lib/applications/registry'
import { useCommandPaletteStore } from '@/lib/commands'
import { registerShortcut } from '@/lib/shortcuts'
import { cn } from '@/lib/utils'
import { buildNavGroups, buildTopbarNav, findNavTrail } from '@/layouts/navigation'
import type { NavScope } from '@/lib/navigation/types'
import { useAppStore, useResolvedTheme } from '@/store/useAppStore'
import { ApplicationsMenu } from './ApplicationsMenu'
import { ConversationsButton } from '@/features/conversations/components/ConversationsButton'
import { ToolsMenu } from './ToolsMenu'
import { UserMenu } from './UserMenu'
import { MobileNavigation } from './MobileNavigation'

/**
 * The crumbs after the root: a nested page shows its parent, a flat one
 * doesn't. A path owned by a `kind: 'application'` registry entry (CRM today)
 * gets that application's name prepended — `Fynovio > CRM > Pipeline` — so a
 * domain application reads as a place, not a flat page list. Utility/system
 * routes are unaffected: they stay flat (`Fynovio > Takvim`).
 */
function useCrumbs(t: TFunction<'nav'>, tHome: TFunction<'home'>): string[] {
  const { pathname } = useLocation()
  const navGroups = useMemo(() => buildNavGroups(t), [t])
  const trail = findNavTrail(navGroups, pathname)
  const app = applicationRegistry.find((item) => item.kind === 'application' && item.route && pathname.startsWith(item.route))
  const appLabel = app ? tHome(app.labelKey) : undefined
  if (!trail) return appLabel ? [appLabel] : ['Fynovio']
  const pageCrumbs = trail.leaf ? [trail.item.label, trail.leaf.label] : [trail.item.label]
  return appLabel ? [appLabel, ...pageCrumbs] : pageCrumbs
}

export function Topbar({ scope }: { scope?: NavScope }) {
  const { t } = useTranslation('nav')
  const { t: tHome } = useTranslation('home')
  const crumbs = useCrumbs(t, tHome)
  const topbarNav = useMemo(() => buildTopbarNav(t), [t])
  const setTheme = useAppStore((s) => s.setTheme)
  // Resolved against the OS, so the toggle always flips what is on screen.
  const isDark = useResolvedTheme() === 'dark'
  // Shared across every entry point (this search button, Home's CommandBar,
  // ⌘K) so exactly one CommandPalette is ever mounted — see `paletteStore`.
  const commandPaletteOpen = useCommandPaletteStore((s) => s.open)
  const setCommandPaletteOpen = useCommandPaletteStore((s) => s.setOpen)

  // Keep the shortcut at the shell: every protected route shares this topbar.
  useEffect(() => {
    return registerShortcut({
      id: 'shell.command-palette',
      matches: (event) => event.code === 'KeyK' && (event.metaKey || event.ctrlKey),
      run: () => setCommandPaletteOpen(true),
    })
  }, [setCommandPaletteOpen])

  return (
    <header className="nx-material sticky top-0 z-[5] flex h-topbar shrink-0 items-center gap-2 rounded-none border-0 border-b px-3 max-xl:[&>button]:size-11 sm:px-4 lg:px-6 xl:gap-3.5">
      <MobileNavigation scope={scope} />
      <nav
        aria-label={t('topbar.breadcrumb')}
        className="text-muted-foreground hidden min-w-0 max-w-[30vw] items-center gap-[7px] text-[13px] xl:flex"
      >
        <span>Fynovio</span>
        {crumbs.map((crumb, index) => (
          <Fragment key={crumb}>
            <ChevronRight aria-hidden className="size-3 shrink-0" strokeWidth={1.7} />
            <span className={index === crumbs.length - 1 ? 'text-foreground font-[550]' : undefined}>
              {crumb}
            </span>
          </Fragment>
        ))}
      </nav>

      <span className="min-w-0 flex-1 truncate text-[13px] font-[550] xl:hidden">{crumbs.at(-1)}</span>
      <div className="hidden flex-1 xl:block" />

      <button
        type="button"
        className="nx-search hidden w-[340px] max-w-[34vw] text-left xl:flex"
        onClick={() => setCommandPaletteOpen(true)}
        aria-label={t('commandPalette.open')}
        aria-keyshortcuts="Meta+K Control+K"
      >
        <Search aria-hidden className="text-muted-foreground size-4 shrink-0" strokeWidth={1.7} />
        <span className="flex-1 truncate text-[13.5px] text-muted-foreground">{t('topbar.searchPlaceholder')}</span>
        <kbd aria-hidden className="nx-kbd">
          ⌘K
        </kbd>
      </button>

      <Button
        variant="secondary"
        size="icon"
        className="xl:hidden"
        onClick={() => setCommandPaletteOpen(true)}
        aria-label={t('commandPalette.open')}
        aria-keyshortcuts="Meta+K Control+K"
      >
        <Search aria-hidden className="size-[17px]" strokeWidth={1.7} />
      </Button>

      <div className="hidden items-center gap-2 xl:flex">
        <ApplicationsMenu />
        <ToolsMenu />
      </div>

      {/* Global comms is not a Tool, so the menus can't carry it — it stays at every width. */}
      <ConversationsButton />

      <nav aria-label={t('topbar.generalNavigation')} className="hidden items-center gap-3.5 xl:flex">
        {topbarNav.map((item) => (
          <Tooltip key={item.id}>
            <TooltipTrigger
              render={
                <NavLink
                  to={item.to}
                  aria-label={item.badge != null ? `${item.label} (${item.badge})` : item.label}
                  className={cn(buttonVariants({ variant: 'secondary', size: 'icon' }), 'relative')}
                >
                  <item.icon aria-hidden className="size-[17px]" strokeWidth={1.7} />
                  {item.badge != null && (
                    <span
                      aria-hidden
                      className="absolute -top-1 -right-1 flex h-[18px] min-w-[18px] items-center justify-center rounded-full border border-white/25 bg-[image:var(--nx-accent-grad)] px-1 text-[10.5px] leading-none font-[750] text-[var(--nx-on-accent)] shadow-[var(--nx-accent-glow)]"
                    >
                      {item.badge > 9 ? '9+' : item.badge}
                    </span>
                  )}
                </NavLink>
              }
            />
            <TooltipContent side="bottom" sideOffset={8}>
              {item.label}
              {item.badge != null && <span className="tnum opacity-70">{item.badge}</span>}
            </TooltipContent>
          </Tooltip>
        ))}
      </nav>

      <Button
        variant="secondary"
        size="icon"
        className="hidden xl:inline-flex"
        aria-label={isDark ? t('topbar.switchToLight') : t('topbar.switchToDark')}
        onClick={() => setTheme(isDark ? 'light' : 'dark')}
      >
        {isDark ? (
          <Sun aria-hidden className="size-[17px]" strokeWidth={1.7} />
        ) : (
          <Moon aria-hidden className="size-[17px]" strokeWidth={1.7} />
        )}
      </Button>

      <NotificationCenter />
      <UserMenu collapsed placement="topbar" />
      <CommandPalette open={commandPaletteOpen} onOpenChange={setCommandPaletteOpen} />
    </header>
  )
}
