# Fynovio Dashboard — Codex instructions

## Scope and repository map

This repository is a single-package React 19 + TypeScript SPA built with Vite,
Tailwind CSS v4, and shadcn/ui. Use npm and the existing package-lock.json.
Ancestor Laravel/PHP commands do not apply here: use the frontend checks below.
This frontend lives in the `fynovio-platform` monorepo (`web/`) next to the .NET API. Authentication is
real (the API's `/auth/*`); the feature data that has no backend yet (deals, calendar, demo pages) is still
mocked with MSW.

- `src/features/<name>/` owns feature pages, components, API hooks, schemas, and stores.
- `src/components/ui/` contains shadcn primitives; reuse before customizing.
- `src/components/common/inputs/` is the shared form-control kit; `common/` holds other shared UI.
- `src/components/data-table/` wraps TanStack Table; reuse it for data grids.
- `src/api/` owns the axios client, endpoint map, and QueryClient.
- Each feature owns a `routes.ts` (`FeatureRoute[]` — path, guard, lazy `load()`) and a `nav.ts`
  (`NavContribution`, see `src/lib/routes/types.ts` / `src/lib/navigation/types.ts`);
  `src/routes/index.tsx` and `src/layouts/navigation.ts` only import and assemble every feature's
  file — add a page by adding to the feature's own `routes.ts`/`nav.ts`, not by hand-editing the
  assembler. `paths.ts` holds route strings and guards live alongside it.
- `src/layouts/navigation.ts` assembles feature-owned `NavContribution`s into `NavGroup`s via
  `buildNavGroups`/`buildSidebarNav`/`buildTopbarNav`/`findNavTrail`; layouts compose feature pages.
  Route ownership follows a five-surface model (`RouteSurface`, `src/lib/navigation/types.ts`):
  `home` (`/dashboard`, no sidebar — the app-launcher surface), `application` (a domain app — CRM
  today — owns a `NavScope`-scoped sidebar), `utility` (cross-domain tools: Calendar, Reports,
  Files, Feedback — no sidebar), `system` (Settings, Members — its own sidebar), `developer`
  (demo/playground pages, `DEV`-only, excluded from the route tree in production, **no sidebar of
  its own** — reached only through the Topbar's Tools menu and Cmd+K, never appended to another
  surface's sidebar). A route group declares its surface via `handle: { surface, navScope }` in
  `routes/index.tsx`; `DashboardLayout` reads it to decide whether/which `Sidebar` to mount. Don't
  reintroduce a Developer sidebar or append Developer entries to another sidebar — that composition
  step was deliberately removed from `buildSidebarNav`.
- The Topbar is the permanent app-switcher, not just chrome: `ApplicationsMenu` (Uygulamalar) and
  `ToolsMenu` (Araçlar), both in `src/layouts/components/`, read the single canonical
  `applicationRegistry` (`src/lib/applications/registry.ts` — `kind: 'application' | 'utility'`,
  `status: 'available' | 'comingSoon'`, plain data with i18n `labelKey`/`descriptionKey`). Home's
  own launcher (`src/features/home/components/ApplicationsSection.tsx`) reads the same registry —
  add a new tool/application as one registry entry, never a second hardcoded list. Conversations
  keeps its own dedicated Topbar icon (global comms, not a Tool). `useAppStore.lastVisitedRouteByScope`
  (keyed by `NavScope`) lets the Applications menu resume an application's last page; it's written
  generically by `DashboardLayout` off `handle.surface === 'application'`, so a second domain
  application gets resume-on-switch for free, no per-app wiring. The Topbar breadcrumb also prepends
  the owning application's label for `application`-surface routes (`Fynovio > CRM > Pipeline`,
  resolved from the same registry) — utility/system routes stay flat (`Fynovio > Takvim`).
- `src/store/` owns global UI state; `src/types/` holds shared types.
- `src/styles/tokens.css` defines design tokens; `src/index.css` maps semantic variables and material classes.
- The `@/` alias resolves to `src/` in both TypeScript and Vite.
- `docs/frontend-platform/` scores 60 enterprise frontend capabilities against this
  repo (buildable now / needs a backend / needs CI) with a committed build order.
  Read it before starting a cross-cutting layer (registries, mutation infrastructure,
  permissions, error architecture).
- `graphify-out/` (gitignored, machine-local) holds a generated code-knowledge graph —
  tree-sitter extraction, no LLM, no API cost. It's a navigation/dependency/impact-
  analysis aid, not a source of truth. Where the `graphify` CLI is available, prefer
  `graphify query "<question>"` / `graphify path "<A>" "<B>"` / `graphify explain
  "<concept>"` to scope down before reading source for a broad "how does X work" or
  cross-file-impact question — skip it for a change already scoped to specific files.
  Verify anything it surfaces against the actual source, tests, and runtime contracts;
  never cite the graph alone as the answer. On a machine with `graphify hook install`
  run, the graph rebuilds itself after commits and branch switches — a fresh worktree
  still needs one `graphify update .` before that takes over, the same one-time step
  as `npm run setup`.

## Commands and verification

Run commands from the repository root. Canonical scripts are in `package.json`.

| Operation | Command |
| --- | --- |
| Fresh clone/worktree bootstrap | `npm run setup` (`npm ci` — exact, lockfile-driven, matches CI) |
| Add/upgrade a dependency | `npm install <pkg>` directly — updates the lockfile; not `npm run setup` |
| Development server | `npm run dev` (Vite prints the URL it actually bound; with `strictPort` unset it auto-increments past 5173 if that port is taken — expected when a second worktree's dev server is already running) |
| Focused lint | `npm run lint -- src/path/to/file.tsx` (accepts multiple paths) |
| Full lint | `npm run lint` |
| Type-check only (fast) | `npm run typecheck` (`tsc -b`, no bundle) |
| Type-check and production bundle | `npm run build` (`tsc -b && vite build`) |
| Unit tests | `npm test` (`vitest run`) |
| Preview existing build | `npm run preview` |
| Fast check (lint + typecheck + test) | `npm run check` |
| End-to-end (real API + throwaway PostgreSQL + Chrome) | `npm run e2e` (runs `../scripts/e2e.sh`; needs Docker and the .NET SDK — see the root README) |

- Small source edits: start with focused lint. Behavior/type changes: run
  `npm run check` and verify the affected flow. Cross-cutting edits also warrant
  `npm run build` before a PR, since it is the closest local equivalent to CI.
- UI changes: use available browser tooling to inspect the affected page, relevant
  viewport, and both themes when styling changed. Demo routes under `/demo/` expose
  shared forms, tables, and charts. Report when browser verification was unavailable.
- `npm test` runs the existing vitest suite — currently narrow coverage (data-table
  URL state, filter query logic). Run it for any change to code it covers or new
  test files; a passing suite is not full behavioral coverage of the app. GitHub
  Actions CI (`.github/workflows/ci.yml`) runs lint, typecheck, test, and build on
  every push to `main` and every pull request — treat a local `npm run check` failure
  as a CI failure in waiting.
- Instruction-only changes: validate paths, commands, instruction loading, and diff;
  an application build is unnecessary. Distinguish existing failures from new ones.
- Oxlint architecture restrictions include warnings; a zero exit code can still
  contain findings. Read relevant diagnostics instead of suppressing them. `npm run
  check` chains lint in, so it inherits this: a green `check`/CI run can still carry
  lint warnings worth reading, not just errors.

## Architecture and editing rules

- Features do not import other features. Move reusable code into shared components,
  global stores, or shared types. Routes/layouts compose features; features must not
  import the router, route guards, or layouts. Shared `@/routes/paths` is allowed.
- Keep route strings in `src/routes/paths.ts`. When adding a navigable page, add the entry to
  the feature's own `routes.ts` and `nav.ts` (see the repo-map entry above), not to the central
  assemblers; preserve lazy-loaded page boundaries.
- Keep server/request state in React Query and feature API hooks. Reuse `apiClient`
  and the endpoint map for real API integration, preserving normalized `ApiError` behavior.
  New mutations use `useAppMutation` (`src/lib/mutations/useAppMutation.ts` — validate → execute
  → invalidate → toast → telemetry), not a raw `useMutation`. New query responses validate at the
  boundary with `parseApiResponse` (`src/api/response.ts`) and a Zod schema — shared domain types
  in `src/types/schemas.ts`, feature-owned types in the feature's own `schema.ts` (see
  `src/features/calendar/schema.ts`); the older `unwrapApiResponse`/`isArrayOf` guards are shallow
  (they don't validate element shape) and remain only where a feature hasn't migrated yet. Key
  factory shape and the invalidation convention: `docs/frontend-platform/QUERY_ARCHITECTURE.md`.
- Reusable cross-cutting behavior lives in `src/lib/`, one registry per concern rather than each
  feature reinventing it: `src/lib/actions/` (`Action` contract — row menus, bulk bars, toolbars
  and the command palette all read from `getActions(ctx)`), `src/lib/commands/` (⌘K palette,
  auto-fed by the route/nav registries; `useCommandPaletteStore` is the one shared open/close
  state for the palette dialog — every entry point, e.g. the Topbar's search button and Home's
  `CommandBar`, must read/write it rather than owning local `open` state, or a second
  `CommandPalette` ends up mounted), `src/lib/applications/` (canonical `ApplicationItem`
  registry backing the Topbar's Applications/Tools menus and Home's launcher — see the repo-map
  entry above), `src/lib/overlay/` (`openDialog()`/`openDrawer()` — one imperative stack, one
  escape/focus-restore path; a new overlay call site should use it instead of a local boolean),
  `src/lib/permissions/` (`usePermission()`, `<Can action="...">`) and `src/lib/capabilities/`
  (`useCapability()`) — both explicitly UX-only, see Risk boundaries below.
  Each was proven on one real call site, not force-migrated everywhere — extend the existing
  registry for new work rather than writing a parallel mechanism.
- Cross-feature references (breadcrumbs, action/command context, domain pickers) use `EntityRef`
  (`src/types/entity.ts` — `{ type, id, display, subtitle?, url }`), not an ad-hoc shape.
- Forms use react-hook-form with colocated Zod schemas. Reuse shared controlled
  inputs (`value` / `onValueChange`) and existing Field/Controller patterns.
  Zustand stores hold state that outlives the form, not individual form fields.
- Read Zustand through selectors; use the existing `useShallow` pattern when selecting
  multiple values. Theme-aware components use `useResolvedTheme()` from the app store —
  it folds "system" against the OS via `useSyncExternalStore`; don't mirror the media
  query in a component. Dark mode toggles the `.dark` class on `<html>` via
  `applyTheme()` in `src/store/useAppStore.ts`, persisted to localStorage.
- Preserve shadcn primitive behavior; edit generated UI only for intentional customization.
- Density is one boolean (`isCompact` in `useAppStore`, persisted), written to
  `<html data-density>` by `applyDensity()`, which selects the `--nx-d-*` block in
  `tokens.css`. `.nx-dense` (`index.css`) is the opt-in half, rebinding
  `--nx-control-height`/`--nx-row-height` so `h-control`, `.nx-row`, `.nx-grid` follow
  with no prop of their own — wrap a region with `DensityScope` or `Toolbar` (`DataTable`
  already is one). Header, sidebar, nav, and KPI/summary cards never carry the class and
  stay comfortable in both modes. Compact rows sit below the `--nx-hit-min` accessible
  pointer-target minimum on purpose (see `tokens.css` for the current row-height and
  hit-min values) — that's why compact stays opt-in and reversible rather than the
  default.
- The sidebar chord (⌘/Ctrl + the key right of P) is matched on
  `event.code === 'BracketLeft'`, not the character — `[` is AltGr+8 on a Turkish
  layout, and a character match would misfire. The displayed key cap comes from
  `navigator.keyboard.getLayoutMap()`. Collapsed state is a narrow icon-only rail
  (`--nx-sidebar-width-collapsed` in tokens.css); only the `width` transition animates, labels are
  dropped not faded. Nav nesting is one level deep (`NavParent` holds `NavLeaf[]`,
  never a route itself); `findNavTrail()` in `src/layouts/navigation.ts` is the one
  path→nav resolver for both the breadcrumb and active states.
- Pipeline stage is never encoded by colour alone — pair it with `StageBadge`'s label.
  `STAGES` in `src/types` is the display order everywhere; `onhold` is last and the
  only stage with no forecast date, so `Deal.closeDate` is nullable.

## Design system

- `docs/design-system/` is the written UI/UX standard (principles, foundations,
  theming/density, component standards, patterns, accessibility, content, definition of
  done). Consult the relevant document before UI work; keep it in sync when a rule changes.
- Treat `src/styles/tokens.css` as canonical, including its derived values. Do not
  reconstruct tokens from an external artboard or historical design annotations.
- Components use semantic CSS variables, Tailwind utilities, and existing `nx-*`
  classes. Keep raw `--nx-*` definitions in tokens.css and their mapping in index.css.
- Reuse the existing radius/material ladder, SF Pro typography, and lucide icons.
  Preserve light/dark behavior rather than introducing local color or theme systems.
  Use the `--nx-r-*` step tokens in `tokens.css` (`rounded-sm|md|nav|lg|xl|2xl`) for
  every radius — never a raw px value, and never a number restated here: `tokens.css`
  is canonical per the rule above, and its own header comment listing the ladder has
  already drifted from its actual variable list once, so treat any number as stale
  until read from the file itself.
- Both themes cast real drop shadows (`--nx-elev`), but only floating surfaces
  (`.nx-card`, `.nx-overlay`) use it. `.nx-material` (sidebar, topbar, inline controls)
  is glass + hairline + specular inset with no cast; the primary button is the one
  control that casts, via `--nx-accent-glow`. Don't add a cast to a material surface
  or remove it from a card/overlay — that split is deliberate.
- One accent, not a role split: `--nx-tint` aliases `--nx-accent` and carries text,
  icons, and tints alike; `--nx-accent-grad` fills the primary button, brand mark, and
  meters. White text on the gradient's top stop has a documented AA contrast gap
  for normal-sized labels; see `docs/design-system/11-accessibility.md` for the
  measured ratio and WCAG thresholds. Treat this as an unresolved accessibility
  issue; any remediation should update the shared design system.
- In the chart gallery, reuse `components/palette.ts` and `components/chartMotion.ts`
  under `src/features/demo-charts/`: only 3 of the 5 `--color-chart-*` slots separate
  cleanly for colour-vision, so an identity-by-colour chart uses `SERIES` (chart-1 ·
  chart-2 · chart-5, that fixed order, never more than three) — more categories means
  a different chart form, not a 4th colour. Entry animation stays disabled via
  `STATIC_MARK` (recharts animates on `rAF`, which a backgrounded tab freezes at frame
  zero). Never two y-scales on one chart. Share helpers outside the feature before
  using them from another feature.
- Read the relevant existing component and source comments for detailed table,
  chart, and input contracts rather than copying entire demo implementations.

## Worktrees

- One task = one branch = one worktree = one agent session — don't run two agent
  sessions making concurrent edits in the same working tree. Put worktrees under
  `.worktrees/` at the repo root (gitignored); `git worktree add` does not copy
  `node_modules`, so run `npm run setup` in a fresh worktree before `dev`/`build`/`test`.
  Same for `graphify-out/` (see the repo-map entry above) — one `graphify update .`
  before relying on query/path/explain there.
- `.env*` files are untracked and won't exist in a fresh worktree; the app degrades
  correctly with no `.env` present (see `.env.example`): `/api/*` goes to the .NET host through the Vite
  proxy (`http://localhost:5208`), and MSW mocks only the features without a backend — that's the
  correct default, not something to "fix" by inventing a `.env`.
- Vite has no `strictPort`, so concurrent dev servers across worktrees auto-increment
  past 5173 instead of colliding — use the URL Vite actually reports (see Commands table).

## Risk boundaries

- **Session model (`src/lib/auth/`).** The short-lived access token lives in memory only
  (`useSessionStore`, never persisted — no token, refresh secret or session data in
  `localStorage`/`sessionStorage`; a boot step purges the legacy `fynovio-auth` key). A reload restores the
  session through `POST /auth/refresh`, using the HttpOnly `SameSite=Strict` refresh cookie the browser
  holds (`withCredentials`), before any route renders (`SessionGate`). One shared single-flight refresh
  serves the boot sequence and the 401 interceptor (Web Locks across tabs); a 401 on a non-`/auth/*` call
  refreshes and replays the request once, and a failed refresh ends the session as `expired`. 403 never
  logs out. The tenant is chosen server-side (`/auth/tenants/select` returns a token with that `tid`); the
  client never builds or edits a tenant claim, and clears the React Query cache on every login, tenant
  switch, expiry and sign-out. State-changing calls carry `X-Requested-With: fynovio` (the API's CSRF guard).
- **E-mailed link pages** (`/accept-invite`, `/reset-password`, in `authLinkRoutes`) sit outside `PublicOnlyRoute`
  on purpose: the single-use token in the link is the credential, and bouncing a signed-in browser away would
  lose it. The token travels in the URL *fragment* (never sent to a server or in `Referer`); `useFragmentToken`
  reads it once into memory and replaces the address, and it is never written to storage, logs or a query key
  that outlives the page (`gcTime: 0`). Every bad link — missing, expired, used, revoked — ends on the same
  `LinkInvalidNotice`; a reset ends the local session because the server revokes all of them; a wrong *current*
  password on `/auth/password/change` is a 400 (`invalid_current_password`), never a 401.
- Route guards (`ProtectedRoute`, `PublicOnlyRoute`, `SessionRoute`) and every `usePermission`/`<Can>`
  check are UX/navigation control, **not** authorization — the API authorises every request. Post-login
  destinations go through `sanitizeReturnUrl()` (same-origin relative paths only); never navigate to a
  raw `returnUrl`.
- `usePermission`/`hasPermission` is a mock-era remnant that **fails closed**: the real session carries no
  role, so it grants nothing. Real capabilities come from the backend in Phase 2.5B; today the mock policy
  only affects the pipeline demo's approve action.
- `RichTextInput` (`src/components/common/inputs/RichTextInput.tsx`) emits HTML and does not
  sanitize it — that is the caller's job. Any code that renders that HTML back
  (`dangerouslySetInnerHTML` or otherwise) must pass it through `sanitizeHtml()`
  (`src/lib/sanitizeHtml.ts`, allowlist-based via DOMPurify) first. Same shape of risk as the
  mock-auth boundary above: the control's contract ends at producing the string, the renderer is
  responsible for safety.
- Keep new public environment settings documented in `.env.example`; never put
  credentials in client-side `VITE_*` variables or commit populated secret files.
  `.gitignore` covers `*.local`, `.env`, and `.env.*`, with `.env.example` explicitly
  allowed for non-secret configuration examples.
- Preserve unrelated working-tree edits. Bootstrap documents are provisioning specs,
  not routine context: do not reload or execute them for ordinary development tasks.
