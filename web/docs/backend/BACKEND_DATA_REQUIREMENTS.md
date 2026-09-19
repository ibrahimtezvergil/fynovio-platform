# Backend Data Requirements (from the Frontend)

What the frontend currently expects from a real backend, beyond the obvious business/domain
tables (deals/pipeline, quotes, orders, customers, products). Built by reading every mock handler
under `src/mocks/handlers/`, `src/api/endpoints.ts`, `src/types/schemas.ts`, and each feature's own
`schema.ts` on `ios-27-applications-tools`. Re-verify against current code before treating this as
exhaustive — features keep landing.

Out of scope on purpose: `deals`, `quotes`, `orders`, `customers`/`accounts` — the CRM pipeline
entities. Quotes/orders aren't separately modeled yet beyond string refs inside other schemas
(`entityType: 'quote' | 'order' | 'customer'`, `documentNumber`) — no dedicated Quote/Order/Product
schema exists anywhere in the codebase today; that's a business-table design question, not covered
here.

## Already required — schema + mock + UI exist, only the endpoint is missing

| Table | Fields | Endpoint | Consumer |
| --- | --- | --- | --- |
| `users` | id, name, email, role (`admin｜manager｜viewer`), initials | `/auth/login`, `/auth/me` (`src/types/schemas.ts` `userSchema`) | Auth, topbar user menu |
| `calendar_events` | id, title, start, end, allDay, kind (`meeting｜call｜task｜deadline｜away`), owner, location?, account?, notes? | `/calendar/events` (`src/features/calendar/schema.ts`) | Calendar page |
| `home_attention_items` | id, type, severity, title, description?, entityType?, entityId?, entityLabel?, documentNumber?, amount?, priority, timestamp, owner?, action `{ label, url }` | `/home/attention` (`src/features/home/schema.ts`) | Home "Your Focus" |
| `home_recent_work` | id, entityType (`quote｜order｜customer`), entityId, title, subtitle?, reference?, route, **lastAccessedAt** | `/home/recent-work` | Home "Continue Working" |
| `home_team_activity` | id, user, avatarInitials, status?, action, entityType?, entityId?, entityLabel?, timestamp, route? | `/home/team-activity` | Home "What's Happening" |
| `dashboard_activities` | id, kind (`task｜message｜email｜review`), title, when | `/dashboard/activities` | CRM dashboard activity list |
| `stage_buckets` | stage, count, value | `/dashboard/stages` | CRM dashboard KPIs — likely a query/view over `deals`, not its own table |

`home_recent_work` and `home_team_activity` are the two to design deliberately: a per-user
"recently viewed entity" log and a cross-user activity/audit feed, not read-models derivable
from the business tables alone.

## Ready but not wired — real schema + UI, currently a client-only Zustand store

Both stores carry a comment saying they're a placeholder for a real query: *"Once a backend
exists this store is replaced outright by a query plus a mutation."*

- **`settings`** (`src/features/settings/store/useSettingsStore.ts`) — profile (fullName,
  jobTitle, email, phone, timezone, currency, signature note), notification toggles
  (stageChange, quoteViewed, weeklyDigest, closingSoon), password change, danger zone.
- **`notifications`** (`src/features/notifications/store/useNotificationStore.ts`) — id, kind,
  title, body, at, read, actor?, decision?. Backs the topbar `NotificationCenter`.

## Implied, not yet modeled — placeholder routes with zero schema

`/conversations`, `/reports`, `/files`, `/feedback`, `/members` all render the generic
`PlaceholderPage` ("this module isn't connected yet") — `src/features/placeholder/`. No mock
handler, no Zod schema, no field list exists for any of these yet. Not required to unblock other
backend work; design their tables when each feature actually gets built.

## Client-side only — never a backend table

- `useAppStore` (`src/store/useAppStore.ts`): theme, locale, `sidebarCollapsed`, `isCompact`,
  `lastVisitedRouteByScope` — persisted to `localStorage` via Zustand `persist`, pure per-device UI
  preference.
- `useCommandPaletteStore` (`src/lib/commands/paletteStore.ts`): ephemeral dialog open/close state.
- Telemetry (`src/lib/telemetry.ts`): `track()` currently calls `console.debug` only — no network
  call, no event table exists or is implied yet.

## Worth flagging for backend design, not needed to start

- **Permissions/capabilities** (`src/lib/permissions/`, `src/lib/capabilities/`) are explicitly
  commented UX-only: *"a backend must enforce every permission/entitlement independently."* Today
  they're a hardcoded `Record<UserRole, PermissionId[]>` and a hardcoded `CapabilityId[]` array —
  no real data model. Implies a future roles/permissions table and a tenant/entitlements table.
- **`applicationRegistry`** (`src/lib/applications/registry.ts`) is 100% static frontend data
  today (`status: 'available'` hardcoded on every entry), but its own type comment anticipates
  serving it from a backend later (`status: 'available' | 'comingSoon'`, tenant-scoped visibility).
  Not needed now — the shape already doesn't block it.
- **Entity search** (`src/lib/search/entitySearch.ts`) is a static in-memory array indexing the
  same customers/quotes/orders the mocks above use. It implies a search endpoint *over* the
  existing business tables, not a separate search table.
