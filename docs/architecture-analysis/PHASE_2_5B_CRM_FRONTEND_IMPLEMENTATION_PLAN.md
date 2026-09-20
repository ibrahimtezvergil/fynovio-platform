# Phase 2.5B — CRM Opportunity Frontend Integration: Implementation Plan

> **Status:** approved for implementation (spec §3: no blocking OPEN DECISION → continue). **Source spec:** `Enterprise_Phase_2_5B_CRM_Opportunity_Frontend_Integration_Binding_Implementation_Specification.pdf` (binding). **Branch:** `feat/phase-2-5b-crm-opportunity-frontend`.
> Every fact below was verified by reading the repository, not the docs. Where the spec assumes something the repository does not provide, it is listed as an **integration gap** (§2) with the smallest safe alternative, per spec §14.

## 1. Verified repository state

### 1.1 Backend — Phase 2 API surface (`src/Host/Endpoints/OpportunityEndpoints.cs`)

| Method | Route | Body / query | Result |
|---|---|---|---|
| POST | `/opportunities` | `{partyId, currency, estimatedAmount}` + `Idempotency-Key` | 201 `{opportunityId, replayed}` |
| POST | `/opportunities/{id}/lines` | `{expectedVersion, productId, quantity, unitPrice, isOptional, sortOrder}` | 200 |
| POST | `/opportunities/{id}/lines/{lineId}/cancel` | `{expectedVersion, cancelReason}` | 200 |
| POST | `/opportunities/{id}/open` | `{expectedVersion, expiryDate}` | 200 `{opportunityId, pipelineStageId, replayed}` |
| POST | `/opportunities/{id}/stage` | `{expectedVersion, targetStageId}` | 200 `{opportunityId, pipelineStageId, replayed}` |
| POST | `/opportunities/{id}/win` | `{expectedVersion}` | 200 `{opportunityId, totalAmount, replayed}` |
| POST | `/opportunities/{id}/lose` | `{expectedVersion, lostReason}` | 200 |
| POST | `/opportunities/{id}/reassign` | `{expectedVersion, newPrincipalIssuer, newPrincipalSubject}` | 200 |
| GET | `/opportunities/{id}` | — | `OpportunityDto` (incl. `rowVersion`, `lines[]`) |
| GET | `/opportunities` | `status?`, `skip`, `take` (1..1000) | `OpportunitySummaryDto[]` — **no total, no `rowVersion`, no party, no pipeline version** |
| GET | `/opportunities/{id}/actions` | — | `{canOpen, canChangeStage, allowedTargetStageIds[], canWin, canLose, canReassign}` |
| GET | `/pipelines/{versionId}/stages` | — | `{id, name, sortOrder, isActive, isEntry}[]` |

Every mutation binds `Idempotency-Key` as a required header (a missing header is a framework 400, not our ProblemDetails). Web calls go through the Vite proxy: `/api/x` → `/x`.

### 1.2 Backend — behaviours the UI must respect

