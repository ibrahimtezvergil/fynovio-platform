import type { LucideIcon } from 'lucide-react'

export type NavGroupId = 'workspace' | 'operations' | 'system' | 'developer'
export type NavParentId = 'demo-data' | 'demo-components'
export type NavigationSurface = 'sidebar' | 'topbar'

/**
 * Which sidebar an item belongs to. Only surfaces that own a sidebar
 * (`RouteSurface`'s 'application' | 'system' | 'developer') ever get matched
 * against this — Home and Utility routes never mount a `Sidebar`, so nothing
 * needs a scope for them. A future domain app (e.g. an eventual Inventory)
 * adds its own id here, not a conditional.
 */
export type NavScope = 'crm' | 'system' | 'developer'

/**
 * What kind of shell surface a route (or route group) is — drives whether
 * `DashboardLayout` mounts a sidebar at all, and which rail (`NavScope`) if
 * so. Set on a route's `handle` in `routes/index.tsx`, read by
 * `DashboardLayout` via `useMatches()`.
 *
 * - home        Global Home. No sidebar — the app-distribution surface.
 * - application A domain application (CRM, and future ones). Owns a sidebar.
 * - utility     A cross-domain tool (Calendar, Reports, Files, ...). No
 *               sidebar — opening it should feel like opening a tool, not
 *               entering an application.
 * - system      Platform administration (Settings, Members). Owns a sidebar.
 * - developer   Cross-cutting dev tooling (demo/playground pages). Owns a
 *               sidebar, and is excluded from the route tree entirely in
 *               production (see `routes/index.tsx`).
 */
export type RouteSurface = 'home' | 'application' | 'utility' | 'system' | 'developer'

export interface NavParentDefinition {
  id: string
  labelKey: string
  icon: LucideIcon
}

export interface NavItemDefinition {
  id: string
  labelKey: string
  to: string
  icon: LucideIcon
  badge?: number
}

/**
 * A feature-owned navigation entry. A parent id makes this entry a nested leaf.
 * General-purpose destinations can live in the always-visible topbar instead
 * of the collapsible sidebar.
 */
export interface NavContribution {
  group: NavGroupId
  item: NavItemDefinition
  parentId?: NavParentId
  surface?: NavigationSurface
  scope?: NavScope
}
