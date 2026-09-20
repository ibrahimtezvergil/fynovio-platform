import type { LucideIcon } from 'lucide-react'

export type { EntityRef } from './entity'

export type UserRole = 'admin' | 'manager' | 'viewer'

export interface User {
  id: string
  name: string
  email: string
  role: UserRole
  initials: string
}

export interface Tenant {
  id: string
  name: string
  role: string
  initial: string
}

/**
 * A sub-page inside a nav parent. The icon is optional because only the flyout
 * shows it — the expanded disclosure leans on indentation instead.
 */
export interface NavLeaf {
  id: string
  label: string
  to: string
  icon?: LucideIcon
  badge?: number
}

interface NavItemBase {
  id: string
  label: string
  icon: LucideIcon
  badge?: number
}

/** A nav entry that is itself a route. */
export interface NavLink extends NavItemBase {
  to: string
  children?: never
}

/**
 * A nav entry that only holds sub-pages. It is never a route, which is what
 * lets the collapsed rail treat it as a flyout trigger rather than a link.
 */
export interface NavParent extends NavItemBase {
  to?: never
  children: NavLeaf[]
}

export type NavItem = NavLink | NavParent

/** The one narrowing both the sidebar and the breadcrumb branch on. */
export function isNavParent(item: NavItem): item is NavParent {
  return item.children !== undefined
}

export interface NavGroup {
  label: string
  items: NavItem[]
}

/**
 * The pipeline stages, in display order everywhere. `onhold` is terminal-ish
 * rather than a step forward — it sits last and carries no forecast date.
 */
export const STAGES = ['new', 'contacted', 'quoted', 'meeting', 'ready', 'onhold'] as const
export type Stage = (typeof STAGES)[number]

/** The stages a deal moves through, excluding the parked one. */
export const ACTIVE_STAGES = STAGES.filter((stage) => stage !== 'onhold')

export interface Deal {
  id: string
  title: string
  account: string
  stage: Stage
  owner: string
  value: number
  /** null once a deal is parked and has no forecast date. */
  closeDate: string | null
  /** Close probability, 0–100. Weighted value is `value * probability / 100`. */
  probability: number
}

/** One pipeline stage rolled up: how many deals sit there and what they are worth. */
export interface StageBucket {
  stage: Stage
  count: number
  value: number
}

/** What kind of touchpoint an activity is — decides its icon and tone. */
export type ActivityKind = 'task' | 'message' | 'email' | 'review'

export interface Activity {
  id: string
  kind: ActivityKind
  title: string
  /** Pre-formatted for now; becomes an ISO timestamp once a backend exists. */
  when: string
}

export type ThemePreference = 'light' | 'dark' | 'system'

export type Locale = 'tr' | 'en'

/**
 * The two row densities. State is the boolean `isCompact` in `useAppStore`;
 * this is its rendered form — the `data-density` attribute value and the prop
 * a grid takes. Two names for one bit, deliberately: booleans toggle cleanly,
 * attributes read better as words.
 */
export type Density = 'comfortable' | 'compact'

export const densityOf = (isCompact: boolean): Density =>
  isCompact ? 'compact' : 'comfortable'

/** Shape the backend will return once it exists. */
export interface ApiError {
  message: string
  status: number
  code?: string
  /**
   * A 422's per-field validation errors — field path -> messages, matching
   * Laravel's `ValidationException` response shape (`{ errors: { field:
   * string[] } }`) and what zod's `.flatten().fieldErrors` already produces
   * client-side, so a client-side pre-check and a real 422 land in the same
   * shape. See `docs/design-system/07-forms.md`.
   */
  fields?: Record<string, string[]>
  /** A `password_policy_violation`'s machine codes (`too_short`, `too_long`, `equals_email`, …) — the UI words them. */
  violations?: string[]
  /** A 429's `Retry-After`, in seconds. */
  retryAfterSeconds?: number
}