- **Wire enum:** `OpportunityStatus` is serialized as an **integer** (no `JsonStringEnumConverter` anywhere): `0 Draft, 1 Open, 2 Won, 3 Lost`. One adapter in the web schema turns it into the canonical string union.
- **Concurrency (resolved, §28):** `expectedVersion: long` in the body, compared to `RowVersion`; mismatch → **409 `concurrency_conflict`**. Not ETag/If-Match. `OpportunitySummaryDto` carries no `rowVersion` ⇒ **mutations originate from the detail view only**.
- **Idempotency (resolved):** tenant + principal + operation + key; same key + same body ⇒ stored response replayed (`replayed: true`); same key + different body ⇒ **409 `idempotency_key_reused`**; a record is written only on success.
- **Errors (resolved):** `CrmProblemDetailsExceptionHandler` emits `{status, type, title}` only — **no `errors` map**. `type` ∈ `not_found` (also record-level denial, deliberately identical), `forbidden` (coarse), `concurrency_conflict`, `invalid_pipeline_transition`, `invalid_pipeline_configuration`, `idempotency_key_reused`, `validation_error` (400), `illegal_lifecycle_transition` (409). `web/src/api/client.ts#toApiError` already maps `type → ApiError.code` and `title → message`; **that is the single ProblemDetails parsing layer** — no second one is added.
- **Lifecycle rules:** lines are added only in `Draft`; `Open` needs a future `expiryDate` and assigns the tenant's entry stage (no pipeline configured ⇒ opens with a null stage; configured-but-broken ⇒ 409 `invalid_pipeline_configuration`); `Win` needs ≥1 active required line; `Lose` needs a non-blank reason; Won/Lost are terminal.
- **Initial semantics (resolved, §28 "no defined initial stage"):** Create ⇒ `Draft`, no stage; the entry stage is assigned by **Open**. The Create form therefore has no stage field.
- **Lost reason (resolved):** free text, required (decision §2.5 of the Phase 2 plan; taxonomy deferred). No structured options exist.
- **Authorization model:** coarse RBAC + tenant-wide scope + `OwnedBy` scope only. **No field-level security, no obligations / `RequireApproval`** — frozen out of Phase 1.5 by design (`PHASE_2_CLOSURE_REPORT.md` §1). `/actions` is plain booleans.

### 1.3 Frontend — 2.5A foundation (`web/`)

React 19 + Vite + TanStack Query + react-hook-form + zod + i18next (tr default, en) + shadcn/base-ui + Sonner + oxlint + vitest + Playwright. Reused as-is: `apiClient` (bearer from memory, 401→refresh→single retry, `X-Requested-With`, `X-Correlation-Id`), `lib/auth` session store + `queryClient.clear()` on tenant switch/login/logout, `useAppMutation`, `PageHeader`/`EmptyState`/`StatusBadge`/`Field`, `parseApiResponse`, the feature-owned `routes.ts`/`nav.ts` assembly, the E2E harness (`scripts/e2e.sh`, throwaway PostgreSQL, real API, Chrome).

### 1.4 Frontend — what is mock and what is not

`features/pipeline`, `dashboard`, `calendar`, `home` read **MSW mocks** (`/deals`, `/pipeline/deals`, …). Nothing mocks `/opportunities` or `/pipelines`; `onUnhandledRequest: 'bypass'` sends them to the real API. `usePermission`/`mockPermissionPolicy` is a mock-era remnant that **grants nothing** with a real session — the Opportunity feature does not use it (spec §13: no parallel permission model).

## 2. Integration gaps & decisions (pre-populated)

### Blocking defect found and fixed (in scope per spec §23 "concrete blocking defect")

**G1 — no seeded user can call any CRM endpoint.** `BootstrapTenantAccessHandler` grants only `AccessActionCatalog.All` (five `access.*`/`identity.*` keys). `Program.cs` registers `CrmActionCatalog.All` in the *registry* but nothing *grants* it, so the seeded tenant administrator gets **403 on every Opportunity endpoint**. Definition of Done (§4) and the E2E matrix (§24) are unreachable without a grant path. Also: no pipeline definition exists anywhere (nothing creates one), so Open yields a null stage and the stage UX has nothing to bind to.
**Resolution:** extend the **Development-only** `DevSeeder` (already double-guarded on `IsDevelopment()` + `DevSeed:Enabled`) with an idempotent CRM step per dev tenant — a `crm_manager` role (all CRM action keys) assigned to the seeded admin, a `crm_viewer` role (`crm.opportunity.read` + `list`) assigned to a new `viewer@fynovio.local` member of tenant 1, and one pipeline definition (`Sales pipeline`, v1: Qualification[entry] → Proposal → Negotiation, plus one deactivated stage). Domain objects only, no schema change, no migration.
**Not resolved here (→ OD2):** how *production* tenants receive CRM grants.

### Integration gaps recorded (smallest safe alternative applied)

