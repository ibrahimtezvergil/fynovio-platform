# CODEX EXECUTION PROMPT — Collaboration / Personal Calendar (real API, `EntityRef` links)

You are working in `fynovio-platform` (.NET 10 modular monolith + React/Vite frontend under `web/`).
Implement the personal calendar end to end with enterprise discipline. Read first, in this order:

1. `AGENTS.md` (binding engineering contract) and `CLAUDE.md` (retrieval hierarchy, plan-checkbox rule, safety).
2. `docs/codex-workflow.md`, `web/AGENTS.md`.
3. `docs/schema/crm-sales-schema.md` and `docs/schema/identity-access-schema.md` (the shape every new schema follows).
4. Research repo `../enterprise ve B2B mimari araştırma/docs/architecture-analysis/`: `04` (EntityRef), `07` §Platform fabric (line ~54), `08` (Collaboration row, line ~58), `12`, `20`.
5. Reference implementation to mirror, not copy blindly: `src/Modules/CRM/` (`CreateOpportunityHandler`, `OpportunityConfiguration`, `EnableRowLevelSecurity*` migrations, `CrmModuleCapabilities`), `src/Contracts/` (`IPartyDirectory`, `IAuthorizedPrincipalDirectory`, `EntityRef`), `src/Host/Endpoints/OpportunityEndpoints.cs`, `src/Host/Modules/PlatformModules.cs`.

Use `graphify query/explain/path` before broad greps (CLAUDE.md).
Do not edit `.claude/settings*.json`, user-level config, generated migrations by hand (except the RLS exception in AGENTS.md), or `docs/` reference repos.

## Step 0 — Create your own worktree (you do this; the owner does not)

You may be started from the main checkout. AGENTS.md requires 1 task = 1 branch = 1 worktree = 1 agent session,
so before touching any file, set up an isolated worktree yourself:

