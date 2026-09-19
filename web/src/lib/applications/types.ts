import type { NavScope } from '@/lib/navigation/types'

export type ApplicationIconKey =
  | 'crm'
  | 'sales'
  | 'marketing'
  | 'service'
  | 'purchasing'
  | 'inventoryWarehouse'
  | 'supplyChainPlanning'
  | 'production'
  | 'quality'
  | 'assetMaintenance'
  | 'productPlm'
  | 'finance'
  | 'projects'
  | 'humanResources'
  | 'b2bCommerce'
  | 'documentManagement'
  | 'inbox'
  | 'calendar'
  | 'tasks'
  | 'conversations'
  | 'notes'
  | 'files'
  | 'knowledgeBase'
  | 'approvals'
  | 'reportsAnalytics'
  | 'automations'
  | 'aiCopilot'

/**
 * `kind` is the application/utility distinction: 'application' is a real
 * business domain with its own routes and rail (see `routes/index.tsx`'s CRM
 * route group); 'utility' is a cross-domain tool or global page that doesn't
 * represent a business boundary of its own. Don't add an 'application' entry
 * for a page that isn't actually backed by a domain route group.
 */
export type ApplicationKind = 'application' | 'utility'

/**
 * Entitlement-shaped, not implemented: every entry today is 'available'.
 * `comingSoon` exists so a future planned application can be represented
 * without inventing a working route — it must render as visibly inert, never
 * behave like an implemented destination.
 */
export type ApplicationStatus = 'available' | 'comingSoon'

/**
 * Available entries route to a real, registered path (`src/routes/paths.ts`).
 * Planned entries intentionally omit it so they cannot look like implemented
 * destinations. `icon` is a lookup key rather than a `LucideIcon` component so
 * this registry stays plain data, the same shape a real backend could serve
 * later. `navScope` is set only for implemented `kind: 'application'` entries —
 * it's the key `lastVisitedRouteByScope` (see `useAppStore`) is read/written
 * under, so the Applications menu can resume the user's last page in that app.
 */
export interface ApplicationItem {
  id: string
  kind: ApplicationKind
  status: ApplicationStatus
  labelKey: string
  descriptionKey: string
  icon: ApplicationIconKey
  route?: string
  navScope?: NavScope
  badge?: number
  order: number
}
