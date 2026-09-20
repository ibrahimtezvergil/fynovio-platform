import { Bell, BellOff, ChevronUp, KeyRound, Moon, Settings, Sun } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { OptionGroup, type Option } from '@/components/common/OptionGroup'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { logout, useSessionStore } from '@/lib/auth'
import { cn } from '@/lib/utils'
import { paths } from '@/routes/paths'
import { useAppStore } from '@/store/useAppStore'
import type { Locale, ThemePreference } from '@/types'

// Each language names itself in its own tongue — not translated by the active UI language.
const LOCALE_OPTIONS: readonly Option<Locale>[] = [
  { value: 'tr', label: 'Türkçe' },
  { value: 'en', label: 'English' },
]

type Permission = NotificationPermission | 'unsupported'

function readPermission(): Permission {
  return typeof Notification === 'undefined' ? 'unsupported' : Notification.permission
}

/** Profile popover: identity, theme, language, notifications, sign out. */
export function UserMenu({
  collapsed = false,
  placement = 'sidebar',
}: {
  collapsed?: boolean
  placement?: 'sidebar' | 'topbar'
}) {
  const { t } = useTranslation('nav')
  const themeOptions: readonly Option<ThemePreference>[] = [
    { value: 'light', label: t('userMenu.themeLight'), icon: Sun },
    { value: 'dark', label: t('userMenu.themeDark'), icon: Moon },
    { value: 'system', label: t('userMenu.themeSystem') },
  ]
  const user = useSessionStore((s) => s.user)
  const theme = useAppStore((s) => s.theme)
  const locale = useAppStore((s) => s.locale)
  const setTheme = useAppStore((s) => s.setTheme)
  const setLocale = useAppStore((s) => s.setLocale)
  const navigate = useNavigate()

  const [open, setOpen] = useState(false)
  const [permission, setPermission] = useState<Permission>(readPermission)

  const close = () => setOpen(false)

  if (!user) return null

  const requestNotifications = async () => {
    if (permission !== 'default') return
    setPermission(await Notification.requestPermission())
  }

  const openSecurity = () => {
    close()
    navigate(paths.accountSecurity)
  }

  const openSettings = () => {
    close()
    navigate(paths.settings)
  }

  const handleLogout = async () => {
    close()
    await logout() // ends the server session, then clears memory + caches even if the request failed
    navigate(paths.login, { replace: true })
  }

  const permissionLabel: Record<Permission, string> = {
    granted: t('userMenu.permissionGranted'),
    denied: t('userMenu.permissionDenied'),
    default: t('userMenu.permissionDefault'),
    unsupported: t('userMenu.permissionUnsupported'),
  }
  const NotificationIcon = permission === 'granted' ? Bell : BellOff

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger
        aria-label={collapsed ? user.name : undefined}
        title={collapsed ? user.name : undefined}
        className={cn(
          'nx-material flex cursor-pointer items-center gap-2.5 rounded-[var(--nx-r-ctl-lg)] text-left transition-[background,border-color] duration-[250ms] ease-fluid hover:bg-[var(--nx-fill-hover)] hover:border-[var(--nx-hairline-strong)]',
          collapsed ? 'size-11 justify-center px-0' : 'h-[52px] w-full px-[11px]',
        )}
      >
        <span
          aria-hidden
          className="nx-avatar size-[30px] shrink-0 rounded-[var(--nx-r-ctl-sm)]"
        >
          {user.initials}
        </span>
        {!collapsed && (
          <>
            <span className="min-w-0 flex-1">
              <span className="block truncate text-[13px] font-[590] tracking-[-0.012em]">{user.name}</span>
              <span className="text-muted-foreground block truncate text-[11px]">{user.email}</span>
            </span>
            <ChevronUp
              aria-hidden
              className={cn(
                'text-muted-foreground size-3.5 shrink-0 transition-transform duration-[120ms]',
                open && 'rotate-180',
              )}
            />
          </>
        )}
      </PopoverTrigger>

      <PopoverContent
        aria-label={t('userMenu.accountSettings')}
        side={placement === 'topbar' ? 'bottom' : 'top'}
        align={placement === 'topbar' ? 'end' : 'start'}
        sideOffset={8}
        className="w-64 gap-0 overflow-hidden rounded-[var(--nx-r-card)] p-0"
      >
        <div className="px-4 py-3">
          <p className="font-heading truncate text-[15px] leading-5 font-[620] tracking-[-0.022em]">{user.name}</p>
          <p className="text-muted-foreground truncate text-[12.5px] leading-5">{user.email}</p>
        </div>

        <Divider />
        <OptionGroup label={t('userMenu.theme')} options={themeOptions} value={theme} onChange={setTheme} />

        <Divider />
        <OptionGroup label={t('userMenu.language')} options={LOCALE_OPTIONS} value={locale} onChange={setLocale} />

        <Divider />
        <button
          type="button"
          onClick={requestNotifications}
          disabled={permission !== 'default'}
          className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-[13px] transition-colors duration-[250ms] ease-fluid hover:bg-[var(--nx-fill-hover)] enabled:cursor-pointer disabled:cursor-default"
        >
          <NotificationIcon aria-hidden className="size-4 shrink-0" strokeWidth={1.5} />
          <span className="flex-1">{t('userMenu.notifications')}</span>
          <span className="text-muted-foreground">{permissionLabel[permission]}</span>
        </button>

        <Divider />
        <button
          type="button"
          onClick={openSecurity}
          className="flex w-full cursor-pointer items-center gap-2.5 px-4 py-2.5 text-left text-[13px] transition-colors duration-[250ms] ease-fluid hover:bg-[var(--nx-fill-hover)]"
        >
          <KeyRound aria-hidden className="size-4 shrink-0" strokeWidth={1.5} />
          <span className="flex-1">{t('userMenu.security')}</span>
        </button>

        <Divider />
        <button
          type="button"
          onClick={openSettings}
          className="flex w-full cursor-pointer items-center gap-2.5 px-4 py-2.5 text-left text-[13px] transition-colors duration-[250ms] ease-fluid hover:bg-[var(--nx-fill-hover)]"
        >
          <Settings aria-hidden className="size-4 shrink-0" strokeWidth={1.5} />
          <span className="flex-1">{t('items.settings')}</span>
        </button>

        <Divider />
        <button
          type="button"
          onClick={() => void handleLogout()}
          className="text-destructive w-full cursor-pointer px-4 py-2.5 text-left text-[13px] font-[590] transition-colors duration-[250ms] ease-fluid hover:bg-[var(--nx-st-red-bg)]"
        >
          {t('userMenu.logout')}
        </button>
      </PopoverContent>
    </Popover>
  )
}

function Divider() {
  return <div role="presentation" className="mx-4 h-px bg-[var(--nx-hairline)]" />
}