1. From the main checkout (`fynovio-platform`), confirm `git status --short` is clean apart from this untracked prompt file, and that you are on up-to-date `main`.
2. Create the worktree next to the main checkout:
   `git worktree add ../fynovio-platform-calendar -b feat/collaboration-calendar main`
   (if the branch or path already exists, inspect it first — never overwrite or reuse another agent's worktree).
3. Continue **all** work from `../fynovio-platform-calendar` (re-anchor your shell/session there). Never edit files in the main checkout after this point.
4. Copy this prompt into the worktree at `docs/plans/collaboration-calendar/CODEX_EXECUTION_PROMPT.md` and make it the branch's first commit
   (`docs(plans): add collaboration calendar execution prompt`). Leave the untracked original in the main checkout alone and tell the owner in your first report that it must be deleted before merge (git refuses to merge over an untracked file).
5. Fresh-worktree setup: `dotnet tool restore`; `npm ci` in `web/`; `graphify update .` is run manually before commits here (the post-commit hook exits by design in linked worktrees — see `docs/codex-workflow.md`).
6. If your sandbox cannot create or write to the sibling directory, report that as a `BLOCKER` and stop. Do **not** fall back to working in the main checkout or on `main`.

Never run destructive worktree/branch commands (`git worktree remove`, `git branch -D`, `git reset --hard`, force-push) without explicit owner confirmation; the repo safety guard will block them anyway.

## Product decisions (already made by the owner)

- `/calendar` stops using MSW mocks and talks to the real API.
- A user creates a **personal calendar entry**: title, date/time, all-day flag, note, and a user-chosen `#rrggbb` color.
- Event *types* and the bottom "Event types" legend are removed. Color belongs to the entry; use the existing `ColorInput` (`web/src/components/common/inputs/ColorInput.tsx`, demoed at `/demo/forms`).
- An entry may optionally link to a CRM opportunity, a customer (party), or — later — any other domain record, via an `EntityRef`-shaped link. If the user is authorized for the target, they can navigate from the entry to it.

"Enterprise level" here means the **quality bar**, not a wider feature set: tenant isolation with RLS, idempotent commands,
optimistic concurrency, server-side authorization, no data leaks through links, bounded queries, full test pyramid,
accessibility, i18n (tr/en parity), and docs kept in sync. Do NOT expand product scope (see Non-goals).

## Architecture decisions (defaults — record them in the ADR at S0)

| # | Decision | Default |
|---|---|---|
| D1 | Module boundary | New module **`Collaboration`** (doc 08 reserves it), schema `collaboration`, first aggregate `CalendarEntry`, table `calendar_entries`. Rationale: a user-created entry with its own dates is a system of record, not a calendar *projection* (doc 07 l.54); it must not become the authority for any date another module owns. `/calendar` remains a view that may later also render other modules' facts it does not own. |
| D2 | Enablement | Normal module manifest (`collaboration`, v1). Role `collaboration_user` with **explicit** action keys, `Relation: "owner"`, no wildcard. Update dev seed / `enable-tenant-module` / `bootstrap-tenant-admin --modules` so local tenants and the dev user get it. State the provisioning consequence in the ADR (copy-once, no reconciler). |
| D3 | Outbox | Write **thin** outbox rows in the same `SaveChanges()` (id, owner principal, timing, link ref — never title/notes). **No Worker dispatcher** (same as Access/MasterData today). |
| D4 | Delete | **Hard delete** (AGENTS.md forbids `deleted_at`, not real deletion of personal data). Outbox row records the fact. |
| D5 | Privacy | Entries are private to their owner. No `All` scope in the template; list/get handlers additionally filter `owner == actor` and fail closed. A manager cannot see a rep's entries. |
| D6 | v1 link types | `crm/opportunity` end to end (incl. navigation to `/crm/opportunities/:id`). `masterdata/party` (customer) in the model and resolver, **without navigation** — the web has no customer detail route. |
| D7 | Time contract | Timed: `timestamptz` (UTC) in DB, offset-bearing ISO 8601 on the wire. All-day: `date` columns, **exclusive end** (FullCalendar semantics), timezone-free. State this in the schema doc and API tests. |

Evidence records: **not required** (AGENTS.md Enforcement Scope: only risk-catalogued commands — money, authorization changes, cancellation). Cite this in the ADR instead of omitting silently.
Idempotency: **mandatory** for create, update and delete (binding core #4).

## Non-goals (do not build; note them in the ADR)

No completion/done state (that is Human Tasks), no reminders/notifications, no attendees/invites/sharing,
no recurrence, no external calendar sync, no projection of other modules' facts, no dialog-level opportunity search
(v1 link creation is "Add to calendar" from the opportunity page/row menu + removing a link in the edit dialog).

## Data model (`collaboration.calendar_entries`)

- `id bigint identity`, `tenant_id`, `owner_principal_issuer`, `owner_principal_subject`, `(tenant_id, id)` alternate key.
- `title` 1–200 (trimmed), `notes` ≤ 4000 plain text, `color char(7)` with `CHECK (color ~ '^#[0-9a-f]{6}$')`; normalize to lowercase in domain and API (`COLOR_SWATCHES` are uppercase).
- Time: `all_day`, `start_at`/`end_at timestamptz`, `start_date`/`end_date date`. CHECKs: all-day ⇒ dates present, times null; timed ⇒ `start_at` present, dates null; `end > start` when present; `end` nullable.
- Link: `link_bounded_context`, `link_entity_type`, `link_entity_id bigint` — all-or-none CHECK, `id > 0` CHECK. **No FK**, tenant is the row's `tenant_id` (mirror `OpportunityLine.ProductRef`).
- `row_version bigint` concurrency token, `created_at`, `updated_at`.
- Companion tables `idempotency_records` and `outbox_messages` (module-local duplicates, per Access/CRM precedent).
- Indexes for the range query: `(tenant_id, owner_principal_issuer, owner_principal_subject, start_at)` and the same with `start_date`.
- One migration sequence: table creation + hand-written RLS (`ENABLE` + `FORCE` + `tenant_isolation` policy on every tenant-scoped table, including idempotency and outbox) — copy the shape of `EnableRowLevelSecurityOnPipelineTables`. Reversible `Down()`. Generate with `dotnet ef`, run `has-pending-model-changes`, and check the migration against the new `docs/schema/collaboration-schema.md` line by line.

## Linked records (`EntityRef`)

- `Contracts`: add a link-target resolver interface keyed by `(BoundedContext, EntityType)`, batch-capable, returning `Accessible(label, subtitle)` or `Unavailable`. Follow `IPartyDirectory` / `IAuthorizedPrincipalDirectory` precedent. Collaboration references `Contracts` only.
- CRM implements `crm/opportunity` (authorize with the opportunity's own read action + `ResourceDescriptor` incl. owner). The **party** resolver also lives in CRM (MasterData has no PDP of its own — AGENTS.md L-3; CRM's `crm.reference.party.search` gates party reads today). Document this as an interim under L-3. A merged party resolves to its survivor.
- Host composes one directory over all resolvers (Host is the only place that knows several modules).
- Write path: Host builds `EntityRef` with `actor.TenantId` — a tenant never comes from the request body. Unknown type, non-existent target, other-tenant target and unauthorized target all return the **same** 422 (`link_target_unavailable`) — no existence oracle.
- Read path: resolve labels at read time, batched per bounded context. **Never denormalize the target's name onto the row** (it would leak to a user who later loses access). `Unavailable` renders as a plain entry with no navigation. A deleted/renamed/merged target degrades, never errors.
- Authorization is server-side. `usePermission` / `useCapability` are UX-only mock remnants — never use them to authorize a link.
- Record the `EntityRef.Id is long` limitation in the ADR.

## API contract

| Route | Notes |
|---|---|
| `GET /calendar/entries?from=&to=` | Offset-bearing ISO range. All-day overlap uses dates derived from the range widened by 1 day each side (over-fetch is harmless; the grid filters). Max range (e.g. 100 days) and a result cap → 422 `range_too_large`. |
| `GET /calendar/entries/{id}` | Non-owner ⇒ 404 (not 403). |
| `POST /calendar/entries` | `Idempotency-Key` required. Result `{ id, rowVersion, replayed }`. 201. |
| `PUT /calendar/entries/{id}` | Full replace, `expectedVersion`, `Idempotency-Key`. Drag/resize uses this. |
| `DELETE /calendar/entries/{id}?expectedVersion=` | `Idempotency-Key`. |

Request: `title`, timing fields, `allDay`, `notes`, `color`, `link?: { boundedContext, entityType, id }`.
Response entry: same fields + `rowVersion` + `link?: { ref, state, label?, subtitle? }` (`label`/`subtitle` absent when `Unavailable`).
Errors: ProblemDetails via the existing exception handler pattern — 400 validation, 401, 404, 409 version conflict / idempotency-key reuse, 422 link/range.
Handler order (mirror Phase 2): load owner-scoped resource → authorize → idempotency lookup → validate → `SaveChanges` (state + outbox + idempotency in one transaction) → handle the unique-violation replay race.
Action keys: `collaboration.calendar_entry.create|read|list|update|delete`. Register in **both** `PlatformModules.CapabilityManifests` and `PlatformModules.ActionRegistry`.
Logging must never contain `title`/`notes`.

## Module-surface checklist (nothing here is optional)

- `src/Modules/Collaboration/` + `Collaboration.csproj` (references `Contracts` only), added to `fynovio-platform.slnx`; `tests/Collaboration.Tests/`.
- `CollaborationDbContext` (schema `collaboration`, own `__ef_migrations_history`), connection string resolver like `CrmConnectionString`, registration in `src/Host/Program.cs` (with `.UseSnakeCaseNamingConvention()` like the others). No Worker changes (D3).
- `scripts/create-runtime-role.sql`: `GRANT USAGE ON SCHEMA collaboration`, table/sequence grants and `ALTER DEFAULT PRIVILEGES` for `fynovio_app`. Missing this leaves the runtime role with no access.
- `CollaborationActionKeys` / `CollaborationActionCatalog` / `CollaborationModuleCapabilities` (`Validate()` must pass; a test proves catalog ⇔ template agreement, like CRM's).
- Dev seed / bootstrap updates so a local tenant can actually use the calendar.
- `docs/schema/collaboration-schema.md` (written BEFORE the EF model), an ADR in the research repo's numbered series only if the owner asks (otherwise `docs/plans/collaboration-calendar/adr-collaboration-boundary.md` in this repo), and update the "Status" section of `AGENTS.md` when done.

## Frontend (`web/`)

- Remove: `EVENT_KINDS`, `KIND_STYLE`, `useKindMeta`, entry fields `kind`/`owner`/`location`/`account`, the legend `Card` and `page.eventKindsHeading`, `kind.*` locale keys (tr **and** en), the `StatusBadge` in `EventDetailDialog`, the hardcoded "4 views" in `page.recordCount`.
- Delete `src/mocks/handlers/calendar.ts` and its entry in `handlers/index.ts`; add `/calendar` to the never-mocked guard in `src/mocks/noOpportunityMocks.test.ts`; update `web/src/lib/apiMode.ts` comment; update the `calendar_events` row in `web/docs/backend/BACKEND_DATA_REQUIREMENTS.md`; update `endpoints.ts`.
- Query keys must include **tenant and principal** (personal data; follow `opportunityKeys` + `tenantIsolation.test.tsx`). Fetch by visible range from `datesSet`, keep previous data while switching, invalidate on mutation.
- Zod schemas at the API boundary (`parseApiResponse`). Validate `color` with `^#[0-9a-f]{6}$` — it flows into inline styles.
- Time: replace `toLocalIso` (mock-era wall clock) with offset-bearing ISO for timed entries; keep exclusive `end` for all-day; `EventDetailDialog.formatWhen` already compensates for it — keep it consistent.
- Contrast: replace `contrastColor: 'var(--background)'` with black/white computed from the entry color's relative luminance (WCAG 1.4.3). Unit-test it.
- Create/edit dialog via `openDialog`: title, date/time (existing pickers), all-day, notes, `ColorInput`, link chip with remove. Idempotency key generated per logical action and reused on retry (see `web/src/features/opportunities/api.ts`). Errors rendered inline, mapped from ProblemDetails.
- Drag/resize → `PUT` with `expectedVersion`; serialize per entry (one request in flight, use the returned `rowVersion` for the next); on 409/error roll back the optimistic write and refetch.
- `openRecord` button becomes a real link to `/crm/opportunities/:id` when the link is `Accessible` and the type has a route; nothing for `Unavailable` or a type without a route (party in v1).
- "Add to calendar" entry point from the opportunity detail page / row menu (prefills the link).
- Keyboard and screen-reader behavior for the dialog and grid interactions; tr/en parity for every new string.

## Test requirements (definition of done; mirror `tests/CRM.Tests`)

Backend (`tests/Collaboration.Tests`, `tests/Host.Tests`, plus CRM resolver tests in `tests/CRM.Tests`):
- **Domain:** title/notes limits, color normalization, `end > start`, all-day vs timed exclusivity, link all-or-none, version increments.
- **Persistence:** one test per CHECK constraint; optimistic-concurrency conflict.
- **RLS:** as the unprivileged runtime role (`PostgresFixture.RuntimeConnectionStringAsync()`), cross-tenant read blocked and `WITH CHECK` write rejected — entries, idempotency, outbox.
- **Handlers:** create writes entry + outbox + idempotency exactly once; replay creates nothing new; same key + different body ⇒ key-reuse conflict; concurrent same-key race hits the unique-violation replay branch; authorization denial precedes idempotency lookup; another user's entry ⇒ 404 for get/update/delete; 409 on stale `expectedVersion`; link cases (unknown type / other tenant / unauthorized / missing ⇒ identical 422; accessible ⇒ hydrated; unavailable ⇒ no label); delete replay.
- **Contract test:** CRM opportunity resolver agrees with the `crm.opportunity.read` PDP decision (same idea as the `IAuthorizedPrincipalDirectory` agreement test).
- **Architecture (NetArchTest):** Collaboration depends on `Contracts` only; `PlatformModulesTests` still green with both registrations.
- **Host.Tests HTTP:** 401 without token, missing `Idempotency-Key` ⇒ 400, JSON shape, range bounds, 404/409/422 mapping, linked-record-without-access case.

Frontend: zod schema tests, date/time conversion tests (all-day exclusive end, offset round trip, DST-adjacent case), luminance/contrast helper, form validation, link states (accessible / unavailable / no route), mock guard covers `/calendar`, tenant/principal query-key isolation, tr/en locale parity, drag → request payload → rollback on 409.

Run and show real output before claiming anything is done (`verification-before-completion`):
`dotnet build`, `dotnet test` (Docker running; `DOCKER_HOST=unix://$HOME/.docker/run/docker.sock` if needed; the sandbox may need `-m:1 -nodeReuse:false`), `dotnet format --verify-no-changes`,
`dotnet ef migrations has-pending-model-changes` for the new context, and in `web/`: `npm run check` (lint + type-check + tests) and `npm run build`.
Finally exercise the real flow against the running API in a browser (create, drag, resize, edit, delete, link + navigate, an unauthorized link, second tenant/user isolation).

## Stages, stop points and tracking

Write the plan as `docs/plans/collaboration-calendar/2026-09-21-collaboration-calendar-plan.md` using the checkbox rule from `CLAUDE.md`:
a step is `[x]` only after its commit exists, with `→ Commit: \`<hash>\` "<message>"` under it. Small, focused commits; `dotnet format` before each; English commit messages ending with the attribution line configured for this repo.

- **S0 — Decision gate.** Deliver `docs/schema/collaboration-schema.md`, the ADR (D1–D7 + non-goals + limitations), and the plan. **STOP after S0 and report to the owner; do not generate migrations or code until the owner replies "go".** (The owner may have edited D1–D6 by then.)
- **S1 — Backend core.** Module skeleton, schema, CHECKs, RLS migration, runtime-role script, `PlatformModules`, manifest/catalog, DI, Create/Get/List, tests.
- **S2 — Update/Delete + Host endpoints** + Host.Tests.
- **S3 — Links.** `Contracts` interface, CRM resolvers, Host directory, hydration and write validation.
- **S4 — Frontend.** Real API, removals, dialog + `ColorInput`, drag/resize, link UX, mock removal + guard, docs.
- **S5 — Closure.** Full suite, native review (`codex review --base main`), findings validated against code before fixing, browser E2E, `graphify update .` and the AGENTS.md graph-commit procedure, update `AGENTS.md` Status. Branch hygiene (delete the merged branch and remove its worktree in the same step) is done only after the owner merges and confirms — do not merge or delete anything yourself.

## Reporting

Keep output factual and short: what changed, real command output, what is next. If code and a binding document conflict, or a mandatory reference is missing, report it as `BLOCKER` / `OPEN DECISION` instead of guessing. Never resolve an owner-gated decision (D1–D6) differently from the table above without asking.