| # | Gap | Alternative used |
|---|---|---|
| G2 | No Party/customer lookup endpoint (MasterData has no HTTP surface); list/detail expose only `partyId`. | Create takes a numeric **Party ID**; detail shows "Party #id". No customer display fields are invented. |
| G3 | No product lookup endpoint; AddLine needs `productId`. | Numeric **Product ID** input on the Add-line dialog. |
| G4 | `/actions` has no `canAddLine`/`canCancelLine`, and no `canCreate` exists before a record does. | Line controls are shown on the lifecycle state the domain allows (Draft to add; not-terminal to cancel) through **one helper** (`lineEditability`) marked for replacement by a backend projection; the backend re-authorizes and re-validates every call. "New opportunity" is always offered; a coarse 403 renders the forbidden state. |
| G5 | `OpportunitySummaryDto` has no party, no stage version, no `rowVersion`, no total count. | List shows id/status/amount/owner/stage-id; paging via `take+1` look-ahead; no counts, no mutations from list rows. Stage *names* are shown on detail only. |
| G6 | No editable CRM-owned scalar field: Phase 2 has no "update" command (`estimatedAmount` is immutable after Create). | **Edit = the line editor + Open's expiry** — the only mutable state Phase 2 defines. No generic PATCH UI (§9). |
| G7 | No field-level READ/WRITE projection, no obligation/approval contract. | Documented gap. The UI renders exactly what the DTO carries (schema fields are tolerant of omission) and adds no approval UI or masking logic. A coarse deny is a forbidden state, never an "allow". |
| G8 | Owner is a raw `(issuer, subject)` principal; no display name. | Shown as the subject in a monospace token. |

### OPEN DECISIONS (non-blocking — continued with the spec's own fallback)

**OD1 — Reassign has no eligible-assignee source** (§12/§28)
- Issue: `POST /reassign` takes a raw `(issuer, subject)`; `/auth/me` and every other endpoint expose no member directory.
- Evidence: `src/Host/Endpoints/` has Auth, AccountLifecycle, Opportunity, Dev only; `ReassignOpportunityRequest` is raw strings.
- Options: (a) add a small authorized `GET` returning assignable principals for the tenant (backend, Phase 2 addendum); (b) free-text principal entry (**rejected**: §12 forbids arbitrary principals); (c) ship without a Reassign control.
- Architectural impact: (a) is a new query in Access/CRM read surface; (b) violates §12/§13.
- UX impact: without (a) the command is not usable from the browser.
- Recommendation: **(a)**.
- Applied fallback (spec §12): the `reassign` API function + mutation hook are implemented and unit-tested against the real contract, but **no UI submits an arbitrary principal**; when `canReassign` is true the owner row shows an explicit "assignee directory not available yet" dependency notice, and when false, nothing.
- Why implementation can continue: the rest of the workflow does not depend on it.

