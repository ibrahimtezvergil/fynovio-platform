# PHASE 2 — CRM Opportunity Commands & API — Closure Report

**Date:** 2026-09-20
**Status:** Phase 2 accepted. This document closes it out.

---

## 1. Phase 2 scope completed

**Commands implemented** (`src/Modules/CRM/Application/`):
- `CreateOpportunityCommand` / `CreateOpportunityHandler`
- `AddOpportunityLineCommand` / `AddOpportunityLineHandler`
- `CancelOpportunityLineCommand` / `CancelOpportunityLineHandler`
- `OpenOpportunityCommand` / `OpenOpportunityHandler`
- `ChangePipelineStageCommand` / `ChangePipelineStageHandler`
- `WinOpportunityCommand` / `WinOpportunityHandler` (renamed from `CompleteOpportunity*`, decision §2.7)
- `LoseOpportunityCommand` / `LoseOpportunityHandler`
- `ReassignOpportunityCommand` / `ReassignOpportunityHandler` (new, decision §2.2 — reuses the existing `AssignedPrincipal` column, no migration)

**Queries implemented:**
- `GetOpportunityQuery` / `GetOpportunityHandler`
- `ListOpportunitiesQuery` / `ListOpportunitiesHandler` (record-level scope filtering via `IAccessScopeResolver`)
- `GetOpportunityAvailableActionsQuery` / `GetOpportunityAvailableActionsHandler` (UX-projection only — every mutation independently re-checks its own guards)
- `GetPipelineStagesQuery` / `GetPipelineStagesHandler`

**HTTP endpoints implemented** (`src/Host/Endpoints/OpportunityEndpoints.cs`), all behind JWT bearer authentication + `ActorContextMiddleware`:
| Method | Route | Command/Query |
|---|---|---|
| POST | `/opportunities` | Create |
| POST | `/opportunities/{id}/lines` | AddLine |
| POST | `/opportunities/{id}/lines/{lineId}/cancel` | CancelLine |
| POST | `/opportunities/{id}/open` | Open |
| POST | `/opportunities/{id}/stage` | ChangePipelineStage |
| POST | `/opportunities/{id}/win` | Win |
| POST | `/opportunities/{id}/lose` | Lose |
| POST | `/opportunities/{id}/reassign` | Reassign |
| GET | `/opportunities/{id}` | GetOpportunity |
| GET | `/opportunities` | ListOpportunities |
| GET | `/opportunities/{id}/actions` | GetOpportunityAvailableActions |
| GET | `/pipelines/{versionId}/stages` | GetPipelineStages |

**Authorization integration:** every command and query calls `IAuthorizer.AuthorizeAsync` (coarse/owner-relation) or `IAccessScopeResolver.ResolveAsync` (list filtering) against the real Phase 1.5 `AccessAuthorizer`/`AccessScopeResolver` PDP — no CRM-local authorization logic exists anywhere. `ActorContext` is built exclusively from validated JWT claims (`ActorContextMiddleware`), never from a caller-supplied command field. `AuthorizationDenialStage` (see §2) threads through every mutation handler into `CrmProblemDetailsExceptionHandler`.

**Field/policy behavior actually implemented:** RBAC + tenant-wide scope + `owner`-relation grants only — Phase 1.5's frozen Phase 1.5 scope (round 3 §8 freeze #10). Field-level security, policy obligations/approvals, and Team/Territory/Organization-unit scopes were **confirmed not to exist** in Phase 1.5 and were correctly **not built** in Phase 2 (test-gap audit §§5–7; building them would have violated `BINDING_SPEC.md` §19's non-goal list).

**Idempotency:** every mutation handler performs an idempotency-key lookup (`IdempotencyRecords`, scoped by tenant+principal+operation+key) before mutating, replays the stored response on an exact-hash match, throws `IdempotencyKeyReusedException` on a hash mismatch, and handles the concurrent-duplicate race (two requests with the same key committing simultaneously) by catching the resulting unique-violation and replaying the winner's result rather than surfacing a raw `DbUpdateException`.

**Optimistic concurrency:** every mutation compares the caller's `ExpectedVersion` against the loaded aggregate's `RowVersion` before mutating, and additionally catches `DbUpdateConcurrencyException` at `SaveChangesAsync` (true race, not just stale-read) — both paths translate to the same `OpportunityConcurrencyConflictException` → HTTP 409 `concurrency_conflict`.

