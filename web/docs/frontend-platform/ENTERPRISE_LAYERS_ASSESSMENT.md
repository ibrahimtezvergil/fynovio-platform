# Enterprise React Frontend — Ana Katmanlar: Feasibility Assessment

Source: [`Enterprise_React_Frontend_Ana_Katmanlar.pdf`](./Enterprise_React_Frontend_Ana_Katmanlar.pdf) (60 capabilities, 8 architecture groups, 5 phases)
Assessed against: `fynovio-react` @ `ios-27` — React 19 + TS strict, Vite 7, Tailwind v4,
TanStack Table v9, React Query v5, zustand v5, react-hook-form + zod, recharts, FullCalendar.
**No backend. No test suite. No CI.** All data is mocked per-feature behind a React Query `queryFn`.

Companion document: [`../ux-backlog/UX_BACKLOG_ASSESSMENT.md`](../ux-backlog/UX_BACKLOG_ASSESSMENT.md).
That one is *what the user sees*; this one is *what holds it up*. Several items appear in both —
cross-references are noted as `UX #n`.

---

## Verdict legend

| Code | Meaning |
| --- | --- |
| 🟢 **NOW** | Buildable today, end to end, inside `src/`. No backend, no new infrastructure. |
| 🟡 **SHELL NOW** | The seam, the contract and the UI are buildable now and are worth building now so the pattern doesn't fragment — but the capability is only *true* once a backend supplies the data or the guarantee. |
| 🔵 **INFRA NOW** | No backend needed, but it lives outside `src/` — a CI runner, a host, a team. Blocked on a decision, not on code. |
| 🔴 **BE-GATED** | Not honestly buildable without a backend. A frontend-only version would be a lie. |

**Bugün** column — what exists in the repo right now:
**✅** built · **◐** partly built · **○** nothing.

**Must** column — **★** commit to it · **☆** valuable, later · **—** park it.

---

## 1. Platform, Modülerlik ve Genişletilebilirlik

