import type { TFunction } from 'i18next'
import { calendarNav } from '@/features/calendar/nav'
import { crmSettingsNav } from '@/features/crm-settings/nav'
import { dashboardNav } from '@/features/dashboard/nav'
import { demoBadgesNav } from '@/features/demo-badges/nav'
import { demoChartsNav } from '@/features/demo-charts/nav'
import { demoDrawersNav } from '@/features/demo-drawers/nav'
import { demoFiltersNav } from '@/features/demo-filters/nav'
import { demoFormsNav } from '@/features/demo-forms/nav'
import { demoKanbanNav } from '@/features/demo-kanban/nav'
import { demoNotificationsNav } from '@/features/demo-notifications/nav'
import { demoOverlaysNav } from '@/features/demo-overlays/nav'
import { demoStatesNav } from '@/features/demo-states/nav'
import { demoTablesNav } from '@/features/demo-tables/nav'
import { demoTimelineNav } from '@/features/demo-timeline/nav'
import { homeNav } from '@/features/home/nav'
import { opportunitiesNav } from '@/features/opportunities/nav'
import { membersNav, placeholderNav } from '@/features/placeholder/nav'
import { settingsNav } from '@/features/settings/nav'
import { navParents } from '@/lib/navigation/parents'
import type { NavigationSurface, NavContribution, NavGroupId, NavScope } from '@/lib/navigation/types'
import { isNavParent, type NavGroup, type NavItem, type NavLeaf, type NavLink } from '@/types'

const navContributions: NavContribution[] = [
  homeNav, dashboardNav, opportunitiesNav, crmSettingsNav, calendarNav, ...placeholderNav, settingsNav, membersNav, demoTablesNav,
  demoChartsNav, demoFiltersNav, demoFormsNav, demoOverlaysNav, demoNotificationsNav, demoStatesNav,
  demoDrawersNav, demoBadgesNav, demoKanbanNav, demoTimelineNav,
]
const groupKeys: { id: NavGroupId; labelKey: string }[] = [
  { id: 'workspace', labelKey: 'groups.workspace' }, { id: 'operations', labelKey: 'groups.operations' },
  { id: 'system', labelKey: 'groups.system' }, { id: 'developer', labelKey: 'groups.developer' },
]

/**
 * Assemble the localized nav from feature-owned contributions. `surface`
 * narrows sidebar vs. topbar; `scope` narrows one domain rail from
 * another (see `NavScope` — Home and Utility routes have no rail at all, so
 * they never pass one). Called with neither for the breadcrumb resolver and
 * the command palette, which need every contribution regardless of which
 * rail (if any) renders it.
 *
 * Developer-scoped entries are dropped outside `import.meta.env.DEV`
 * unconditionally, even when `scope: 'developer'` is asked for explicitly —
 * the one place that rule lives, so nothing downstream (a sidebar, the
 * palette, the breadcrumb resolver) has to remember it independently.
 */
export function buildNavGroups(t: TFunction<'nav'>, surface?: NavigationSurface, scope?: NavScope): NavGroup[] {
  return groupKeys.map(({ id, labelKey }) => {
    const items: NavItem[] = []
    for (const contribution of navContributions.filter(
      (entry) =>
        entry.group === id &&
        (surface === undefined || (entry.surface ?? 'sidebar') === surface) &&
        (scope === undefined || entry.scope === scope) &&
        (import.meta.env.DEV || entry.scope !== 'developer'),
    )) {
      const item = { ...contribution.item, label: t(contribution.item.labelKey) }
      if (!contribution.parentId) { items.push(item); continue }
      const parent = navParents[contribution.parentId]
      if (!parent) continue
      const existing = items.find((candidate) => candidate.id === parent.id)
      if (existing && isNavParent(existing)) existing.children.push(item)
      else items.push({ ...parent, label: t(parent.labelKey), children: [item] })
    }
    return { label: t(labelKey), items }
  }).filter((group) => group.items.length > 0)
}

/**
 * A sidebar's full nav: just its own scope. Developer is no longer appended
 * here — it's a global Tool reached from the Topbar's Tools menu (see
 * `ToolsMenu`), not sidebar content grafted onto every domain rail.
 */
export function buildSidebarNav(t: TFunction<'nav'>, scope: NavScope): NavGroup[] {
  return buildNavGroups(t, 'sidebar', scope)
}

/** Flat, localized destinations reserved for the always-visible topbar. */
export function buildTopbarNav(t: TFunction<'nav'>): NavLink[] {
  return buildNavGroups(t, 'topbar').flatMap((group) => group.items).filter(
    (item): item is NavLink => !isNavParent(item),
  )
}

/** Where a pathname sits in the tree: the top-level entry, and the leaf under it if any. */
export interface NavTrail { item: NavItem; leaf?: NavLeaf }

/** Resolve a pathname to its nav entry. Longest match wins. */
export function findNavTrail(groups: readonly NavGroup[], pathname: string): NavTrail | undefined {
  let best: NavTrail | undefined
  let bestLength = -1
  const consider = (to: string, trail: NavTrail) => {
    if (!pathname.startsWith(to) || to.length <= bestLength) return
    best = trail
    bestLength = to.length
  }
  for (const item of groups.flatMap((group) => group.items)) {
    if (isNavParent(item)) for (const leaf of item.children) consider(leaf.to, { item, leaf })
    else consider(item.to, { item })
  }
  return best
}

/** Whether a parent owns the current route — the collapsed rail marks it active too. */
export function isParentActive(item: NavItem, pathname: string): boolean {
  return isNavParent(item) && item.children.some((leaf) => pathname.startsWith(leaf.to))
}