**Evidence:** every successful mutation writes an append-only `EvidenceRecord` (tenant, resource type/id, row version, principal, operation name, payload, correlation id) in the same transaction as the domain write.

**Outbox:** every successful mutation writes an `OutboxMessage` in the same transaction, carrying the module's event-catalog type (e.g. `enterprise.crmsales.opportunity.won.v1`) for later dispatch. (The outbox *dispatcher* itself remains out of Phase 2's scope per the original plan — this closure report does not claim dispatch was built.)

**RLS/runtime-role behavior:** every handler opens an explicit transaction and calls `SetTenantContextAsync` before touching any RLS-protected table — this was already correct on the CRM side from earlier phases. This session additionally found and fixed the same requirement missing on the **Access** side (§2, §3).

**Pipeline behavior:** `PipelineStage.IsEntry` is the sole source of truth for entry-stage selection (decision §2.3); `SortOrder` is presentation ordering only, never read as an entry signal anywhere in the codebase. `ChangePipelineStage` is unrestricted within the current pipeline version's active stages (no transition matrix — decision, not a gap). `IsActive == false` stages are rejected both as `ChangePipelineStage` targets and as `Open`'s auto-assigned entry stage (§3 below).

---

## 2. Architecture Deltas

### 2.1 `AuthorizationDenialStage` delta

**Original problem:** Phase 1.5's `AuthorizationDecision` carried only Allow/Deny with a reason code — no way to distinguish "the caller has zero grant for this action at all" (should stay a visible 403) from "the caller holds a grant for this action, but it doesn't cover this specific record" (must be externally indistinguishable from a 404, per the tenant non-leak rule) without CRM inventing its own second authorization implementation.

**Chosen solution:** an additive enum, `AuthorizationDenialStage {None, Coarse, Record}`, added to `Contracts.AuthorizationDecision`'s constructor as an optional 5th parameter (backward compatible — no existing call site needed to change). `AccessAuthorizer.AuthorizeAsync` computes it from information the PDP already has: `items.Count == 0` → `Coarse`; a matching grant exists but no branch authorized this specific resource → `Record`. CRM's `OpportunityAuthorizationDeniedException` carries `DenialStage` end-to-end (type never substituted for `OpportunityNotFoundException`, preserving internal diagnostic semantics); only `CrmProblemDetailsExceptionHandler`'s external mapping collapses a `Record` denial to the same 404 shape a genuinely missing opportunity produces (explicit `"Opportunity {id} was not found."` title, never the raw "was denied" message).

**Why it preserves binding architecture:** zero new authorization logic exists outside `AccessAuthorizer` — CRM only *reads* a richer decision, it never re-derives or overrides one. The delta is minimal and purely additive to the existing contract.

**Affected commits:** `27b552b` (Access-side delta), `fdac321` (CRM-side PEP wiring + tests).

### 2.2 Phase 1.5 runtime-RLS PDP delta

**Original problem:** `AccessAuthorizer`, `AccessScopeResolver`, and `PrincipalResolver.IsActiveTenantMemberAsync` queried RLS-protected Access-schema tables without ever calling `SetTenantContextAsync`. Every existing test suite ran these classes against the Postgres superuser connection (which bypasses RLS unconditionally), so this was invisible until Host.Tests was fixed to run under the real `fynovio_app` role, at which point the entire authorization system failed closed for every request — a genuine, previously-undetected production blocker, not a CRM Phase 2 defect.

**Chosen solution:** each of the three methods now opens its own transaction and calls `SetTenantContextAsync` for the claimed/actor tenant before querying — the exact same canonical pattern every CRM/MasterData handler and Access's own `Grant`/`RevokeRoleAssignmentHandler` already used. Each method is reentrant (skips opening a second transaction when the caller already holds one open with tenant context set on the same `AccessDbContext` instance), since `Grant`/`RevokeRoleAssignmentHandler` call `AuthorizeAsync` from inside their own already-open transaction.

**Why it preserves binding architecture:** no new tenant-resolution mechanism, no new transaction primitive, no BYPASSRLS, no SECURITY DEFINER, no trusted-header shortcut. Tenant context establishes DB visibility only — every authorization branch still independently re-checks `TenantId`/`AccountId`/`ActionKey`/relation, exactly as before. This is Phase 1.5's own established convention applied to three call sites that had missed it, not a redesign.