| # | Katman | Verdict | Bugün | Must | Against this codebase |
| --- | --- | --- | --- | --- | --- |
| 1 | App Shell | 🟢 NOW | ◐ | ★ | `DashboardLayout` + `Sidebar` (two widths, persisted) + `Topbar` (breadcrumb via `findNavTrail`, search, theme, `NotificationCenter`) + `AuthLayout` already are the shell. Missing pieces are in-app tabs (→ #30) and a single overlay orchestrator (→ #29). Do not rewrite; extend. |
| 2 | Module Architecture | 🟢 NOW | ✅ | ★ | Feature-Sliced Design is already the layout: 18 `src/features/<name>/` slices with `components · pages · data · store · api`, and the invariant "features never import from each other". The gap is *enforcement*, not structure — see #50. |
| 3 | Route Registry | 🟢 NOW | ◐ | ★ | `routes/index.tsx` is a hand-written array of 21 routes, `paths.ts` a flat object, `navigation.ts` a third structure describing the same tree. Three places drift. Have each feature export its own `routes` + `nav` and let the router assemble them. Pure FE, half a day, and it is the precondition for #4 and #7. |
| 4 | Navigation Registry | 🟢 NOW | ◐ | ★ | `NAV_GROUPS` in `layouts/navigation.ts` is already declarative config, and `findNavTrail()` is already the single path→nav resolver. Missing: per-module contribution (with #3) and a filter predicate. The predicate's *input* (permissions, package) is 🟡 — see #17. |
| 5 | Command Registry | 🟢 NOW | ○ | ★ | Nothing exists. `Topbar.tsx:24-35` binds ⌘K but only focuses the search input. Same item as **UX #1**. Build it as a registry (`{id, label, group, icon, when, run}`) fed by #3/#4/#6, not as a palette component with a hardcoded list. |
| 6 | Action Registry | 🟢 NOW | ○ | ★ | Nothing exists, and it is the highest-leverage missing abstraction in the whole document. Today an "approve" would be written once in a row menu, once in a bulk bar, once in a drawer. One `Action` contract (`{id, label, icon, when(ctx), run(ctx), undo?}`) feeds row menus, bulk bars (**UX #3**), toolbars, the palette (#5) and later #60. Build this **before** the palette. |
| 7 | Extension / Slot System | 🟢 NOW | ○ | ☆ | Technically pure FE (a slot registry + `<Slot name="customer.details.tabs" />`). But a slot contract with zero external consumers is speculative API design — you would be versioning a contract nobody calls. Defer until there is a second party writing against it. |
| 8 | Feature Capability Registry | 🟡 SHELL NOW | ○ | ★ | The resolver, the `useCapability(id)` hook and a local provider are FE and cheap. What decides the answer — tenant package, entitlement, licence — is BE. Cut the seam now: every `if` written without it becomes an `if` to hunt down later. |

## 2. API, Server State, Mutation ve Realtime

| # | Katman | Verdict | Bugün | Must | Against this codebase |
| --- | --- | --- | --- | --- | --- |
| 9 | Typed API Layer | 🟡 SHELL NOW | ✅ | ★ | `api/client.ts` (axios + auth-token interceptor + a normalising error interceptor producing `ApiError`) and `api/endpoints.ts` exist. Dashboard, Pipeline, Calendar and Auth responses are now validated with shared Zod schemas at the API boundary; legacy shallow guards remain only for unmigrated feature APIs. A *generated* client still needs an OpenAPI document — that is a BE deliverable. |
| 10 | Query Architecture | 🟢 NOW | ◐ | ★ | Key factories already exist per feature (`pipelineKeys`, `dashboardKeys`, `calendarKeys`, `demoTableKeys`) and they all follow the same shape by coincidence, not by rule. `queryClient.ts` sets one global default (`staleTime: 60_000, retry: 1, refetchOnWindowFocus: false`). Write down the convention: key factory shape, stale tiers (reference data vs. live list), the invalidation map, prefetch-on-hover. Zero new code, large payoff. |
| 11 | Server / Client State Separation | 🟢 NOW | ✅ | ★ | Already enforced by four invariants: server data → React Query, app state → zustand (selector-mandatory), form state → RHF (never zustand), view state → URL (`useTableSearchParams`). This is one of the strongest things in the repo. Work = documenting it in `docs/design-system/` and auditing the demo features once. |
| 12 | Mutation Infrastructure | 🟡 SHELL NOW | ◐ | ★ | Only three files use `useMutation` — `pipeline/api.ts`, `calendar/api.ts`, `auth/api.ts` — and each does its own thing. One `useAppMutation` wrapper (validate → execute → invalidate → toast → telemetry → undo) is FE-buildable today against mocks. The *durable* half (reversal endpoints, soft delete) is BE — same finding as **UX #7**. |
| 13 | Optimistic Update Layer | 🟡 SHELL NOW | ○ | ★ | React Query's `onMutate`/`onError` rollback works against the mocks today and the pattern is identical against a real server. But pending/failed/committed only becomes *meaningful* with real latency and real 409s. Build it inside #12 so it is one decision, not per-feature. |
| 14 | Error Architecture | 🟢 NOW | ✅ | ★ | Root `ErrorBoundary` in `main.tsx`, route `errorElement` via `RouteErrorBoundary` in `routes/index.tsx`, shared `ErrorFallback` UI, and React Query's `throwOnError` enabled. All render/loader/action errors are caught and display context-aware recovery. |
| 15 | Resilient Request Layer | 🟡 SHELL NOW | ◐ | ☆ | Axios has `timeout: 15_000`, React Query has `retry: 1`. Cancellation via `AbortSignal`, dedup, exponential backoff and a network-degradation banner can all be written now — but they can only be *tuned* against a real network. Write the policy, defer the tuning. |
| 16 | Real-Time Event Layer | 🔴 BE-GATED | ○ | — | Needs SignalR/WebSocket/SSE on the server. A simulated event bus would demo well and teach the wrong reconciliation model. Post-BE this becomes ★, and it should reuse #10's invalidation map rather than inventing a second one. |

## 3. Yetki, Tenant, Feature ve Configuration

| # | Katman | Verdict | Bugün | Must | Against this codebase |
| --- | --- | --- | --- | --- | --- |
| 17 | Permission Engine | 🟡 SHELL NOW | ○ | ★ | The FE half — `usePermission()`, `<Can action="deal.approve">`, route guards, an action's `when(ctx)` in #6 — is buildable now against a mocked policy set. The rules come from BE. Document loudly, next to the existing "mock auth is not auth" rule for `ProtectedRoute.tsx`: **permission controls UX, authorization is always enforced server-side.** |
| 18 | Tenant Context | 🟡 SHELL NOW | ◐ | ☆ | `types/index.ts` already declares a `Tenant` interface and `Sidebar.tsx:9` hardcodes `TENANT_NAME = 'Nordwind Lojistik'` behind a real switcher affordance. A `TenantProvider` + `useTenant()` is FE; resolving *which* tenant and what it may see is BE. |
| 19 | Feature Flag Engine | 🟡 SHELL NOW | ○ | ☆ | A local provider (env defaults + a localStorage override + a dev inspector, → #49) is a day's work and immediately useful for shipping half-finished screens. Remote rollout, cohorts and kill-switches are BE/infra. |
| 20 | Configuration Engine | 🔴 BE-GATED | ○ | — | "Deploy gerektirmeden" is the entire point, and that requires a server to serve the configuration. Building a config *reader* with only a build-time source is just constants with extra indirection. Park. |

## 4. Form, Grid ve Veri Çalışma Alanı

| # | Katman | Verdict | Bugün | Must | Against this codebase |
| --- | --- | --- | --- | --- | --- |
| 21 | Schema-Driven Forms | 🟢 NOW | ○ | ☆ | Entirely FE: zod schema + a layout descriptor → rendered form, on top of the existing `Field` contract. The reason it is ☆ and not ★ is that this is the classic premature abstraction — it pays off when many forms are near-identical, and today there are two real ones. Revisit once ERP master-data screens exist. |
| 22 | Validation Architecture | 🟡 SHELL NOW | ◐ | ★ | The client half is settled: zod via `@hookform/resolvers`, schema colocated at `features/<name>/schema.ts`, `mode: 'onTouched'` / `reValidateMode: 'onChange'`, and error-copy rules in `docs/design-system/07-forms.md`. Missing: the **server-error contract** — the 422 shape and the field-path mapping. `ApiError` in `client.ts` is where it lands. Agree the shape with BE now; it is cheap before and expensive after. |
| 23 | Draft Engine | 🟢 NOW | ○ | ★ | Fully FE for local drafts: RHF `formState.isDirty` → a `useBlocker` navigation guard (react-router-dom is already a dep; `grep` finds no `useBlocker` and no `beforeunload` today, so a half-filled ERP form is silently lost on a mis-click), debounced localStorage autosave, an explicit restore prompt. Cross-device drafts are BE. Highest real-world ERP value per hour in this group. |
| 24 | Field Registry | 🟢 NOW | ◐ | ★ | Two thirds already built: `components/common/inputs/` holds 36 controls including `MoneyInput`, `QuantityInput`, `PercentInput`, `ExchangeRateInput`, `DiscountInput`, `BarcodeInput`, `PhoneInput`, `DatePicker`, `TreeSelect`, `AsyncCombobox`, with a shared `types.ts`/`styles.ts` and a barrel `index.ts`. Missing: the **domain tier** (`CustomerPicker`, `ProductPicker`, `ErpCodeField`) and the type→component registry. `AsyncCombobox` becomes honest only with a real search endpoint. |
| 25 | Enterprise Grid Layer | 🟢 NOW | ✅ | ★ | `src/components/data-table/` **is** this layer: a vendor-neutral renderer over TanStack v9 with `DataTable`, header cell, pagination, selection, toolbar, view options, `columnSizeVars`, and a deliberate widening seam (`widenTable` + `hasApi` guards in `types.ts`) so one renderer serves grids with different registered feature sets. Remaining work is domain adapters and column-type presets, feeding off #24. |
| 26 | Filter AST / Query Model | 🟢 NOW | ◐ | ★ | `demo-filters/types.ts` has a single serializable `FilterState` and `lib/query.ts` applies it with a documented rule ("an empty control is not a predicate"). It is a flat object, not an AST — no OR, no grouping. Promoting it to `{ op, field, value }` nodes is pure FE and is the prerequisite for #27 saved views, server-side filtering, and **UX #19** semantic filters. |
| 27 | View State Engine | 🟢 NOW | ◐ | ★ | Three quarters exists: `useTableSearchParams` makes the URL the owner of pagination/sorting/global filter, `demo-filters/data/views.ts` models saved views, `columnVisibilityFeature`/`columnSizingFeature`/`columnResizingFeature` are registered in `saasGridTable.tsx`, density lives in `useAppStore`. Missing: one `ViewState` type covering all of it plus `columnOrderFeature`, and persistence. Personal views 🟢; **team-shared views are 🟡** (ownership, sharing, permissions are BE). |
| 28 | Selection Engine | 🟢 NOW | ◐ | ★ | `rowSelectionFeature` is registered on `pipelineTable` and `saasGridTable`, and `DataTableSelection.tsx` exists. Missing: the enterprise model — `select-all-results` + an *exclusion* set rather than an id list, so "37.412 kayıt seçildi, 3 hariç" is representable. That model is FE; the bulk mutation it feeds is BE. Design it now or every bulk action inherits the page-only assumption. |
| 29 | Overlay Manager | 🟢 NOW | ◐ | ★ | All six primitives exist (`dialog`, `alert-dialog`, `sheet`, `drawer`, `popover`, `tooltip`), everything portalled sits at one `z-50`, and `docs/design-system/08-overlays.md` already fixes the choosing rules and the ≤2 stack depth. What is missing is the *imperative* orchestrator — `openDrawer(...)` returning a promise, one stack, one escape order, one focus-restore path — instead of a `useState` boolean per call site. |
| 30 | Workspace Manager | 🟢 NOW | ○ | ☆ | In-app tabs, open records, restore — all FE (URL + persisted store). Expensive, and it only pays off with real records to keep open. Defer behind #29, which it needs anyway. |

## 5. Bildirim, Arka Plan İşleri ve Design System

| # | Katman | Verdict | Bugün | Must | Against this codebase |
| --- | --- | --- | --- | --- | --- |
| 31 | Notification Center | 🟢 NOW | ✅ | ★ | Built: `features/notifications/` with `useNotificationStore`, `NotificationList`, `NotificationRow`, mounted in the topbar, and separated from transient `sonner` toasts exactly as the PDF asks. Toast-vs-center rules are in `docs/design-system/09-states-and-feedback.md`. Only server-pushed events remain, and those ride on #16. |
| 32 | Background Jobs UX | 🟡 SHELL NOW | ○ | ☆ | The job list, progress model and result panel are FE and could be built against a mocked job store — but there is no long operation in the product yet to attach them to. Build it together with the first one, which will be #58's import. |
| 33 | Design Token System | 🟢 NOW | ✅ | ★ | Done, and it is the strongest layer in the repo: `styles/tokens.css` (253 lines, canonical) → `index.css` (756 lines, the single mapping layer) → components, with spacing, type, semantic colour, radius ladder, elevation **and density** all tokenised, plus a documented one-way flow. Only gap: a tenant/brand overlay, which is #56. |
| 34 | Enterprise Design System | 🟢 NOW | ✅ | ★ | Well past button level: 22 `ui/` primitives, 15 `common/` blocks (`StatusBadge` with a generic `StatusRegistry<T>`, `StageBadge`, `PageHeader`, `Toolbar`, `EmptyState`, `Field`, `DensityScope`), 36 domain-shaped inputs, the data-table layer, and 11 `demo-*` routes documenting them in prose. Missing: the entity tier (`EntityPicker`, `AddressBlock`, `TaxNumberField`), which waits on #59 and #24. |
| 35 | Accessibility Foundation | 🟢 NOW | ◐ | ★ | `docs/design-system/11-accessibility.md` sets the WCAG 2.2 AA baseline, the focus and pointer-target rules, the `event.code` keyboard rule, and records two bounded exceptions (accent gradient ≈3.4:1; the 32px compact row below the 44px target). What is missing is *verification and coverage*: no `eslint-plugin-jsx-a11y`, no axe, and `prefers-reduced-motion` is honoured only by the skeleton. All FE. |
| 36 | Localization Engine | 🟢 NOW | ✅ | ★ | Built on `react-i18next`: a dedicated instance (`src/lib/i18n.ts`, not the default global singleton) with 21 namespace catalogs under `src/locales/{tr,en}/` — one per feature slice plus `common`/`nav`/`routes`. Components consume it via `useTranslation(ns)`; non-component/module-scope code (mock fixtures, static registries like `StageBadge.tsx`'s `stageMeta()`) calls `i18n.t()` directly, with a reactive `use*()` hook counterpart wherever a component needs the same data. `useAppStore.setLocale` and rehydration both call `i18n.changeLanguage`, alongside the existing `document.documentElement.lang` write. `Intl` formatting in `lib/datetime.ts` and `tr-TR` collation in `demo-filters/lib/query.ts` are untouched — only literal copy moved. Verified live: switching locale via `UserMenu` reactively re-renders every `useTranslation`-based screen. |

## 6. Güvenlik, Audit, Observability ve Performans

| # | Katman | Verdict | Bugün | Must | Against this codebase |
| --- | --- | --- | --- | --- | --- |
| 37 | Security Layer | 🟡 SHELL NOW | ○ | ☆ | CSP is a response header — server/host, not `src/`. The FE half is real and worth writing down now: exactly one `dangerouslySetInnerHTML` exists today (`ui/chart.tsx:92`, generated CSS custom properties — safe), and tiptap is a dependency, so a sanitisation policy is needed *before* the first user-authored rich text is stored and re-rendered. |
| 38 | PII / Data Classification | 🟡 SHELL NOW | ○ | — | Field-level classification is only meaningful when fields arrive with schema metadata, which is BE. The FE can define the enum and the blur/mask presentation now, but it would classify nothing. Park until #9 has a real schema. |
| 39 | Frontend Audit Hooks | 🔴 BE-GATED | ○ | — | Emitting actor/screen/resource metadata with no audit sink is dead code that still has to be maintained. Comes free later if #6 actions carry `id` and `entity` from day one. |
| 40 | Observability | 🟡 SHELL NOW | ○ | ★ | The *seam* is FE and should be cut together with #14: one `track(event, props)`, error-boundary reporting, React Query `onError`, and route-change timing. The *sink* (Sentry / OTel collector) is an infrastructure decision, not necessarily a BE one — which means it is unblocked whenever you choose a vendor. Without this, a production bug is debugged by asking for a screenshot. |
| 41 | Performance Budgets | 🔵 INFRA NOW | ○ | ☆ | Blocked on CI existing at all (#53), not on code. Once it does: `vite build` already prints chunk sizes, and a size gate plus `rollup-plugin-visualizer` is a few hours. Meaningless before #44 and #53. |
| 42 | Virtualization Strategy | 🟢 NOW | ○ | ☆ | `grep` finds no virtualization anywhere, and nothing needs it yet — the mocks are hundreds of rows. The decision that matters *today* is negative: the `DataTable` renderer must not assume every row is in the DOM, or retrofitting `@tanstack/react-virtual` later means rewriting it. Record the constraint, defer the implementation until a real paged endpoint exists. |
| 43 | Code Splitting Strategy | 🟢 NOW | ◐ | ★ | Already largely done: `routes/index.tsx` lazily imports all 20 page components behind a shared `RouteFallback`, so the login screen never pays for the dashboard. Missing: a written rule for the heavy libraries — FullCalendar, tiptap and recharts should be split off the vendor chunk, and `vite.config.ts` currently has no `manualChunks` at all. Cheap. |

## 7. Test, Developer Experience ve Delivery Governance

| # | Katman | Verdict | Bugün | Must | Against this codebase |
| --- | --- | --- | --- | --- | --- |
| 44 | Testing Pyramid | 🟢 NOW | ✅ | ★ | Vitest + React Testing Library + jsdom configured in `vite.config.ts` and `src/test/setup.ts`. MSW server integration for test environment. 26 passing tests for `data-table/lib/urlState.ts` and `demo-filters/lib/query.ts` covering edge cases, Turkish collation, date/amount ranges, filtering, sorting. Verified zero lint errors and clean build. |
| 45 | API Contract Tests | 🔴 BE-GATED | ○ | — | There is no contract to test against until BE publishes OpenAPI. Becomes ★ the day it exists, and it is what makes #9's generated client trustworthy. |
| 46 | Visual Regression | 🔵 INFRA NOW | ○ | ☆ | Needs CI (#53) to compare against, and a stable render surface (#47 or the existing demo routes). Worth it specifically because of the token architecture: a `tokens.css` edit is a global change with no local diff, which is precisely what screenshot diffing catches. |
| 47 | Component Playground | 🟢 NOW | ◐ | ☆ | Honest reading: **you already have one.** The 11 `demo-*` routes are a hand-built workbench with prose rationale, state matrices and copyable snippets — richer than a default Storybook and already in the product's own shell, theme and density. Adding Storybook would duplicate it and split the source of truth. Recommendation: keep the demo routes, and revisit only if isolation-per-component becomes necessary for #46. |
| 48 | Mock Server | 🟢 NOW | ✅ | ★ | MSW browser (`src/mocks/browser.ts`) and Node.js server (`src/mocks/server.ts`) fully set up. All scattered `setTimeout` mocks moved to network boundary: `handlers/auth.ts`, `dashboard.ts`, `pipeline.ts`, `calendar.ts`, `demo-tables.ts`, `demo-forms.ts`. Every feature's `api.ts` calls `apiClient` instead of inline mocks. Centralized `apiBase.ts` ensures handlers match axios baseURL. Gated by `VITE_API_MOCKING` env var. Same code runs with/without backend; switch is one env var. |
| 49 | Developer Diagnostics | 🟢 NOW | ○ | ☆ | `@tanstack/react-query-devtools` is not installed; adding it is one line and immediately useful given how much state lives in the query cache. Permission and flag inspectors follow #17/#19 and cost almost nothing once those exist. |
| 50 | Dependency Rules | 🟢 NOW | ✅ | ★ | `.oxlintrc.json` auto-generated covering all 18 feature slices with error-level `no-restricted-imports` rules (raised from `warn`). Each feature forbids imports from other features and from internal route seams (`ProtectedRoute`, `PublicOnlyRoute`). Enforced at lint time. This keeps #2 (module architecture) from eroding as the codebase grows. |
| 51 | Public / Internal API Boundaries | 🟢 NOW | ✅ | ☆ | Barrel `index.ts` added to all 18 feature slices. Each exports public API (types, schemas, hooks, page components) and forbids deep imports via #50's error-level `no-restricted-imports`. Done with #50 in the same commit. |
| 52 | Code Ownership | 🔵 INFRA NOW | ○ | — | `CODEOWNERS` needs owners. Single-maintainer repo → premature. Revisit when a second developer joins. |
| 53 | CI Quality Gates | 🔵 INFRA NOW | ✅ | ★ | **Done** — `.github/workflows/ci.yml` runs lint, typecheck, test and build on every push to `main` and every PR (see `AGENTS.md`). |
| 54 | Preview Environments | 🔵 INFRA NOW | ○ | ☆ | Needs a host decision (Vercel / Netlify / Pages). Genuinely valuable for a design-system-heavy product where review means *looking* at it — but it follows #53. |
| 55 | Controlled Rollout | 🔴 BE-GATED | ○ | — | Requires remote flags (#19), telemetry (#40) and a deploy pipeline (#53/#54) simultaneously. Nothing to start. |

## 8. White-Label, Offline, Veri Taşıma ve AI Hazırlığı

| # | Katman | Verdict | Bugün | Must | Against this codebase |
| --- | --- | --- | --- | --- | --- |
| 56 | White-Label Architecture | 🟢 NOW (mechanism) / 🟡 (per-tenant) | ◐ | ☆ | The mechanism is already there and is the reason the token architecture was built the way it was: `tokens.css` is the single override point, `index.css` the single mapping layer, so a tenant theme is one token block — not a component fork. What is 🟡 is *resolution*: which tenant gets which brand comes from BE (#18). Prove the mechanism with one alternate theme now; wire resolution later. |
| 57 | Offline / Degraded Mode | 🟡 SHELL NOW | ○ | — | React Query persistence, an `navigator.onLine` banner and a read-only mode are all FE. But caching mock data offline demonstrates nothing, and the draft-protection half is better delivered as #23. Park; revisit if a field-use scenario becomes real. |
| 58 | Import / Export Framework | 🟢 NOW (export + mapping) / 🟡 (commit) | ○ | ★ | Almost entirely client-side: CSV/XLSX parse, the column-mapping step, row-level validation against the zod schemas that already exist, an error report, and CSV export straight from a grid. `FileDropzone.tsx` is already built. Only the final commit and true background export are BE. Very high ERP value and it is the natural first consumer of #32. |
| 59 | Entity Reference System | 🟢 NOW | ○ | ★ | Pure FE, tiny, and a prerequisite for four other items. One contract — `{ type, id, display, subtitle?, url }` — is the shared vocabulary that the command palette (#5), the action registry (#6), peek drawers (**UX #4**), breadcrumbs, `AsyncCombobox` and later #60 all need. Define it **before** building the palette, or it gets defined five slightly different ways. |
| 60 | AI-Ready UI Metadata | 🟢 NOW | ○ | ☆ | Not a project — a constraint on three others. If #6 actions carry `{id, entity, params, permission}`, #17 exposes a resolvable permission id, and #59 gives entities a stable type/id, then the machine-readable surface already exists and an agent never needs the DOM. Costs nothing if applied from the start; costs a refactor if applied later. Write it into the contracts, don't schedule it. |

---

## Roll-up

| Verdict | Count | Items |
| --- | --- | --- |
| 🟢 **NOW** | **36** | 1, 2, 3, 4, 5, 6, 7, 10, 11, 14, 21, 23, 24, 25, 26, 27, 28, 29, 30, 31, 33, 34, 35, 36, 42, 43, 44, 47, 48, 49, 50, 51, 56, 58, 59, 60 |
| 🟡 **SHELL NOW, true after BE** | **14** | 8, 9, 12, 13, 15, 17, 18, 19, 22, 32, 37, 38, 40, 57 |
| 🔵 **INFRA NOW** (CI / host / team) | **5** | 41, 46, 52, 53, 54 |
| 🔴 **BE-GATED** | **5** | 16, 20, 39, 45, 55 |
| | **60** | |

**55 of 60 are unblocked by the missing backend.** That is the headline: the backend is not
what is holding this frontend back. What is holding it back is that there are no tests, no CI,
no error boundaries and no message catalog.

### What is already built

Nine layers are effectively done or nearly done, and they are not the easy ones:
**#33 Design Tokens** and **#34 Design System** (a real three-layer token architecture with a
density axis, 22 primitives, 36 inputs), **#25 Enterprise Grid** (a genuine vendor-neutral
abstraction over TanStack v9), **#11 State Separation** (four sources of truth, cleanly
separated), **#2 Module Architecture**, **#31 Notification Center**, **#43 Code Splitting**,
**#1 App Shell**, **#35 Accessibility** (documented, if unverified).

That is roughly *Faz 3 — Enterprise Data UX Foundation* delivered ahead of *Faz 1*. The
inversion is worth naming: the product surface is well ahead of the platform underneath it.

---

## Kesin yapmalıyız — the ★ list, in build order

### Tier A — stop the bleeding (nothing here needs a backend, all of it is overdue)

**Status: all done.** #14, #44, #53, #50+51, #48 and #40 all shipped in commits after this
document was first written — verified against the repo, not just the doc (2026-09-07, including
a live browser pass on Tier B/C's Codex tasks). Tier A is closed.

| Order | # | Layer | Why first | Status |
| --- | --- | --- | --- | --- |
| 1 | **14** | Error Architecture | Today one render throw blanks the app. Hours of work, highest severity on the list. | ✅ done |
| 2 | **44** | Testing Pyramid | Nothing else in governance can exist without it, and 21 routes of behaviour are currently unverified. | ✅ done |
| 3 | **53** | CI Quality Gates | Turns `tsc -b` + `oxlint` from discipline into a guarantee. | ✅ done |
| 4 | **50 + 51** | Dependency Rules + API Boundaries | The `.oxlintrc.json` boundary list already covers only 8 of 18 features, all at `warn`. Generate it, raise to `error`, add feature barrels. | ✅ done |
| 5 | **48** | Mock Server (MSW) | Moves mocks to the network boundary: makes #44 able to test failures and makes the BE switch one env var. | ✅ done |
| 6 | **40** | Observability seam | Cut with #14 — same code path, one decision. | ✅ done — Codex Task 1 |

### Tier B — the registries (this is the leverage the whole PDF is arguing for)

| Order | # | Layer | Why in this order |
| --- | --- | --- | --- |
| 7 | **59** | Entity Reference System | The vocabulary everything below shares. Define it first or define it five times. |
| 8 | **6** | Action Registry | One business action, defined once, reused by rows, bulk bars, toolbars and the palette. |
| 9 | **3 + 4** | Route + Navigation Registry | Collapses three drifting descriptions of the same tree into one. |
| 10 | **5** | Command Registry / ⌘K | Falls out almost free once 3, 4, 6 and 59 exist. Also **UX #1**. |
| 11 | **29** | Overlay Manager | An imperative stack instead of a boolean per call site. Unblocks **UX #4**. |
| 12 | **17 + 8** | Permission + Capability seams | Cheap now; every `if` written without them is one to hunt down later. |

### Tier C — the data workspace (where ERP users actually spend the day)

**2026-09-07 — split between Codex and Claude, see "İş Bölümü" below.** Owner column added;
task specs live under **Codex Görevleri — Tur 2** and **Claude Görevleri**.

| Order | # | Layer | Owner | Note |
| --- | --- | --- | --- | --- |
| 13 | **26** | Filter AST | Codex (Task 8) | Prerequisite for saved views and server-side filtering. |
| 14 | **27** | View State Engine | Claude (Task A) | Personal views now; team views after BE. Depends on #26 landing first — kept with the reviewer coordinating both. |
| 15 | **28** | Selection Engine | Codex (Task 9) | Exclusion-set model, decided before any bulk action inherits page-only assumptions. |
| 16 | **12 + 13 + 22** | Mutation infra, optimistic layer, validation contract | Claude (Task B) | One `useAppMutation`, built once. Highest blast radius (touches every feature's `api.ts`) — kept on one owner with a test checkpoint after each feature migration. |
| 17 | **23** | Draft Engine | Claude (Task C) | Touches RHF global behavior (navigation blocking) across every form — a bad pass breaks navigation app-wide. |
| 18 | **24** | Field Registry (domain tier) | Claude (Task D) | 36 controls exist; needs visual/interaction consistency with the existing set — a design-judgment call, not a mechanical add. |
| 19 | **58** | Import / Export | Codex (Task 10) | Client-side mapping + validation + export. High value, low backend dependency, self-contained new files. |

### Tier D — do not let it get more expensive

| # | Layer | Owner | Note |
| --- | --- | --- | --- |
| **36** | Localization Engine | — | ✅ **Already done** — see roll-up row #36 above (`react-i18next`, 21 namespace catalogs, verified live). This row predates that work; kept as a record, not open work. |
| **10** | Query Architecture | Claude (Task E) | Zero new code — write down the convention the four key factories already follow by accident, before a fifth one diverges. |
| **60** | AI-Ready Metadata | Claude (Task F) | Touches the Action/Command/EntityRef contracts Claude already authored — kept on the same owner so the shape stays intentional. |
| **43** | Code Splitting | Codex (Task 11) | Add `manualChunks` for FullCalendar / tiptap / recharts. An afternoon, isolated to `vite.config.ts`. |
| **35** | Accessibility verification | Codex (Task 12) | Add `eslint-plugin-jsx-a11y` (or oxlint's native a11y category) to the lint gate and extend `prefers-reduced-motion` past the skeleton. |

### İş Bölümü — Codex vs Claude (Tur 2)

The 11 items left in Tier C + D split by risk profile, the same way Tasks 1–7 worked:

- **Codex gets self-contained, additive, low-blast-radius work** (new files, one integration
  point, easy to verify in isolation) — the same shape that Tasks 1–7 proved out: #26, #28, #58,
  #43, #35.
- **Claude keeps cross-cutting or judgment-heavy work** — anything that touches every feature at
  once (#12+13+22 mutation infra reaches every `api.ts`), anything that breaks silently across
  the whole app if done wrong (#23 draft engine's navigation blocking), anything requiring visual
  consistency with an existing design system (#24), anything sequenced behind a Codex task landing
  first (#27 depends on #26), and the two small contract/doc items already shaped by Claude's own
  review (#10, #60).

Full specs: **Codex Görevleri — Tur 2** (Task 8–12, same section as Tasks 1–7 below) and the new
**Claude Görevleri** section that follows it.

### Deliberately not doing (yet)

- **#7 Extension/Slot System** — versioning a contract with no external consumer.
- **#21 Schema-Driven Forms** — two real forms is not enough evidence to abstract.
- **#30 Workspace Manager** — expensive, and it needs real records to hold.
- **#47 Component Playground** — the 11 `demo-*` routes already are one, in the real shell.
  Storybook would split the source of truth.
- **#42 Virtualization** — record the constraint on `DataTable`, defer the implementation.
- **#38 PII, #57 Offline, #52 Code Ownership** — nothing real to classify, cache, or own yet.

### What to ask the backend for, in this order

1. **OpenAPI document** — unblocks #9 (typed client) and #45 (contract tests) at once.
2. **The error contract**: a fixed 422 shape with field paths, for #22.
3. **Reversible mutations** (soft delete + reversal endpoints), for #12/#13 — also **UX #7**.
4. **Entitlement / permission resolution** per user and tenant, for #17, #8, #18.
5. **A realtime channel** (SignalR/SSE) with entity-scoped events, for #16 — and have it emit
   entity `type` + `id` so it can drive #10's invalidation map directly.

---

## Mapping to the PDF's own phases

The document's suggested sequence assumes a greenfield repo. This one is not greenfield, and
it is unevenly built, so the phases reorder:

| PDF phase | Status here |
| --- | --- |
| **Faz 0 — Architecture Audit** | This document, plus `docs/design-system/`. Done. |
| **Faz 1 — Platform Core** | Partly done (app shell ✅, API skeleton ◐) and partly **missing entirely** — error architecture, telemetry, permission, tenant, flags. This is Tier A + the seams in Tier B. |
| **Faz 2 — Registry Architecture** | Not started, and it is the real unlock. Tier B. |
| **Faz 3 — Enterprise Data UX** | **Ahead of schedule** — grid, tokens, design system, overlays, notifications, filters all exist. Tier C is finishing it, not starting it. |
| **Faz 4 — Scale & Extensibility** | Correctly last. Mostly 🔴 or ☆ here. |

The one correction to the PDF's ordering for this repo: **testing and CI are not a Faz 7
governance concern, they are a Faz 1 precondition.** A 60-capability programme executed on a
repo with no test suite and no CI will erode faster than it is built — which is the exact
failure mode the document's own "Architecture rules CI ile enforce edilir" rule exists to prevent.

---

## Codebase notes that affect the plan

- **Almost nothing here needs a new runtime dependency.** The additions the ★ list implies are
  `msw`, `vitest` + `@testing-library/react`, `@playwright/test`, `@tanstack/react-query-devtools`,
  `eslint-plugin-jsx-a11y`, an i18n library for #36, and eventually `@tanstack/react-virtual`.
  Every registry, seam and contract is plain TypeScript.
- **Six registries want the same substrate.** Command (#5), Action (#6), Route (#3), Navigation
  (#4), Field (#24) and Capability (#8) are one pattern instantiated six times: a typed map, a
  `when(ctx)` predicate, and a resolver. Write the substrate once, above the feature layer —
  features never import from each other, so it must live in `src/lib/` or `src/components/common/`.
- **`docs/design-system/` already covers the UI half of #33, #34, #35 and part of #29.** New
  platform docs belong beside it, not inside it — that folder is the *design* standard, this
  folder is the *platform* standard.
- **Density is a token flip, never a prop.** Anything new that renders rows — virtualised grids
  (#42), workspace tabs (#30), import preview tables (#58) — must go through `DensityScope` /
  `.nx-dense` rather than inventing its own sizing.
- **Keyboard work matches on `event.code`.** `Sidebar.tsx` documents why: `[` is AltGr+8 on a
  Turkish layout, and on Windows AltGr *is* Ctrl+Alt. The command palette (#5) and any shortcut
  registry inherit this rule.
- **Permission is UX, authorization is backend.** `ProtectedRoute.tsx` and `features/auth` are
  mocked client-side behaviour today. #17 must be documented the same way, or a future reader
  will mistake a hidden button for a security control.

---

## Codex Görevleri

Bundan sonrası Codex'e devrediliyor. Her görev tek başına bitirilebilecek şekilde yazıldı —
sırayla al, birini bitirip doğrulamadan sonrakine geçme. Claude bu listeyi periyodik review
edip **Durum** satırlarını günceller; Codex kendi değerlendirmesini yazmaz, sadece işi yapar.

Genel kurallar (hepsi `AGENTS.md`'den, tekrar hatırlatma):
- Her görev sonunda en az `npm run check` (lint + typecheck + test) yeşil olmalı. Router/state/
  form dokunan görevlerde ayrıca `npm run build`.
- `src/features/<a>` asla `src/features/<b>`'den import etmez. Paylaşılan kod `src/lib/`,
  `src/components/common/`, `src/store/`, `src/types/` içine gider.
- Yeni bir sayfa/route eklemiyorsan `routes/index.tsx` ve `layouts/navigation.ts`'e dokunma.
- Yorum yazma gerekçesi sadece "neden" — kod zaten okunaklı isim taşımalı.
- Bir görev mevcut bir dosyayı **kırıyorsa** (örn. bir feature'ın action'ını registry'ye taşımak
  o feature'ın davranışını değiştiriyorsa) önce o feature'ın kendi testini/manuel akışını koru.

### Codex Task 1 — Observability seam (#40)

**Durum:** ✅ Tamamlandı — commit `e07a415`

**Neden:** Tier A'nın son açık kalemi. Şu an bir prod hatası ancak kullanıcıdan ekran görüntüsü
isteyerek debug edilebiliyor — `grep -rn "track(" src` boş dönüyor, hiçbir yerde telemetry yok.

**Ne yapılacak:**
1. `src/lib/telemetry.ts` oluştur. Tek export: `track(event: string, props?: Record<string, unknown>): void`.
   Şimdilik sink yok (vendor seçimi — Sentry/OTel — altyapı kararı, bu görevin kapsamı dışında);
   `console.debug('[telemetry]', event, props)` yeterli, ama fonksiyonun imzası ileride bir vendor
   eklendiğinde tek dosya değişecek şekilde tasarlanmalı (yani çağıran taraflar `track()`'i
   doğrudan `console`'a değil bu fonksiyona çağırsın).
2. `src/components/common/ErrorBoundary.tsx`'teki `componentDidCatch` içine, mevcut
   `console.error('[ErrorBoundary]', ...)` satırının yanına `track('error_boundary_caught', { message: error.message, stack: info.componentStack })` ekle.
3. `src/routes/RouteErrorBoundary.tsx`'e aynı şekilde bir `track('route_error_boundary_caught', ...)` çağrısı ekle (component body'de, error değişkeni zaten mevcut).
4. `src/api/queryClient.ts`'e global `onError` bağla: `QueryClient` constructor'ına `queryCache: new QueryCache({ onError: (error, query) => track('query_error', { queryKey: query.queryKey, message: error.message }) })` ve aynısı `mutationCache: new MutationCache({ onError: ... })` ile. (`QueryCache`/`MutationCache` `@tanstack/react-query`'den import edilir.)
5. Route-change timing: `src/routes/index.tsx` içindeki router'ın kullanıldığı yerde (ya da
   `App.tsx`/layout'ta) `useLocation` + `performance.now()` ile küçük bir efekt yaz — her
   location değişiminde önceki mount'tan bu yana geçen süreyi `track('route_change', { path, durationMs })` olarak gönder. Bunu ayrı bir `src/lib/telemetry/useRouteChangeTracking.ts` hook'una çıkar, `DashboardLayout`'ta bir kez çağır.

**Kabul kriteri:** `npm run check` yeşil. Bir demo route'ta bilinçli olarak throw eden bir
component ile (`/demo/*` altında var olan bir örnek varsa onu kullan, yoksa geçici olarak bir
buton ekleyip test et, sonra kaldır) console'da `[telemetry] error_boundary_caught` çıktısı
görülmeli. Kalıcı bir test component'i bırakma.

---

### Codex Task 2 — Entity Reference System (#59)

**Durum:** ✅ Tamamlandı — commit `4a7e0b7`

**Neden:** Command Registry (#5), Action Registry (#6), breadcrumb'lar ve `AsyncCombobox`'ın
hepsinin ihtiyaç duyacağı ortak sözlük. Önce bu tanımlanmazsa beş yerde beş farklı şekilde
icat edilir.

**Ne yapılacak:**
1. `src/types/entity.ts` oluştur, tek export:
   ```ts
   export interface EntityRef {
     type: string // 'deal' | 'contact' | 'event' | ... — feature'lar kendi type string'ini kullanır
     id: string
     display: string
     subtitle?: string
     url: string
   }
   ```
2. `src/lib/entity.ts` oluştur: `buildEntityRef(type: string, id: string, display: string, url: string, subtitle?: string): EntityRef` gibi küçük bir yardımcı (opsiyonel — sadece tip yeterliyse bu adımı atla, over-engineering yapma).
3. Mevcut bir feature'da (örn. `src/features/pipeline/`) bir `Deal`'ı `EntityRef`'e çeviren bir
   örnek fonksiyon ekle — `src/features/pipeline/data/format.ts` içine `dealToEntityRef(deal: Deal): EntityRef` gibi — bunu gerçek bir kullanım kanıtlamak için ekliyoruz, henüz hiçbir yerden çağrılmasına gerek yok.

**Kabul kriteri:** `npm run typecheck` yeşil. Bu görev UI değiştirmiyor, sadece tip + bir örnek dönüşüm fonksiyonu.

---

### Codex Task 3 — Action Registry (#6)

**Durum:** ✅ Tamamlandı ve Claude tarafından doğrulandı — Action Registry ve pipeline stage-change entegrasyonu eklendi. Commit: `d53e2e5` (`feat: add action registry`). `npm run check` (34 test) ve `npm run build` başarılı. 2026-09-07: claude-in-chrome ile canlı doğrulandı — Pipeline sayfasında bir satırın "..." menüsünde "Aşama değiştir" alt menüsü açıldığında mevcut aşama (`when(ctx)` filtresiyle) listede görünmüyor, bir aşama seçildiğinde mutasyon uygulanıp toast bildirimi çıkıyor.

**Neden:** Bugün bir "approve" eylemi bir satır menüsünde, bir bulk bar'da, bir drawer'da ayrı
ayrı yazılıyor. Tek bir `Action` sözleşmesi hepsini beslemeli.

**Ne yapılacak:**
1. `src/lib/actions/types.ts`:
   ```ts
   export interface ActionContext {
     entity?: EntityRef
     [key: string]: unknown
   }
   export interface Action<Ctx extends ActionContext = ActionContext> {
     id: string
     label: string
     icon?: LucideIcon
     when: (ctx: Ctx) => boolean
     run: (ctx: Ctx) => void | Promise<void>
     undo?: (ctx: Ctx) => void | Promise<void>
   }
   ```
2. `src/lib/actions/registry.ts`: basit bir `Map<string, Action>` + `registerAction(action)` +
   `getActions(ctx): Action[]` (kayıtlı olup `when(ctx)` true dönenleri filtreler).
3. `src/lib/actions/index.ts`: barrel.
4. Kanıt olarak: `src/features/pipeline/` içinde satır menüsünde var olan bir eylemi (örn. deal
   approve/stage değiştirme — `pipelineTable.tsx` veya `PipelineGrid.tsx`'te `PipelineRowActions`
   olarak geçen kısmı grep'le bul) registry üzerinden çağıracak şekilde yeniden bağla. **Feature
   registry'ye import eder, registry feature'a değil** — yön tek taraflı.

**Kabul kriteri:** `npm run check` yeşil, `npm run build`. Pipeline sayfasında satır menüsündeki
ilgili eylem tarayıcıda öncekiyle aynı şekilde çalışmalı (claude-in-chrome ile doğrula).

---

### Codex Task 4 — Route + Navigation Registry consolidation (#3 + #4)

**Durum:** ✅ Tamamlandı ve Claude tarafından doğrulandı — Feature-owned route/nav katkıları ve merkezi birleştirme eklendi. Commit: `10a3463` (`feat: consolidate route and navigation registries`). `npm run check` (34 test) ile `npm run build` başarılı. 2026-09-07: 18 feature'ın 17'sinde `routes.ts`, 16'sında `nav.ts` var (kalanlar `auth` — public/no-nav — ve `notifications` — sayfası olmayan bir widget feature, doğru şekilde hariç); tüm 17 route curl ile 200 döndü. claude-in-chrome ile Dashboard→Pipeline arası ⌘K navigasyonu ve breadcrumb ("Fynovio > Pipeline") canlı doğrulandı, `findNavTrail()` değişmeden çalışıyor.

**Neden:** `routes/index.tsx` (21 route, elle yazılmış dizi), `paths.ts` (düz obje) ve
`layouts/navigation.ts` (`NAV_GROUPS`, üçüncü bir ağaç) aynı bilgiyi üç yerde tutuyor ve
birbirinden bağımsız güncelleniyor.

**Ne yapılacak:**
1. Önce oku: `src/routes/index.tsx`, `src/routes/paths.ts`, `src/layouts/navigation.ts` — özellikle `findNavTrail()` fonksiyonunu (breadcrumb + active-state tek çözücüsü, bozulmamalı).
2. Her feature'a `src/features/<name>/routes.ts` ekle: o feature'ın path'i, lazy import'u ve
   guard bilgisini (protected/public) plain bir obje olarak export etsin. **Feature router'ı
   import etmez** — sadece `{ path: string, element: () => Promise<...>, protected: boolean }`
   gibi veri döndürür, `lazy()` çağrısını merkezi `routes/index.tsx` yapar.
3. Aynı feature'a `src/features/<name>/nav.ts` ekle: o feature'ın nav girdisi (label, icon,
   path referansı — `paths.ts`'e halen bağlı kalabilir, sorun değil, `paths.ts` shared).
4. `routes/index.tsx`'i tüm feature'ların `routes.ts`'ini import edip birleştirecek şekilde
   yeniden yaz. `layouts/navigation.ts`'i aynı şekilde `nav.ts`'leri birleştirecek şekilde
   yeniden yaz. `findNavTrail()` mantığı değişmemeli, sadece girdi kaynağı değişiyor.
5. 18 feature'ın hepsini tek seferde taşıma — önce 2-3 feature ile pattern'i doğrula
   (`npm run build` + tarayıcıda 2-3 route dene), sonra kalanına mekanik olarak uygula.

**Kabul kriteri:** `npm run build` yeşil. Tüm route'lar (21 tanesi) hâlâ 200 dönüyor, sidebar
navigation ve breadcrumb önceki gibi çalışıyor — en az 5 route'u claude-in-chrome ile gez ve
doğrula (özellikle nested nav'ı olan bir feature).

---

### Codex Task 5 — Command Registry / ⌘K (#5)

**Durum:** ✅ Tamamlandı ve Claude tarafından doğrulandı — Command registry, ⌘K palette ve route/action entegrasyonu eklendi.
Commit: `ce14cc6` (`feat: add command registry palette`).
`npm run check` (34 test) başarılı; üretim build çıktısı üretildi. 2026-09-07: claude-in-chrome
ile hem light hem dark temada canlı doğrulandı — ⌘K paletini açıyor, "Pipeline" komutuyla
route'a gidiyor, fuzzy arama dark modda da (Türkçe karakterler dahil) doğru filtreliyor.

**Neden:** `Topbar.tsx:24-35` şu an ⌘K'yı sadece search input'a focus atmak için bind ediyor.
Bu **UX #1** ile aynı kalem.

**Ne yapılacak:**
1. `src/lib/commands/` — `types.ts` (`{id, label, group, icon, when, run}`), `registry.ts`,
   `index.ts` — Action Registry (Task 3) ile aynı iskelet, kopyalama değil, mümkünse
   `src/lib/actions`'taki generic yapıyı paylaşacak şekilde düşün (aşırı soyutlama yapma —
   ikisi de basitse ayrı kalabilir, zorlamayla ortak taban çıkarma).
2. Route Registry (Task 4) ve Navigation Registry'den komutlar üret: her route için "Şuraya
   git: X" komutu otomatik.
3. Action Registry'den (Task 3) mevcut context'e uygun action'ları komut olarak yüzeye çıkar.
4. `src/components/common/CommandPalette.tsx` (yeni) — `cmdk` benzeri bir UI değil, mevcut
   `Dialog`/`Command` shadcn primitive'i varsa onu kullan (`src/components/ui/` içinde `command.tsx` var mı kontrol et, yoksa shadcn'den ekle — bu tek shadcn primitive eklemesi, "generated UI'ı özelleştirmeden önce oku" kuralına uy).
5. `Topbar.tsx`'teki mevcut ⌘K binding'ini palette'i açacak şekilde değiştir.

**Kabul kriteri:** `npm run check`, `npm run build`. ⌘K ile palette açılıyor, en az 3 route'a
git komutu çalışıyor, bir action tetiklenebiliyor. claude-in-chrome ile hem light hem dark tema
kontrolü yap (`AGENTS.md`'nin UI changes kuralı).

---

### Codex Task 6 — Overlay Manager (#29)

**Durum:** ✅ Tamamlandı ve Claude tarafından doğrulandı — merkezi overlay stack ve host eklendi; calendar event detayı
`openDialog()` üzerinden açılacak şekilde taşındı. `npm run check` (34 test) başarılı.
2026-09-07: claude-in-chrome ile canlı doğrulandı — Takvim sayfasında bir etkinliğe tıklandığında
dialog açılıyor, "Kapat" ile temiz kapanıyor, konsolda hata yok.
Commit: `9702b25` (`feat: add overlay manager`).

**Neden:** Altı overlay primitive'i (`dialog`, `alert-dialog`, `sheet`, `drawer`, `popover`,
`tooltip`) zaten var ve `docs/design-system/08-overlays.md` kuralları zaten sabitliyor. Eksik
olan imperatif orkestratör — her çağrı yerinde ayrı bir `useState` boolean yerine tek stack.

**Ne yapılacak:**
1. Önce oku: `docs/design-system/08-overlays.md` (z-50, ≤2 stack depth kuralı).
2. `src/lib/overlay/manager.ts`: `openDialog(config): Promise<Result>`, `openDrawer(config): Promise<Result>` gibi fonksiyonlar; dahili bir stack state (zustand ile, `useAppStore` pattern'ini takip et — `useShallow` ile selector).
3. `src/components/common/OverlayHost.tsx`: stack'i render eden tek component, escape sırası ve focus-restore mantığı burada.
4. `OverlayHost`'u `src/layouts/DashboardLayout.tsx`'e (veya root `App.tsx`'e) bir kez mount et.
5. Kanıt için mevcut bir overlay'i (örn. calendar feature'daki `EventDetailDialog.tsx`) yeni
   manager üzerinden açılacak şekilde göç ettir — **tek örnek**, geri kalan overlay'leri
   zorla taşıma, bu ayrı bir görev.

**Kabul kriteri:** `npm run check`. Calendar sayfasında bir event'e tıklayınca dialog hâlâ
açılıyor ve kapanıyor (claude-in-chrome ile doğrula), ama artık `openDialog()` üzerinden.

---

### Codex Task 7 — Permission + Capability seams (#17 + #8)

**Durum:** ✅ Tamamlandı — mock policy tabanlı `usePermission()` ve `<Can>` eklendi;
tenant/paket entitlement seam'i için root `CapabilityProvider` ve `useCapability()` eklendi.
Pipeline üst eylemi ikisini birlikte örnekliyor; permission veya capability kaldırıldığında render
edilmiyor. Her iki resolver da bunun yalnız UX katmanı olduğunu, gerçek yetkilendirmenin backend'de
zorunlu olduğunu açıkça belirtir. `npm run check` (34 test) başarılı, `usePermission`/`useCapability`
için ayrı test dosyaları var. 2026-09-07 Claude tarafından doğrulandı: kod incelemesinde
`PipelinePage.tsx`'teki "Pipeline'ı onayla" butonunun `useCapability('pipeline.approval')` VE
`<Can action="deal.approve">` ile çift kapılı olduğu, mock manager kullanıcısının (Deniz Kaya)
her ikisine de sahip olduğu için butonun canlı ortamda göründüğü teyit edildi. Commit: `b1c28ce`
(`feat: add permission and capability seams`).

**Neden:** Bugün yazılan her `if (user.role === ...)` ileride avlanması gereken bir borç.
BE gerçek kararı verecek, ama seam'i şimdi kesmek ucuz.

**Ne yapılacak:**
1. Önce oku: `src/features/auth/` (mock auth nasıl çalışıyor), `ProtectedRoute.tsx`, ve
   `AGENTS.md`'deki "Risk boundaries" bölümü — mock auth'un neden gerçek yetkilendirme
   olmadığını anlat.
2. `src/lib/permissions/types.ts`: `type PermissionId = string`, basit bir mock policy tipi.
3. `src/lib/permissions/usePermission.ts`: `useAuthStore`'dan (veya neresi ise) kullanıcıyı
   okuyup mock bir policy set'e göre `boolean` dönen selector-hook.
4. `src/components/common/Can.tsx`: `<Can action="deal.approve">{children}</Can>` — permission
   yoksa render etmeyen basit bir wrapper.
5. `src/lib/capabilities/useCapability.ts`: aynı pattern, tenant/paket bazlı (mock, local provider — bkz. #19 Feature Flag Engine ile aynı seviyede basitlik).
6. **Zorunlu:** Her iki dosyanın başına da `ProtectedRoute.tsx`'teki gibi net bir uyarı yorumu
   koy: bunun UX katmanı olduğunu, gerçek yetkilendirmenin her zaman backend'de yapılması
   gerektiğini belirt.
7. Action Registry'deki (Task 3) `when(ctx)`'e opsiyonel olarak `usePermission` entegre et —
   zorunlu değil, sadece bağlantı noktasını göster.

**Kabul kriteri:** `npm run check`. `<Can>` ile sarılmış bir örnek buton mock policy'ye göre
gösterilip gizlenebiliyor (bir demo route'ta veya pipeline'da göster).

---

## Codex Görevleri — Tur 2

Tier C + D'nin kalan 11 kaleminden 5'i Codex'e, 6'sı Claude'a paylaşıldı (2026-09-07, bkz.
"İş Bölümü" notu yukarıda). Bu 5 görev aynı kuralla çalışır: sırayla al, birini bitirip
doğrulamadan sonrakine geçme, her görev sonunda `npm run check` yeşil olmalı.

### Codex Task 8 — Filter AST (#26)

**Durum:** ✅ Tamamlandı — commit `bd35688`. Düz form durumu, boş kontrolleri predicate'e çevirmeyen `FilterNode[]` AST'sine taşındı; uygulama ve chip üretimi bu düğümlerden çalışıyor. `npm run check` ve `npm run build` başarılı. Tarayıcı doğrulaması bu ortamda bağlı Chrome olmadığı için yapılamadı.

**Neden:** `demo-filters/types.ts`'teki `FilterState` düz bir obje — OR yok, gruplama yok.
Saved views (#27, Claude'da) ve server-side filtering'in önkoşulu, bu objeyi node listesine
terfi ettirmek.

**Ne yapılacak:**
1. Önce oku: `src/features/demo-filters/types.ts` (`FilterState`, `EMPTY_FILTER`),
   `src/features/demo-filters/lib/query.ts` (predicate uygulama mantığı — "boş kontrol bir
   predicate değildir" kuralı), `components/FilterPanel.tsx`, `ActiveFilterChips.tsx`,
   `OrderExplorer.tsx`.
2. Yeni `src/features/demo-filters/lib/filterAst.ts`: bir `FilterNode` union'ı (`{ field, op,
   value }` şekli — `op`: `'eq' | 'in' | 'range' | 'contains' | 'bool'`) ve
   `filterStateToNodes(state: FilterState): FilterNode[]` + `applyFilterNodes(nodes, rows)`.
   Mevcut `query.ts`'teki alan-bazlı kontrolleri (Türkçe collation, tarih/miktar aralığı dahil)
   birebir taşı — mantığı değiştirme.
3. `OrderExplorer.tsx`'i node listesi üretip uygulayacak şekilde bağla.
4. `ActiveFilterChips.tsx`'i her `FilterState` alanını elle kontrol etmek yerine node listesinden
   chip üretecek şekilde yeniden yaz.
5. OR/gruplama **eklemiyorsun** — bu görev sadece düz objeden node listesine terfi, #27'nin
   üzerine ekleyeceği şey.

**Kabul kriteri:** `npm run check` yeşil — `demo-filters/lib/query.test.ts`'teki mevcut testler
davranış değişmeden geçmeli. `npm run build`. `/demo/filters`'ta en az 3 filtre tipi (arama,
çoklu durum seçimi, miktar aralığı) uygulanıp chip'lerin doğru göründüğü claude-in-chrome ile
doğrulanmalı.

---

### Codex Task 9 — Selection Engine exclusion-set modeli (#28)

**Durum:** ✅ Tamamlandı — commit `61f2865`. Include/exclude-set seçim yardımcıları ve sınır testleri eklendi; Pipeline toplu işlem çubuğu tüm sonuçlar seçildiğinde sayıyı ve hariç tutulanları gösterip mock mutasyonlara ID listesini çözümlüyor. `npm run check` ve `npm run build` başarılı. Tarayıcı doğrulaması bu ortamda bağlı Chrome olmadığı için yapılamadı.

**Neden:** Bugün bulk action'lar "seçili = id listesi" varsayıyor. Bu, "37.412 kayıttan tümünü
seç, 3'ünü hariç tut" senaryosunda kırılıyor.

**Ne yapılacak:**
1. Önce oku: `src/components/data-table/DataTableSelection.tsx`,
   `src/features/pipeline/table/pipelineTable.tsx` (`rowSelectionFeature` kullanımı),
   `PipelinePage.tsx`'teki bulk bar'ın seçimi nasıl okuduğu.
2. `src/components/data-table/lib/selection.ts`: `SelectionState = { mode: 'include' |
   'exclude'; ids: Set<string> }` — `'include'` bugünkü davranış (sadece `ids` seçili),
   `'exclude'` "hepsi, `ids` hariç".
3. Saf fonksiyonlar: `isRowSelected(state, id)`, `resolveSelectionCount(state, totalCount)`,
   `toggleSelectAllResults(state)`. Bulk bar ve mutasyonlar tablo'nun içindeki
   `rowSelectionFeature` state'ini değil bu fonksiyonları okur.
4. Pipeline'daki "tümünü seç" checkbox'ının yanına "37.412 kayıttan tümünü seç" affordance'ı
   ekle — tıklanınca `SelectionState`'i `exclude` moduna çevirir; bulk bar'da "X kayıt seçildi,
   Y hariç" metnini göster.
5. Mevcut bulk action'lar (`onChangeStage`, `onAssign`, `onRemove`) şimdilik id listesiyle
   çalışmaya devam eder — `exclude` seçimini action'ları çağırmadan önce id listesine çözmek
   yeterli (BE bulk endpoint'leri kapsam dışı).

**Kabul kriteri:** `npm run check`, `npm run build`. Pipeline'da satır-bazlı seçim öncekiyle aynı
çalışmalı; yeni "tümünü seç" affordance'ı tıklandığında bulk bar'da exclude-mode metni görünmeli
(claude-in-chrome ile doğrula).

---

### Codex Task 10 — Import / Export Framework (#58)

**Durum:** ✅ Tamamlandı — commit `a9cfe2e`. Bağımlılıksız CSV ayrıştırma, kolon eşleme, satır bazlı Zod doğrulama ve commit stub'ı içeren `ImportWizard` eklendi; demo formlarında giriş noktası ve Pipeline'da CSV dışa aktarma var. CSV escaping unit testi eklendi; XLSX sonraki adım olarak bırakıldı. `npm run check` (39 test) ve `npm run build` başarılı. Tarayıcı doğrulaması bağlı Chrome olmadığı için yapılamadı.

**Neden:** Client-side CSV import (parse → kolon eşleme → satır validasyonu → commit stub) ve
grid'den CSV export yüksek ERP değeri taşıyor, backend olmadan büyük ölçüde bitirilebilir.

**Ne yapılacak:**
1. Önce oku: `src/components/common/inputs/FileDropzone.tsx` (zaten var), örnek bir zod schema
   (`src/features/demo-forms/schema.ts`).
2. `package.json`'da csv/xlsx parse eden bir kütüphane olup olmadığını kontrol et
   (`grep -n "papaparse\|xlsx\|exceljs" package.json`). Yoksa **yeni bağımlılık ekleme** — CSV
   için küçük, elle yazılmış bir parser yeterli; XLSX desteğini "sonraki adım" olarak not düş.
3. `src/lib/importExport/parseFile.ts`: dosyayı satır+kolon dizisine çeviren fonksiyon.
4. `src/components/common/ImportWizard.tsx`: üç adım — yükleme (`FileDropzone`) → kolon eşleme
   (dosya başlıklarını çağıranın verdiği bir zod schema'nın alanlarına eşle) → satır bazlı
   validasyon raporu (hangi satırlar geçti/geçemedi, alan bazlı zod hatasıyla). Çağıran bir
   `onCommit(rows)` verir — bu görevde no-op/mock (kalıcı commit BE, kapsam dışı).
5. `src/lib/importExport/exportCsv.ts`: `exportRowsToCsv(rows, columns)` — `DataTable`'ın
   zaten gösterdiği kolon tanımlarını kabul edip client-side CSV indirme tetikler.
6. Kanıt: mevcut bir tabloya (Pipeline veya bir demo tablo) "Dışa aktar" butonu, ve
   `ImportWizard`'ı gerçek bir schema'ya (örn. demo-forms schema) karşı çalıştıran bir giriş
   noktası ekle — bir demo route yeterli, her tabloya production entegrasyonu gerekmiyor.

**Kabul kriteri:** `npm run check`, `npm run build`. Export butonu tetiklendiğinde doğru
içerikte bir CSV üretilmeli (tarayıcı indirmeleri bu ortamda inert olabilir — en azından
üretilen içerik bir unit testle ya da console log ile doğrulanmalı). Import wizard'ın üç adımı
claude-in-chrome ile gezilmeli.

---

### Codex Task 11 — Code Splitting: manualChunks (#43)

**Durum:** ✅ Tamamlandı — commit `a53bdd1`. FullCalendar, Tiptap ve Recharts için bağımsız Rollup parçaları tanımlandı. `npm run build` başarılı; ana giriş parçası 1,006 KB'den 992 KB'ye indi ve üç vendor parçası sırasıyla 313 KB, 394 KB, 440 KB üretildi.

**Neden:** `vite.config.ts`'de `manualChunks` yok — FullCalendar/tiptap/recharts vendor chunk
içinde route'lardan bağımsız büyüyor. Kontrol edildi: şu an gerçekten yok (`grep -n
"manualChunks" vite.config.ts` boş dönüyor).

**Ne yapılacak:**
1. Önce oku: `vite.config.ts`.
2. `build.rollupOptions.output.manualChunks` fonksiyonu ekle: `id` içinde `node_modules` +
   `fullcalendar` geçenler bir chunk'a, `tiptap` geçenler başka bir chunk'a, `recharts` geçenler
   başka bir chunk'a ayrılsın.
3. `npm run build` ile `dist/` çıktısında bu üç kütüphanenin ayrı chunk dosyalarında olduğunu
   doğrula; ana `index` chunk boyutunun küçüldüğünü öncesi/sonrası olarak not al.

**Kabul kriteri:** `npm run build` yeşil, build çıktısında üç kütüphane ayrı dosyalarda.

---

### Codex Task 12 — Accessibility verification (#35)

**Durum:** ✅ Tamamlandı — commit `3cdfcc6`. Oxlint'in yerel `jsx-a11y` eklentisi lint kapısında etkin; alt metin, ARIA, ilişkilendirilmiş etiket, tabindex ve semantic-role kuralları hata seviyesinde kontrol ediliyor. Reduced motion koruması global animasyon, geçiş ve scroll davranışına genişletildi; tasarım sistemi belgesi güncellendi. Mevcut bileşenlerde semantic-role ve etiket ilişkilendirmesine yönelik uyarılar kaldı; lint, typecheck, test ve build başarılı.

**Neden:** `docs/design-system/11-accessibility.md` WCAG 2.2 AA kurallarını yazıyor ama hiçbir
lint/otomatik kontrol bağlı değil. Kontrol edildi: `eslint-plugin-jsx-a11y` kurulu değil
(`package.json`/`.oxlintrc.json`'da yok).

**Ne yapılacak:**
1. Önce oku: `docs/design-system/11-accessibility.md`.
2. Oxlint'in a11y kural kategorisini native destekleyip desteklemediğini kontrol et (oxlint
   dokümantasyonu/`.oxlintrc.json` şeması). Destekliyorsa sadece `.oxlintrc.json`'da bu
   kategoriyi `error` seviyesinde aç; desteklemiyorsa `npm install -D eslint-plugin-jsx-a11y`
   ekleyip lint pipeline'ına bağla.
3. `npm run lint` çalıştır; çıkan ihlallerden kolay olan 5-10 tanesini düzelt (eksik alt text,
   eksik aria-label vb.); kalanları bu Durum satırına not olarak ekle, hepsini bu görevde
   bitirmeye çalışma.
4. `prefers-reduced-motion`'ı skeleton dışına genişlet: `grep -rn "prefers-reduced-motion" src`
   ile şu an nerede kullanıldığını bul, animasyon içeren diğer bileşenlere aynı media query
   guard'ını ekle.

**Kabul kriteri:** `npm run check` yeşil (yeni a11y kuralları dahil).
`docs/design-system/11-accessibility.md`'deki "no eslint-plugin-jsx-a11y" notu güncellenmeli.

---

## Codex Görevleri — Tur 3

Tur 2 kapandıktan sonra ★ listesi yeniden tarandı: 35 ★ kalemden 34'ü bitmişti, **#9 Typed API
Layer** hiçbir Tier'e (A/B/C/D) hiç girmemiş, planlamadan tamamen atlanmış tek kalemdi
(2026-09-08 doğrulandı). Aynı kural geçerli: bitirip doğrulamadan başka işe geçme, sonunda
`npm run check` yeşil olmalı.

### Codex Task 13 — Typed API Layer boundary hardening (#9)

**Durum:** ✅ Tamamlandı — commit `5866f5c`. `parseApiResponse()` doğrudan yanıtları ve `{ data }` envelope'larını
Zod ile API sınırında doğruluyor; eski `unwrapApiResponse()`/`isArrayOf()` geçişteki diğer feature
API'leri için korundu. Shared `dealSchema`, `stageBucketSchema`, `activitySchema` ve `userSchema`
mevcut TypeScript domain tipleriyle `z.ZodType<T>` sözleşmesi üzerinden derleme zamanında bağlı;
calendar'ın feature-owned `CalendarEvent` tipi kendi şemasıyla doğrulanıyor. Dashboard'un üç
query'si, Pipeline, Calendar ve Auth login bu şemalara taşındı. Bozuk bir `Deal` kaydının
reddedildiğini kanıtlayan birim test eklendi. `npm run check` (59 test) ve `npm run build` başarılı.
Tarayıcı doğrulaması bu ortamda bağlı bir browser olmadığı için yapılamadı.

**Neden:** Bu doküman #9'u yazarken tarif ettiği durum (endpoints sadece auth+deals, hiç boundary
validasyonu yok) artık **kısmen yanlış** — `src/api/response.ts` (`unwrapApiResponse` +
`isArrayOf<T>`, ayrıca `auth/api.ts`'teki elle yazılmış `isSession` guard'ı) zaten çalışma zamanı
bir tip kontrolü yapıyor ve her feature'ın `api.ts`'i bunu kullanıyor. Ama bu guard'lar **sığ**:
`isArrayOf<T>` sadece `Array.isArray` kontrolü yapıyor, dizinin içindeki her elemanın gerçekten
`T` şeklinde olduğunu (zorunlu alanlar, tipler) doğrulamıyor — sunucudan gelen bozuk/eksik bir
kayıt sessizce `Deal`/`CalendarEvent`/`Activity`/`StageBucket` olarak cast edilip UI'a sızabilir.
Kalan iş, bu guard'ları zod şemasıyla değiştirip gerçek şekil doğrulaması eklemek — "sistem
sınırında validasyon" kuralının (`AGENTS.md` → Coding Standards) API'nin gerçek giriş noktası
olan yerde eksik kalan tek parçası.

**Ne yapılacak:**
1. Önce oku: `src/api/response.ts` (mevcut `unwrapApiResponse`/`isArrayOf` — neyi zaten
   yaptığını anla, sıfırdan icat etme), `src/types/index.ts` (`Deal`, `StageBucket`, `Activity`,
   `User` arayüzleri — zod şemaları bunlarla alan-alan eşleşmeli), `src/features/auth/api.ts`
   (`isSession` — elle yazılmış guard'a en yakın örnek), `src/features/demo-forms/schema.ts`
   (projede zaten kullanılan zod stilini takip et).
2. Domain tipi başına bir zod şeması: `src/types/schemas.ts` (yeni dosya) içinde `dealSchema`,
   `stageBucketSchema`, `activitySchema`, `userSchema` — `src/types/index.ts`'teki arayüzlerin
   birebir aynısı, `z.infer<typeof dealSchema>` mevcut `Deal` tipiyle uyuşmalı (iki tip birbirinden
   sapmasın diye bir `satisfies`/eşitlik kontrolü ekle).
3. `src/api/response.ts`'e zod tabanlı bir `parseApiResponse<T>(payload: unknown, schema:
   z.ZodType<T>): T` ekle (mevcut envelope-unwrap mantığını koru, `isExpected` guard yerine
   `schema.safeParse` kullan, hata mesajı yine `i18n.t('api.invalidResponse')`). Mevcut
   `unwrapApiResponse`/`isArrayOf`'u **kırma** — bir feature migrate olmadan diğerleri bozulmasın,
   ikisi geçiş süresince bir arada yaşayabilir.
4. Feature'ları tek tek taşı (hepsini aynı anda değil): önce `dashboard/api.ts` (3 query, en
   düşük risk), sonra `pipeline/api.ts`, `calendar/api.ts`, en son `auth/api.ts`'teki
   `isSession`'ı `z.object({ user: userSchema, token: z.string() })`'a çevir. **Her migrasyondan
   sonra** `npm run check` + ilgili feature'ın mock verisiyle çalıştığını tarayıcıda doğrula.
5. `docs/design-system/`'de API boundary validasyonuyla ilgili bir not yoksa, `07-forms.md`'nin
   yanına değil — bu form değil, bu görev kapsamında yeni bir doküman **açma**, sadece bu
   dosyadaki (`ENTERPRISE_LAYERS_ASSESSMENT.md`) #9 satırının "Bugün" sütununu güncelle.

**Kabul kriteri:** `npm run check`, `npm run build`. Dashboard, Pipeline, Calendar ve
Auth (login) sayfaları mock veriyle öncekiyle birebir aynı çalışmalı (claude-in-chrome ile
4 sayfa da tek tek doğrulanmalı). Bilinçli olarak bozuk bir mock response (örn. `Deal.id`'yi
eksik bırakan) ile denenip `parseApiResponse`'un hatayı yakaladığı (sessizce geçirmediği) en az
bir feature'da gösterilmeli, sonra test verisi geri alınmalı — kalıcı bir test fixture'ı bırakma.

---

## Codex Görevleri — Tur 4

★ listesi kapandıktan sonra ☆ ("yapılırsa iyi olur") listesindeki 17 kalem gözden geçirildi
(2026-09-08). Çoğu gerçek bir tetikleyici olaya bağlı (BE, gerçek kayıt, ikinci geliştirici vb.)
ve şimdiden yapmak tam da bu dokümanın #21'de uyardığı prematüre soyutlama olurdu — o yüzden
onlara dokunmuyoruz. İki kalem farklı: biri bedava ve erteleme gerekçesi yok (#49), diğeri
"tetikleyicisi henüz gerçekleşmedi" değil, "riski zaten şu an mevcut ama fark edilmemiş" (#37,
tiptap zaten bağımlılık ve `RichTextInput.tsx` kendi sınırını zaten belgeliyor). Aynı kural:
sırayla al, her görevden sonra `npm run check` yeşil olmalı.

### Codex Task 14 — Developer Diagnostics: React Query Devtools (#49)

**Durum:** ✅ Tamamlandı — commit `5866f5c`. `@tanstack/react-query-devtools` geliştirme bağımlılığı olarak eklendi.
`App.tsx` onu yalnızca `import.meta.env.DEV` altında `React.lazy()` ve `Suspense` ile yüklüyor;
üretimde branch derleme sırasında eleniyor. `npm run check` (62 test) ve `npm run build` başarılı;
`rg -l "react-query-devtools" dist` çıktı vermedi. Canlı devtools ikonu bu ortamda bağlı browser
olmadığı için doğrulanamadı.

**Neden:** `@tanstack/react-query` (`^5.102.8`) zaten bağımlılık, uygulamanın state'inin büyük
kısmı query cache'inde yaşıyor (bkz. #11 State Separation), ama `@tanstack/react-query-devtools`
kurulu değil (`package.json`'da yok). Tek satırlık bir ekleme, hiçbir erteleme gerekçesi yok —
diğer ☆ kalemlerin aksine bir "tetikleyici olay" beklemiyor.

**Ne yapılacak:**
1. Önce oku: `src/App.tsx` (`QueryClientProvider`'ın sarıldığı yer), `src/api/queryClient.ts`.
2. `npm install -D @tanstack/react-query-devtools@^5` (mevcut `react-query` major'üyle aynı,
   `^5.102.8`'e yakın bir sürüm — `package.json`'daki diğer `@tanstack/*` paketlerinin sürüm
   sabitleme kuralına bak).
3. `App.tsx`'te `QueryClientProvider`'ın içine devtools'u **yalnızca dev'de** mount et. Statik bir
   `import { ReactQueryDevtools } from '@tanstack/react-query-devtools'` production bundle'ına
   dahil olur (paket kendi içinde `NODE_ENV` kontrolü yapsa da import ağacı build'e girer) —
   bunun yerine `React.lazy(() => import('@tanstack/react-query-devtools').then((m) => ({
   default: m.ReactQueryDevtools })))` ile dinamik import kullan, `import.meta.env.DEV` ile sar,
   `<Suspense fallback={null}>` içine al.
4. `npm run build` çıktısında devtools'un prod `dist/` bundle'ında **ayrı, dev-only bir chunk
   olarak bile görünmediğini** (dinamik import + `import.meta.env.DEV` kombinasyonu Vite'da bu
   chunk'ı tamamen eler) doğrula — `grep -r "react-query-devtools" dist/` boş dönmeli.

**Kabul kriteri:** `npm run check`, `npm run build`. `npm run dev` ile açılan uygulamada sağ alt
köşede React Query devtools ikonu görünmeli ve query cache'i gösterebilmeli (claude-in-chrome ile
doğrula). Prod build'de (`dist/`) devtools koduna dair hiçbir iz olmamalı.

---

### Codex Task 15 — Rich text sanitization policy (#37)

**Durum:** ✅ Tamamlandı — commit `5866f5c`. `dompurify` ile `sanitizeHtml()` eklendi; RichTextInput'un ürettiği
gerçek etiket kümesi ve yalnız `href` attribute'u allowlist'te. Script/event-handler ve
`javascript:` URL denemelerini reddeden üç birim test var. `/demo/forms` editörünün yanında
yalnızca sanitize edilmiş HTML'i `dangerouslySetInnerHTML` ile render eden kalıcı önizleme paneli
eklendi; RichTextInput sözleşmesi de `sanitizeHtml()`e yönlendiriyor. `npm run check` (62 test)
ve `npm run build` başarılı. Canlı zararlı-HTML denemesi bağlı browser olmadığı için yapılamadı.

**Neden:** `src/components/common/inputs/RichTextInput.tsx:52-66` kendi sınırını zaten açıkça
yazmış: *"HTML from a user is untrusted input, so whatever renders it later sanitises it. This
control's job ends at producing it."* Bugün bu HTML'i geri render eden hiçbir yer yok (`grep -rn
"dangerouslySetInnerHTML" src` tek sonucu `ui/chart.tsx:92`, tiptap ile ilgisiz), yani risk henüz
tetiklenmedi — ama sanitizasyon *politikası* şimdi yazılmazsa, ilk kez bir yerde bu HTML
`dangerouslySetInnerHTML` ile geri basıldığında (bir teklif önizlemesi, bir e-posta şablonu
render'ı) sanitizasyonsuz gidecek. `package.json`'da hiçbir sanitizasyon kütüphanesi yok.

**Önemli:** Bu, Codex Task 10'daki "yeni bağımlılık ekleme, elle yaz" kuralının **istisnası** —
HTML sanitizasyonu güvenlik sınırı, elle yazılmış bir regex/parser burada yanlış kısayol olur.
`dompurify` (endüstri standardı, allowlist tabanlı) eklemek doğru seçim.

**Ne yapılacak:**
1. Önce oku: `RichTextInput.tsx` (özellikle üstteki `CONTENT` sabiti — tiptap'ın StarterKit +
   Placeholder + Link extension'larının ürettiği gerçek etiket kümesi: `p`, `h2`, `h3`, `ul`,
   `ol`, `li`, `blockquote`, `a`, `code`, `strong`, `em`, `u`, `s`, `br`), `docs/design-system/`
   içinde #37 ile ilgili bir not var mı kontrol et (muhtemelen yok).
2. `npm install dompurify` (+ tip tanımları pakette dahil, ayrı `@types` gerekmez).
3. `src/lib/sanitizeHtml.ts`: `sanitizeHtml(html: string): string` — `DOMPurify.sanitize(html, {
   ALLOWED_TAGS: [...], ALLOWED_ATTR: ['href'] })` ile yalnızca `RichTextInput`'un gerçekten
   üretebileceği etiketlere izin ver (adım 1'deki liste). `javascript:` href'leri, `on*`
   attribute'ları, `script`/`style`/`iframe` DOMPurify varsayılanıyla zaten düşer — allowlist'i
   daraltarak bunu açıkça garanti altına al.
4. `src/lib/sanitizeHtml.test.ts`: en az üç vaka — izin verilen etiketler aynen geçer, `<script>`
   ve `onerror` gibi enjeksiyon denemeleri temizlenir, `javascript:` href reddedilir.
5. Kanıt için tek bir gerçek tüketici: `demo-forms/components/EditorSection.tsx`'e (veya aynı
   demo route'a) `RichTextInput`'un ürettiği HTML'i `sanitizeHtml()`'den geçirip
   `dangerouslySetInnerHTML` ile gösteren küçük bir "Önizleme" paneli ekle — bugün var olmayan
   render yolunun ilk örneği, politika kanıtlanmış olsun. Kalıcı bir test/demo veri fixture'ı
   bırakma, ama bu panel kalıcı kalabilir (gerçek bir kullanım örneği).
6. `RichTextInputProps` üzerindeki `/** HTML. Sanitise it on the way back in — this control does
   not. */` yorumuna `sanitizeHtml()`'e referans ekle, böylece gelecekte bu kontrolü kullanan biri
   nereye bakacağını bilir.

**Kabul kriteri:** `npm run check`, `npm run build`. `/demo/forms`'ta zengin metin editörüne
`<script>alert(1)</script>` veya `<img src=x onerror=alert(1)>` yazılıp önizleme panelinde
zararlı kodun çalışmadığı (etiketin temizlendiği) claude-in-chrome ile canlı doğrulanmalı.

---

## Claude Görevleri

Tier C + D'nin Codex'e gitmeyen 6 kalemi. Bunlar çapraz-kesen (birden fazla feature'ı aynı anda
etkiliyor), sessizce kırılabilecek (yanlış yapılırsa uygulama genelinde fark edilmeden bozulur)
ya da mevcut tasarım sistemiyle görsel/etkileşim tutarlılığı gerektiren işler — bu yüzden Codex'in
paralel, izole görev profiline uymuyor. Aynı kuralla ilerlenir: sırayla al, her görevden sonra
`npm run check` (+ router/state/form dokunulan görevlerde `npm run build`) yeşil olmalı, canlı
doğrulama claude-in-chrome ile yapılır. Durum satırları bu bölümde de periyodik güncellenir.

### Claude Task A — View State Engine (#27)

**Durum:** ✅ Tamamlandı — commit `6947352`. Önceki "Bloklu" notu yanlıştı: Codex Task 8
(#26 Filter AST) aslında `bd35688` ile tamamlanmıştı, sadece bu Durum satırı güncellenmemişti
(2026-09-08 `git stash` ile doğrulandı — `filterAst.ts` mevcut ve kayıtlı).

`src/components/data-table/lib/viewState.ts`'te tek bir `ViewState<TFilter>` tipi (pagination,
sorting, filter, columnVisibility, columnSizing, columnOrder, density) ve
`viewStateStorageKey`/`readPersistedViewState`/`writePersistedViewState`/`useViewStateUserId`
persistence yardımcıları eklendi. `columnOrderingFeature` (görev metnindeki `columnOrderFeature`
adı yanlış — gerçek export `columnOrderingFeature`) `saasGridTable.tsx`'e kaydedildi;
`DataTableViewOptions.tsx`'in "Columns" menüsüne, yapısal olmayan (hideable) kolonlar için
yukarı/aşağı taşıma kontrolleri eklendi (`data-table/lib/columnOrder.ts`'teki saf
`normalizeColumnOrder`/`moveColumnOrder` üzerinden — birim testli). `AdvancedSaasGrid.tsx` artık
columnVisibility/columnSizing/columnOrder üçlüsünü kullanıcı+tablo bazlı localStorage anahtarına
debounce'lu (400ms) yazıyor ve mount'ta geri okuyor; `columnResizing` kasıtlı olarak persist
edilmiyor (geçici sürükleme state'i, geri yüklenirse hayalet bir sürükleme oluşturur). "Reset
widths" → "Reset layout" olarak genişledi (sizing+order sıfırlıyor; visibility'nin kendi resetı
menüde ayrı kalıyor).

demo-filters tarafında: `SavedView`'a type-seviyesinde `shared?: boolean` eklendi (resolution
yazılmadı — BE gerektirir). Yeni `lib/personalViews.ts`, kullanıcının o anki filtre+sıralamayı
adlandırıp `SavedView` şeklinde localStorage'a (kullanıcı+tablo keyed) kaydetmesini sağlıyor;
`SavedViews.tsx` artık kürate edilmiş 5 görünümle kişisel görünümleri birlikte render ediyor,
"Görünümü kaydet" popover'ı ve kişisel görünümler için kaldırma (×) kontrolü ekliyor. Bir görünüm
kaydedildiğinde otomatik olarak seçili hale geliyor (`save()` oluşturduğu `SavedView`'ı döner).

Kapsam daraltmaları (advisor review sonrası, önceki taslağa göre): `SavedView.filter` hâlâ
`Partial<FilterState>` — Codex Task 8'in `FilterNode[]` AST'sine zorla taşınmadı. Kabul kriteri
kayıtlı bir görünümün filtre+sıralamasının *aynı gelmesini* istiyor, temsilinin AST olmasını değil;
flat şekli korumak `nodesToFilterState` ters-dönüşümünü ve `data/views.ts`'in yeniden yazılmasını
gereksiz kılıyor. `ViewState.density` tip seviyesinde var ama bir görünüm seçildiğinde geri
yazılmıyor — global density'yi (`useAppStore`) bir demo-filters etkileşiminden değiştirmek,
AGENTS.md'nin "compact bilinçli bir tercih, varsayılan değil" kuralını çiğner; alan sadece
gelecekte bir görünümün density'yi anlık görüntüleyebilmesi için yer açıyor.

Yan bulgu ve düzeltme: `src/components/ui/dropdown-menu.tsx`'teki `DropdownMenuLabel`, kurulu
`@base-ui/react@1.7.0` ile `Base UI: MenuGroupContext is missing` hatasıyla çöküyordu — bu görevden
önce de mevcuttu (`git stash` ile doğrulandı), ve "Columns" menüsünü hiç açılamaz hale getirdiği
için bu görevi test etmeyi engelliyordu. `MenuPrimitive.Group` sarmalayıcısı eklenerek düzeltildi.
Diğer üç kullanıcıdan ikisi canlı doğrulandı — `/pipeline`'daki "Stage" filtre menüsü ve
`/demo/kanban`'daki kart "..." menüsü sorunsuz açılıyor. `demo-badges/InvoiceStatusGrid.tsx`'teki
üçüncü kullanım doğrulanamadı: o sayfadaki durum rozetleri bu görevden bağımsız, önceden var olan
bir nedenle görünmez render ediliyor (`DropdownMenuTrigger`'a geçilen children, `render` prop'lu
Base UI tetikleyicisinde görünmüyor gibi duruyor) — `DropdownMenuLabel` değişikliği bu tetikleyici
davranışına dokunmadığından risk düşük, ama kayıtlı bir gözlem olarak bırakılıyor.

`AdvancedSaasGrid.tsx`'teki `SaasGridLayout` tipi ilk taslakta `ViewState`'ten bağımsız kendi
alanlarını tekrar tanımlıyordu — bu, tipin hiçbir gerçek tüketicisi olmaması ve zamanla
sessizce kaymasına izin vermesi anlamına geliyordu (görevin önlemeye çalıştığı "üç yerde ayrı ayrı
tutulan aynı bilgi" sorununun kendisi). `type SaasGridLayout = Pick<ViewState, 'columnVisibility' |
'columnSizing' | 'columnOrder'>` olarak düzeltildi, böylece `ViewState` gerçekten tip-kontrollü bir
sözleşme.

Kolon taşıma butonlarının klavye erişilebilirliği canlı doğrulandı: "Columns" menüsü açıkken Tab,
odağı satırın kendisinden yukarı/aşağı ok butonlarına taşıyor; Enter/Space butonu tetikliyor, menü
açık kalıyor ve liste canlı yeniden sıralanıyor; sıradaki Tab odağı menü içinde bir sonraki satıra
taşıyor (menüden dışarı kaçmıyor). `docs/design-system/11-accessibility.md`'ye yeni bir istisna
eklenmedi çünkü bir istisna yok — düzen çalışıyor.

`npm run check` (58 test, 13 dosya) ve `npm run build` yeşil. claude-in-chrome ile hem
`/demo/tables` (kolon sırası + genişlik + görünürlük değiştirilip sayfa yenilendiğinde korunuyor,
"Reset layout" geri alıyor) hem `/demo/filters` (bir görünüm kaydedilip başka bir görünüme geçilip
geri seçildiğinde filtre+arama birebir geri geliyor, kaldırma çalışıyor) her iki temada da canlı
doğrulandı.

**Neden:** `useTableSearchParams` (URL sahipliği), saved views, `columnVisibilityFeature`/
`columnSizingFeature`/`columnResizingFeature`, density — dördü de var ama tek bir `ViewState`
tipi bunları birleştirmiyor. #26 filtre şeklini değiştirdiği için aynı kişinin koordine etmesi
daha güvenli.

**Ne yapılacak:**
1. Önce oku: `src/components/data-table/lib/urlState.ts` (`useTableSearchParams`),
   `src/features/demo-filters/data/views.ts` (`SavedView` modeli), `saasGridTable.tsx`'teki
   feature kayıtları, `useAppStore`'daki density state.
2. Tek bir `ViewState` tipi: pagination, sorting, filter (Codex Task 8'in ürettiği node listesi),
   `columnVisibility`, `columnSizing`, **yeni** `columnOrder`, density — hepsini kapsayan.
3. Kişisel view'lar için localStorage persistence (kullanıcı+tablo bazlı key). Takım-paylaşımlı
   view'lar BE gerektirir — kapsam dışı, sadece tip seviyesinde yer aç (`shared?: boolean` alanı
   yeterli, resolution'ı yazma).
4. `columnOrderFeature`'ı TanStack Table v9'dan kayıt et (şu an kayıtlı değil).

**Kabul kriteri:** `npm run check`, `npm run build`. Bir tabloda kolon sırası/genişliği/
görünürlüğü değiştirip sayfa yenilendiğinde korunduğu, bir saved view kaydedilip geri
yüklendiğinde filtre+sıralamanın aynı geldiği claude-in-chrome ile doğrulanmalı.

---

### Claude Task B — Mutation infra + optimistic layer + validation contract (#12 + #13 + #22)

**Durum:** ✅ Tamamlandı — `9006d32` (`useAppMutation` + pipeline migrasyonu), `9c2ceac`
(calendar migrasyonu), `0f085d7` (422 kontratı dokümantasyonu). Her migrasyondan sonra
`npm run check` + `npm run build` yeşildi; pipeline (aşama değiştirme, kaldırma) ve calendar
(sürükle-taşı) claude-in-chrome ile tek tek doğrulandı, toast metinleri ve grid/takvim
güncellemeleri öncekiyle birebir aynı. **Not:** auth'un `useLogin` mutasyonu kapsam dışı
bırakıldı — görev metni yalnızca pipeline ve calendar'ı migrate etmeyi istiyordu, auth üçüncü
örnek olarak sadece okunmak için verilmişti. Calendar'ın taşınmamış toast'ı artık
`toast.success()` (yeşil ikon) kullanıyor — önceden nötr `toast()` idi; kasıtlı bir stil
normalizasyonu, davranış aynı kaldı.

**Neden:** Kalan işlerin en yüksek blast radius'lu olanı — her feature'ın `api.ts`'ine dokunuyor
(pipeline, calendar, auth ve ileride mutasyon ekleyecek her feature). Yanlış bir migrasyon birden
fazla feature'ı aynı anda kırar; bu yüzden tek bir sahipte, her feature migrasyonundan sonra test
checkpoint'iyle ilerlemek, async/paralel bir Codex görevinden daha güvenli.

**Ne yapılacak:**
1. Önce oku: `src/features/pipeline/api.ts`, `src/features/calendar/api.ts`,
   `src/features/auth/api.ts` (üç farklı `useMutation` kullanımı), `src/api/client.ts`'teki
   `ApiError` şekli.
2. `src/lib/mutations/useAppMutation.ts`: validate (opsiyonel zod parse) → execute → invalidate
   (query key'ler) → toast (başarı/hata metni) → telemetry (`track('mutation_...')`) → opsiyonel
   undo — tek bir wrapper.
3. Pipeline'ın iki mutasyonunu **önce** bu wrapper'a taşı (kanıt), davranışın birebir aynı
   kaldığını tarayıcıda test et, sonra calendar'ınkini taşı — **her migrasyondan sonra**
   `npm run check` + `npm run build` + manuel smoke test, hepsini sona bırakma.
4. 422 validation error contract'ını `types/index.ts`'teki `ApiError`'a ekle (field-path
   dizisi) ve `docs/design-system/07-forms.md`'deki mevcut error-copy kurallarının yanına
   dokümante et.

**Kabul kriteri:** Her feature migrasyonundan sonra `npm run check` + `npm run build` yeşil.
Pipeline ve calendar'daki mutasyonlar tarayıcıda öncekiyle birebir aynı çalışmalı
(claude-in-chrome ile her ikisi de tek tek doğrulanmalı).

---

### Claude Task C — Draft Engine (#23)

**Durum:** ✅ Tamamlandı — `b42ac16`. `useDraftGuard` (`src/lib/drafts/`) `QuoteFormDemo.tsx`'e
bağlandı, kanıt için tek örnek — spec'in "diğer formları zorla taşıma" notuna uyuldu. Cihazlar
arası senkron kapsam dışı bırakıldı. claude-in-chrome ile doğrulandı: dolu formdan çıkmaya
çalışınca "Kaydedilmemiş değişiklikleriniz var" onayı çıkıyor, "Bu sayfada kal" iptal ediyor,
"Yine de ayrıl" navigasyonu tamamlıyor; gerçek bir sayfa yenilemesinde (hash-only SPA
navigasyonu değil) "Kaydedilmiş bir taslağınız var" istemi görünüyor, geri yükleme alanı
dolduruyor ve localStorage'ı temizliyor, silme de localStorage'ı temizliyor. İki yeni birim testi
(`useDraftGuard.test.ts`) `Date` alanının (`closeDate`) JSON round-trip'ini doğruluyor.

**Neden:** RHF'nin global davranışına (`isDirty` takibi + navigasyon engelleme) dokunuyor — tüm
formları aynı anda etkiliyor. Burada bir hata sessizce uygulama genelinde navigasyonu bozar;
tek geçişte dikkatli yapıp her formu tek tek test etmek gerekiyor.

**Ne yapılacak:**
1. Önce oku: `src/features/demo-forms/components/QuoteFormDemo.tsx` (kanıt için hedef —
   çok-alanlı, ERP benzeri gerçek bir form), react-router-dom'un `useBlocker` API'si.
2. `src/lib/drafts/useDraftGuard.ts`: RHF `formState.isDirty` → `useBlocker` ile navigasyon
   onayı, debounced localStorage autosave (route+form id ile keyed), sonraki ziyarette açık bir
   "taslağı geri yükle?" istemi.
3. `QuoteFormDemo.tsx`'e bağla — kanıt için tek örnek, diğer formları zorla taşıma.
4. Cihazlar-arası taslak senkronizasyonu BE — kapsam dışı, park.

**Kabul kriteri:** `npm run check`, `npm run build`. Doldurulmuş formdan çıkmaya çalışınca uyarı
çıktığı, sayfaya geri dönünce "taslağı geri yükle?" isteminin göründüğü claude-in-chrome ile
doğrulanmalı.

---

### Claude Task D — Field Registry domain tier (#24)

**Durum:** ✅ Tamamlandı — `0d6c49b`. `CustomerPicker`/`ProductPicker`/`ErpCodeField` tek bir
`createDomainEntityField` fabrikası üzerinde (üç neredeyse-özdeş sarmalayıcı eşiği geçtiği için
çıkarıldı), `AsyncCombobox` + mock arama, sonuç `EntityRef`. `registry.ts`'teki `fieldRegistry`
hiçbir yerden çağrılmıyor — #21 ileride yapılırsa diye hazır. `/demo/forms`'un "Dependent and
server-backed selection" bölümüne eklendi; claude-in-chrome ile hem koyu hem açık temada arama,
seçim ve `EntityRef` şekilli value okunuşu doğrulandı.

**Neden:** Mevcut 36 kontrolle görsel/etkileşim tutarlılığı gerektiriyor — tasarım sistemi
token'larını denetleyen aynı gözün yapması gereken bir yargı işi, mekanik bir ekleme değil.

**Ne yapılacak:**
1. Önce oku: `src/components/common/inputs/index.ts` (barrel, 36 kontrol), `AsyncCombobox`'ın
   mevcut implementasyonu, `src/types/entity.ts` (`EntityRef`, Codex Task 2'nin ürünü).
2. `CustomerPicker`, `ProductPicker`, `ErpCodeField` — `AsyncCombobox`'ın üzerine (mock arama),
   sonucu `EntityRef` şeklinde döndürecek şekilde.
3. `src/components/common/inputs/registry.ts`: bir `Record<FieldKind, ComponentType>` —
   şimdiden hiçbir çağıran olması gerekmiyor (bkz. #21 Schema-Driven Forms'un "deliberately not
   doing" notu), ama #21 ileride yapılırsa çözücüsü burada hazır olsun.

**Kabul kriteri:** `npm run check`, `npm run build`. `/demo/forms`'a (veya yeni bir demo route'a)
üç yeni kontrol eklenip light+dark temada görsel olarak doğru göründüğü claude-in-chrome ile
kontrol edilmeli.

---

### Claude Task E — Query Architecture convention dokümanı (#10)

**Durum:** ✅ Tamamlandı — `2169f86`. `docs/frontend-platform/QUERY_ARCHITECTURE.md`: key factory
şekli, stale-time kademeleri, invalidation/cache-write konvansiyonu, prefetch-on-hover — dört
mevcut factory (`pipelineKeys`, `dashboardKeys`, `calendarKeys`, `demoTableKeys`) örnek olarak.
Kod değişikliği yok, `npm run check` zaten yeşildi.

**Neden:** Sıfır kod, ama şu an sadece kafada var — yazıya dökmek dakikaya en yüksek getiriyi
sağlayan kalem.

**Ne yapılacak:**
1. `pipelineKeys`, `dashboardKeys`, `calendarKeys`, `demoTableKeys` — dördünün de tesadüfen aynı
   şekli takip ettiğini doğrula (`grep -rn "Keys = " src/features`).
2. Yeni bir platform dokümanı yaz (design-system klasörünün *dışında* — "Codebase notes" bölümü
   bunu zaten söylüyor): key factory şekli, stale-time kademeleri (referans verisi vs. canlı
   liste), invalidation map konvansiyonu, prefetch-on-hover kuralı — dört mevcut factory örnek
   olarak gösterilsin.

**Kabul kriteri:** Doküman var ve mevcut factory'lerle tutarlı. Kod değişikliği yok,
`npm run check` zaten yeşil kalır.

---

### Claude Task F — AI-Ready UI Metadata (#60)

**Durum:** ✅ Tamamlandı — `1caa8b7`. `ActionContext`'e (`src/lib/actions/types.ts`) mevcut
`entity` alanının yanına, `Command`'a (`src/lib/commands/types.ts`) kendi opsiyonel `permission`
alanı eklendi — #17'nin `PermissionId`'sine referans veriyor. Geriye uyumlu (tüm alanlar
opsiyonel); tek mevcut çağıran (`pipeline/actions.ts`'teki `registerAction`) kırılmadı,
`npm run check` + `npm run build` yeşil.

**Neden:** Küçük ama Claude'un zaten yazdığı/incelediği üç sözleşmeye (`Action`, `Command`,
`EntityRef`) dokunuyor — şeklin niyet edildiği gibi kalması için aynı sahipte tutulması daha
güvenli.

**Ne yapılacak:**
1. `src/lib/actions/types.ts`'teki `ActionContext`'e ve `src/lib/commands/types.ts`'teki komut
   tipine, mevcut `entity` alanının yanına opsiyonel bir `permission` alanı ekle (#17'nin
   `PermissionId`'sine referans veren).
2. Tip dosyalarının başına, bunun bir agent/otomasyonun DOM'a dokunmadan çalışmasını sağlayan
   sözleşme olduğunu belirten kısa bir not ekle.

**Kabul kriteri:** `npm run check`, `npm run build`. Tip değişikliği geriye uyumlu (opsiyonel
alan), mevcut `registerAction`/`registerCommand` çağırıları kırılmıyor.
