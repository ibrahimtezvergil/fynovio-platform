import { useSyncExternalStore } from 'react'
import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import { i18n } from '@/lib/i18n'
import { densityOf, type Density, type Locale, type ThemePreference } from '@/types'

interface AppState {
  theme: ThemePreference
  locale: Locale
  /** Rail mode, as one bit. `false` is the full 266px sidebar — the default. */
  sidebarCollapsed: boolean
  /** Row density, as one bit. `false` is comfortable — the default. */
  isCompact: boolean
  /**
   * The last route visited inside each application scope (e.g. `{ crm:
   * '/crm/pipeline' }`), so the Applications menu can resume where the user
   * left off instead of always landing on the app's default page. Keyed by
   * `NavScope`, not hardcoded to one application.
   */
  lastVisitedRouteByScope: Record<string, string>
  setTheme: (theme: ThemePreference) => void
  setLocale: (locale: Locale) => void
  setSidebarCollapsed: (collapsed: boolean) => void
  toggleSidebar: () => void
  setCompact: (isCompact: boolean) => void
  toggleDensity: () => void
  setLastVisitedRoute: (scope: string, pathname: string) => void
}

const DARK_QUERY = '(prefers-color-scheme: dark)'

function subscribeToSystemTheme(onStoreChange: () => void) {
  const media = window.matchMedia(DARK_QUERY)
  media.addEventListener('change', onStoreChange)
  return () => media.removeEventListener('change', onStoreChange)
}

const getSystemDark = () => window.matchMedia(DARK_QUERY).matches

/** Resolve "system" against the OS and write the class Tailwind's dark variant reads. */
export function applyTheme(theme: ThemePreference) {
  const isDark = theme === 'dark' || (theme === 'system' && getSystemDark())
  document.documentElement.classList.toggle('dark', isDark)
}

/**
 * Announce the density on the root element.
 *
 * The attribute is what `src/styles/tokens.css` keys the `--nx-d-*` block off,
 * so writing it here is the whole propagation mechanism — no context, no prop
 * drilling. It is only half the switch though: a subtree resizes when it also
 * carries `.nx-dense` (see `DensityScope`), which is how the topbar, sidebar
 * and KPI cards stay comfortable in both modes.
 */
export function applyDensity(isCompact: boolean) {
  document.documentElement.dataset.density = densityOf(isCompact)
}

export const useAppStore = create<AppState>()(
  persist(
    (set) => ({
      theme: 'system',
      locale: 'tr',
      sidebarCollapsed: false,
      isCompact: false,
      lastVisitedRouteByScope: {},
      setTheme: (theme) => {
        applyTheme(theme)
        set({ theme })
      },
      setLocale: (locale) => {
        document.documentElement.lang = locale
        void i18n.changeLanguage(locale)
        set({ locale })
      },
      setSidebarCollapsed: (sidebarCollapsed) => set({ sidebarCollapsed }),
      toggleSidebar: () => set((s) => ({ sidebarCollapsed: !s.sidebarCollapsed })),
      setCompact: (isCompact) => {
        applyDensity(isCompact)
        set({ isCompact })
      },
      toggleDensity: () =>
        set((s) => {
          applyDensity(!s.isCompact)
          return { isCompact: !s.isCompact }
        }),
      setLastVisitedRoute: (scope, pathname) =>
        set((s) => ({ lastVisitedRouteByScope: { ...s.lastVisitedRouteByScope, [scope]: pathname } })),
    }),
    {
      name: 'fynovio-app',
      onRehydrateStorage: () => (state) => {
        applyTheme(state?.theme ?? 'system')
        applyDensity(state?.isCompact ?? false)
        document.documentElement.lang = state?.locale ?? 'tr'
        void i18n.changeLanguage(state?.locale ?? 'tr')
      },
    },
  ),
)

/**
 * The density preference, plus the string form components render.
 *
 * Two selectors rather than one object: `isCompact` is the only slice that
 * changes, and the two setters are stable identities, so this costs one
 * subscription and re-renders exactly on a density flip.
 */
export function useDensity(): {
  isCompact: boolean
  density: Density
  setCompact: (isCompact: boolean) => void
  toggleDensity: () => void
} {
  const isCompact = useAppStore((s) => s.isCompact)
  const setCompact = useAppStore((s) => s.setCompact)
  const toggleDensity = useAppStore((s) => s.toggleDensity)
  return { isCompact, density: densityOf(isCompact), setCompact, toggleDensity }
}

/**
 * The preference collapsed to what is actually on screen.
 *
 * The OS media query is an external store, so it is read through
 * `useSyncExternalStore` rather than mirrored into `useState` from an effect —
 * one subscription per consumer, no render with a stale first value.
 */
export function useResolvedTheme(): 'light' | 'dark' {
  const theme = useAppStore((s) => s.theme)
  const systemDark = useSyncExternalStore(subscribeToSystemTheme, getSystemDark, () => false)
  if (theme !== 'system') return theme
  return systemDark ? 'dark' : 'light'
}

/**
 * The rail preference and its toggle.
 *
 * Both slices in one hook, but still two selectors: `toggleSidebar` is a stable
 * identity, so a component that only flips the rail never re-renders on the flip
 * it caused — only the ones reading `collapsed` do.
 */
export function useSidebar(): { collapsed: boolean; toggle: () => void } {
  const collapsed = useAppStore((s) => s.sidebarCollapsed)
  const toggle = useAppStore((s) => s.toggleSidebar)
  return { collapsed, toggle }
}