**OD2 — production CRM grant path** (Phase 1.5's open "system-template lifecycle strategy", `AGENTS.md` Status)
- Issue: outside the Development seed, no code path gives any user `crm.*` grants; `BootstrapTenantAccessHandler` is "today's narrow, manual answer".
- Recommendation: extend the tenant bootstrap/template reconciler to include the owning modules' catalogs (Host already knows both). Not decided here — dev seed only (G1).

## 3. Definition of Done mapping (spec §4)

`Opportunity List → Create → Detail → (line edit) → Open → Change stage → Reassign*(OD1) → Win|Lose → final state`, all against the real API, exercised by Playwright.
Adjusted order note: lines are Draft-only, so the real order is Create → add line(s) → Open → Stage → Win/Lose.

## 4. UX flows

- **List** `/crm/opportunities`: status filter (`?status=`), `?page=`; loading skeleton, empty (with Create CTA), forbidden (403), error + retry (5xx/network), background-refetch indicator. Rows link to detail; no row mutations (G5).
- **Create** `/crm/opportunities/new`: react-hook-form + zod (partyId int ≥1; currency `^[A-Z]{3}$`; estimatedAmount ≥0, ≤2 dp). Tenant/principal never in the form. One idempotency key per logical submit. On success → detail route.
- **Detail** `/crm/opportunities/:id`: summary, lifecycle badge, pipeline card (current stage name, allowed targets by name), lines, owner, action bar driven only by `/actions`. Row version kept in the query cache, never displayed.
- **Actions:** Open (expiry date dialog), Change stage (select of allowed targets → confirm), Win (confirm), Lose (reason dialog), Add/Cancel line (dialogs). All server-confirmed (no optimistic updates, §19); after success invalidate detail + actions (+ list).
- **States:** 404 → not-found panel (identical for denied/missing/cross-tenant); 403 → forbidden panel; 409 concurrency → inline alert with **Reload latest**, input preserved, never auto-retried; 409 idempotency → "request conflict" alert, key rotated; 409 lifecycle/pipeline → server's business-rule message; 400 → form-level message; 401 → global session recovery (2.5A); 5xx/network → generic retry-safe message (no internals).

## 5. Mutation idempotency model

`lib/mutations/attemptKey.ts`: a key is created when a logical action starts and is **held** while the outcome is unknown (network error, timeout, 5xx) so the user's retry reuses it; it is **released** on a definitive answer (2xx or a 4xx). A changed payload gets a new key (same key + different body would 409). The key is set explicitly on the request at call time — never in the axios request interceptor — so the 401-refresh replay carries the identical header. Submit buttons are disabled while pending. Keys are not logged or rendered.

## 6. Tenant / cache isolation

Query keys are rooted in the active tenant id (`opportunityKeys(tenantId)`); `selectTenant` additionally cancels in-flight queries before the existing `queryClient.clear()`. Proven by a unit test and the E2E tenant-switch test.

## 7. API mode visibility (spec §20)

A DEV-only header chip states which API is in use (`/api` real; MSW active only for the mock-era features). A vitest guard fails if any MSW handler ever matches `/opportunities` or `/pipelines`.

## 8. Files (expected)

Web — new `src/features/opportunities/{api.ts,schema.ts,routes.ts,nav.ts,index.ts,lib/*,components/*,pages/*}`, `src/lib/mutations/attemptKey.ts`, `src/lib/apiMode.ts`, `src/locales/{tr,en}/opportunities.ts`; edits to `src/routes/{paths.ts,index.tsx}`, `src/layouts/navigation.ts`, `src/api/endpoints.ts`, `src/lib/auth/sessionClient.ts`, `src/lib/i18n.ts`, `web/e2e/{opportunities.e2e.ts,support/*}`.
Backend — `src/Host/Authentication/DevSeeder.cs` (+ `CrmDevSeed.cs`), `tests/Host.Tests/Authentication/DevSeederTests.cs`. No migration, no schema change, no Phase 2 contract change.
Docs — this plan, `PHASE_2_5B_CRM_FRONTEND_FINAL_REPORT.md`, README seed note.

## 9. Tests

- **Unit/component (vitest):** problem→UX mapping; attempt-key lifecycle; wire→canonical DTO adapters (int enum); form validation; list states (loading/empty/403/5xx); detail action rendering purely from `/actions` (incl. all-false, no stage, no pipeline); stage selector = allowed ids only, names from stages; concurrency conflict UI (input kept, no auto retry); idempotency reuse across a simulated network retry; tenant-switch cache isolation; guard test (no Opportunity MSW handler).
- **E2E (Playwright, real API + PostgreSQL):** spec §24 matrix — login→CRM, create, validation, stage (allowed only), forbidden stage (direct API 403 as viewer), field READ/WRITE = *not applicable, documented gap G7*, concurrency (two contexts), idempotency (double click + replayed key over HTTP), tenant switch, 401 recovery, 403/404 no-leak, win, lose; reassign = *authorized ⇒ dependency notice, unauthorized ⇒ absent, direct API unauthorized ⇒ denied* (OD1).

## 10. Explicit non-goals

Customer Need Intelligence / AI, activities, quotes, sales orders, feedback, consent, analytics, admin IAM console, shell redesign, backend domain redesign, production grant path (OD2), a party/product/assignee directory (G2/G3/OD1).