**Affected commits:** `abfdf01` (the fix), `25e9b95` and `c4dbca5` (runtime-role proof tests, including a review-driven correction to make the pooling non-leak test actually discriminating). Full writeup: `docs/architecture-analysis/2026-09-20-phase-1-5-runtime-rls-pdp-delta.md`.

### 2.3 Explicit `IsEntry` / invalid pipeline configuration resolution

**Original problem:** two related gaps. (a) The entry-stage *selection rule* was previously underspecified between "first stage added" and an explicit flag. (b) `OpenOpportunityHandler.ResolveEntryStageAsync` filtered only `IsEntry`, never `IsActive`, and silently returned a null stage id when no active entry stage existed for an otherwise-resolved pipeline version — conflating "tenant has no pipeline configured" (legal) with "tenant's pipeline is configured but broken" (should be a hard error).

**Chosen solution:** `PipelineStage.IsEntry` is the sole source of truth (owner decision, 2026-09-19); `SortOrder` is never read as an entry-stage signal anywhere. `ResolveEntryStageAsync` now filters `s.IsEntry && s.IsActive` and throws the new `PipelineConfigurationInvalidException` (mapped to HTTP 409 `invalid_pipeline_configuration`) when a resolved version has no usable entry stage, instead of returning `(resolvedVersionId, null)`. "No pipeline configured at all" remains legal and unchanged (`Opportunity.Open` still accepts a null version/stage). Existing-Opportunity pinning (an opened Opportunity's version/stage never retroactively moves) was confirmed true by construction and regression-locked with a dedicated test.

**Why it preserves binding architecture:** `AddStage`'s `_stages.Count == 0 → IsEntry` default is kept as a creation-time convenience only, not a semantic rule — `MarkEntry` remains the sole explicit configuration operation, unchanged. No new pipeline mechanism was introduced; only the one already-broken read path was fixed.

**Affected commits:** `e52f37f` (the fix + entry-stage proof tests), `e52b467` (remaining decision-2 proof-points: multi-pipeline tie-break, version pinning, same-stage no-op).

---

## 3. Production defects discovered and fixed

1. **Phase 1.5 runtime RLS context failure** — `AccessAuthorizer`/`AccessScopeResolver`/`PrincipalResolver` never established RLS tenant context, so the entire authorization system would fail closed under the real, correctly-configured `fynovio_app` runtime role in any production deployment. See §2.2. Fixed in `abfdf01`.
2. **Invalid/missing pipeline entry silently allowing Open** — `OpenOpportunityHandler` would open an Opportunity against an inactive or nonexistent entry stage without any error, silently persisting a broken pipeline reference. See §2.3. Fixed in `e52f37f`.
3. **`GetPipelineStages` missing authorization** — the query handler read RLS-protected pipeline configuration data with no `IAuthorizer` check at all; any authenticated tenant member could read it regardless of holding `crm.opportunity.read`. RLS still kept results tenant-scoped, so this was a missing-capability-check gap, not a cross-tenant leak. Fixed in `6a9d11b`.

A fourth, test-infrastructure-only defect was also found and fixed as a prerequisite to discovering #1: `Host.Tests` was silently connecting the application under test as the Postgres migration superuser rather than the unprivileged runtime role, meaning no Host.Tests run had ever actually exercised RLS enforcement. Fixed alongside `fdac321`.

---

## 4. Final security properties

- **Production runtime does not require BYPASSRLS:** confirmed. `fynovio_app` remains `NOSUPERUSER NOBYPASSRLS` in both `scripts/create-runtime-role.sql` and every test fixture that mirrors it. No delta introduced or requires a bypass role.
- **Tenant context is not equivalent to authorization:** confirmed by design and by test (`PdpRuntimeRoleTests`). Setting `app.tenant_id` only changes DB row visibility; every authorization branch (`AccessAuthorizer.AuthorizeAsync`, `AccessScopeResolver.ResolveAsync`) still independently re-checks `TenantId`/`AccountId`/`ActionKey`/relation before granting.
- **Coarse deny → 403:** confirmed (`CrmProblemDetailsExceptionHandlerTests.Coarse_denial_stays_visibly_forbidden`, `Coarse_denial_with_no_opportunity_id_stays_forbidden`; HTTP-level `Creating_an_opportunity_for_a_tenant_the_caller_does_not_belong_to_is_forbidden`).
- **Record-level deny → external 404:** confirmed (`CrmProblemDetailsExceptionHandlerTests.Record_level_denial_and_genuinely_missing_opportunity_produce_identical_responses`; HTTP-level `Record_level_denial_on_a_mutation_is_the_same_404_as_a_genuinely_missing_opportunity`).
- **Genuine missing → same external 404 contract:** confirmed by the same tests — status, `type`, and title *format* are identical between the two paths; only the embedded id differs, which is expected and does not leak record existence.
- **Cross-tenant existence is not leaked:** confirmed (`Cross_tenant_real_opportunity_id_is_not_found_over_http` — a real opportunity id belonging to another tenant returns the same 404 as a nonexistent id).
- **Runtime PDP works under `fynovio_app`:** confirmed (`PdpRuntimeRoleTests`, all 5 tests, run against `PostgresFixture.RuntimeConnectionStringAsync()`; `OpportunityAuthorizationTests` also switched onto the runtime role in this closure cycle's predecessor session).
- **Connection tenant context does not leak across pooled requests:** confirmed (`PdpRuntimeRoleTests.Sequential_authorization_calls_on_the_same_connection_do_not_leak_tenant_context` — both tenants are granted so a leak is actually distinguishable: two sequential `AuthorizeAsync` calls on the same `AccessDbContext`/connection for Tenant A then Tenant B both correctly Allow; `set_config(..., true)`'s transaction-local scope means nothing survives past each call's own commit).

---

## 5. Final test results

Run 2026-09-20, from the worktree root (`.worktrees/crm-phase2-opportunity-commands-api`), against a fresh Testcontainers PostgreSQL instance per suite:

```bash
dotnet build -m:1 -nodeReuse:false

dotnet test tests/Access.Tests/Access.Tests.csproj -m:1 -nodeReuse:false
dotnet test tests/CRM.Tests/CRM.Tests.csproj -m:1 -nodeReuse:false
dotnet test tests/Host.Tests/Host.Tests.csproj -m:1 -nodeReuse:false
dotnet test tests/MasterData.Tests/MasterData.Tests.csproj -m:1 -nodeReuse:false

dotnet format --verify-no-changes
```

| Suite | Result |
|---|---|
| Access.Tests | **65/65** |
| CRM.Tests | **135/135** |
| Host.Tests | **12/12** |
| MasterData.Tests | **30/30** |
| **Total** | **242/242** |

`dotnet format --verify-no-changes` — exit 0, no formatting drift. `dotnet build` — 0 errors, 14 pre-existing NU1900 offline-advisory warnings (network-restricted sandbox, unrelated to this work).

---

## 6. Deferred items

Carried forward from `docs/plans/crm-phase2/2026-09-19-crm-phase2-test-gap-audit.md`'s "Not closed, explicitly deferred" section and the runtime-RLS delta doc's own open item. None were silently dropped.

### P1

- **Full HTTP happy-path/error matrix for every Phase 2 endpoint.** Only Create's happy path (the authorization-wiring proof case) and the shared error-contract rows (401/403/404/409/400/idempotent-retry) are covered end-to-end over HTTP; the other ~10 endpoints (AddLine, CancelLine, Open, ChangeStage, Lose, Reassign, GetOpportunity, ListOpportunities, GetPipelineStages, GetOpportunityAvailableActions) have Application-layer coverage but no dedicated `WebApplicationFactory` HTTP test.
  **Reason deferred:** unbounded in scope relative to the P0 gaps this closure cycle targeted — building it now risked open-ended scope creep without a bounding spec.
  **Recommended future phase:** Phase 2 follow-up or Phase 3 hardening pass, whichever lands first; not a Phase 3 *feature* dependency.
  **Classification:** test debt.

- **Complete admin-vs-runtime-role audit across remaining integration tests.** Only the suites this closure cycle actually touched (`Access.Tests`, `CRM.Tests`'s `OpportunityAuthorizationTests`, `Host.Tests`) were corrected to distinguish legitimate admin-connection use (migration/seed setup) from tests that should assert RLS/authorization/application behavior and therefore must use the runtime role. The remaining integration tests across all four suites were not exhaustively re-audited.
  **Reason deferred:** the specific defect this cycle needed to fix (§2.2, §3.1) is fixed and proven; a full audit is a separate, larger verification pass with its own scope.
  **Recommended future phase:** Phase 2 follow-up, before Phase 3 begins building new RLS-protected surfaces.
  **Classification:** test debt (with a small architectural-debt component — the audit could surface further instances of the same pattern as §2.2).

### P2

- **`ChangePipelineStage` entry-stage-inactive-on-open interaction** (test-gap audit §9's original framing). The audit flagged an apparent inconsistency between `ChangePipelineStageHandler` (already rejected `!IsActive` targets) and `OpenOpportunityHandler` (previously didn't). **This was resolved, not deferred** — the owner's binding 2026-09-19 decision made an inactive entry stage on Open a hard error, implemented in §2.3/`e52f37f`. Listed here only so the original audit item's disposition is traceable, not because it remains open.
- **Outbox dispatcher.** Confirmed out of Phase 2's original scope in the source planning document (`PHASE_2_OPPORTUNITY_COMMANDS_API_PLAN.md` §3.8) — Phase 2 writes `OutboxMessage` rows correctly; nothing dispatches them yet.
  **Reason deferred:** never in Phase 2's scope to begin with.
  **Recommended future phase:** whichever phase first needs an external consumer of these events.
  **Classification:** optional enhancement (scoped out, not a defect).
- **`ReopenOpportunity`.** Explicitly deferred by owner decision §2.6 in the original Phase 2 planning document.
  **Reason deferred:** owner decision, out of Phase 2 scope by design.
  **Recommended future phase:** unscheduled, pending product need.
  **Classification:** optional enhancement.

---

## 7. No remaining blockers

- **Unresolved P0 gaps:** none.
- **Architectural contradictions:** none. The one contradiction discovered mid-cycle (§2.2/§3.1) was reported per the `ARCHITECTURAL CONTRADICTION` protocol, resolved via an explicit owner-approved Architecture Delta, and is now closed.
- **Open decisions:** none. All owner decisions from the original Phase 2 plan (§2.1–§2.7) and the two mid-cycle decisions (authorization denial-stage delta, pipeline entry-stage semantics) are resolved and implemented.
- **Known production blockers:** none. The one real production blocker found during this work (§2.2/§3.1) is fixed and proven under the real runtime role.

**Expected result: none. Confirmed: none found.**

---

## 8. Final repository verification

- **Formatting:** `dotnet format --verify-no-changes` — clean (exit 0).
- **Build:** `dotnet build -m:1 -nodeReuse:false` — 0 errors.
- **Tests:** all four suites green, 242/242 (§5).
- **`git status`:** clean working tree, no untracked files (`git status --short` and `git ls-files --others --exclude-standard` both empty at verification time).
- **Migration state:** no new migrations were required by this closure cycle's fixes (both the runtime-RLS delta and the pipeline entry-stage fix are pure application-code changes); the most recent CRM/Access migrations (`AddPipelineStageActiveAndEntryFlags`, `EnableAccessRowLevelSecurity`) are committed and match the schema the current code assumes.
- **Architecture-analysis docs:** `docs/architecture-analysis/2026-09-19-crm-phase2-authorization-delta-and-entry-stage-resolution.md`, `docs/architecture-analysis/2026-09-20-phase-1-5-runtime-rls-pdp-delta.md`, and `docs/plans/crm-phase2/2026-09-19-crm-phase2-test-gap-audit.md` are all committed and cross-referenced from this document.

No production behavior was modified for cleanup purposes in this closure cycle — this document is pure documentation.

---

## 9. Final status

**PHASE 2 — FINAL STATUS**

- Architecture: **COMPLETE**
- Authorization integration: **COMPLETE**
- Runtime RLS: **VERIFIED**
- Pipeline invariants: **VERIFIED**
- HTTP/API foundation: **COMPLETE**
- P0 test gaps: **CLOSED**
- Remaining P1/P2: **DOCUMENTED / DEFERRED**
- Full solution tests: **242/242 PASS**
- Blocking open decisions: **NONE**
- Ready to close Phase 2: **YES**
