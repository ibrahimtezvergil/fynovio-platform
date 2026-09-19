# CRM Phase 2 — Test Gap Audit

**Status:** Audit only. No production code or test file changes in this document's scope, except one throwaway probe (see §3) that was run, confirmed, and reverted — it is not part of this diff.

**Method:** repository code = source of truth for what exists; `PHASE_2_OPPORTUNITY_COMMANDS_API_BINDING_SPEC.md` + `PHASE_2_OPPORTUNITY_COMMANDS_API_PLAN.md` + Phase 1.5 (`docs/plans/enterprise-access-foundation/*.md`) = source of truth for intended behavior. Every verdict below is cited to a specific file/line or doc section — none are inferred from the audit checklist itself.

**A note on the input:** the audit checklist this document responds to was cut off mid-sentence at section 9 ("termi..."). Sections 0–8 are addressed in full below; section 9 is addressed as far as it was received. If there was more after "termi...", it hasn't been seen.

---

## Two owner decisions — RESOLVED 2026-09-19

Both decisions below were raised here as blockers and are now resolved. Full resolution text, the concrete `AuthorizationDenialStage` delta design, and the required `OpenOpportunityHandler` fix live in `docs/architecture-analysis/2026-09-19-crm-phase2-authorization-delta-and-entry-stage-resolution.md` — this section is kept for the original problem statement/evidence only; treat the linked document as authoritative for what to build.

### Decision 1 — mutation-side 403 vs. 404 for record-level denial — RESOLVED: option (a), Architecture Delta

`PHASE_2_OPPORTUNITY_COMMANDS_API_PLAN.md` §9A/§15 is explicit and unambiguous:

> "a record that exists but is outside the caller's resolved access scope... returns the same `404` as a record that doesn't exist at all, per §15's tenant-non-leak rule — never `403` for 'exists, not yours.'"

The actual implementation cannot honor this. `AccessAuthorizer.AuthorizeAsync` (`src/Modules/Access/Application/AccessAuthorizer.cs:45`) returns the identical `Deny("no_matching_grant", ...)` whether the principal has **zero grant for the action at all** (coarse denial) or has an `OwnedBy`-scoped grant that simply **doesn't match this record's owner** (record-level denial) — `AuthorizationDecision` (`src/Contracts/AuthorizationDecision.cs`) has no field to distinguish the two cases. Every CRM mutation handler (`WinOpportunityHandler`, `LoseOpportunityHandler`, `ChangePipelineStageHandler`, `ReassignOpportunityHandler`, `OpenOpportunityHandler`, `AddOpportunityLineHandler`, `CancelOpportunityLineHandler`) throws `OpportunityAuthorizationDeniedException` on any denial, which `CrmProblemDetailsExceptionHandler.cs:17` unconditionally maps to `403`.

The query side gets this right: `GetOpportunityHandler` never throws on denial, returns `null`, and the endpoint maps `null → 404` (`OpportunityEndpoints.cs:106`). The mutation side has no equivalent path — and can't, without one of two changes that are each blocked by another binding rule:

- Extending `AuthorizationDecision` to carry a "record exists but scope excludes it" signal is an Architecture Delta into Phase 1.5's Scope-Locked territory (`PHASE_2_OPPORTUNITY_COMMANDS_API_PLAN.md` §1: "Route for anything in the 'Frozen out' rows... `STOP → Architecture Delta note → owner approval → Phase 1.5 scope update`").
- Having CRM re-derive the distinction itself (e.g. compare `opportunity.AssignedPrincipal` to the actor after a deny, to guess whether it was record-level) is "a second local authorization mechanism inside CRM," which `BINDING_SPEC.md` §4 forbids outright ("Do NOT create a second local authorization mechanism inside CRM").

This is not a bug to fix quietly. It's a case where the binding decision mandates a distinction the frozen Phase 1.5 contract cannot express. **No test should be written asserting either 403 or 404 for mutation-endpoint record-level denial until this is resolved** — a test asserting today's actual 403 would encode a contradiction of the plan's own binding text; a test asserting the plan's mandated 404 would fail against real code and could only be made to pass by one of the two blocked changes above.

**Resolved:** owner chose (a). See the resolution doc for the `AuthorizationDenialStage{None,Coarse,Record}` delta design and the CRM-side PEP wiring (denial-stage `Record` → throw `OpportunityNotFoundException`, reusing the existing 404 path with no exception-handler changes).

### Decision 2 — pipeline entry-stage selection rule — RESOLVED: `IsEntry` is the sole source of truth

`BINDING_SPEC.md` §9A: *"Do NOT choose the first stage by sort order unless this behavior is explicitly binding."* `PHASE_2_OPPORTUNITY_COMMANDS_API_PLAN.md` §2.3 resolves that an explicit `IsEntry` flag exists, is exactly-one-per-version (partial unique index), and is assigned during `OpenOpportunity` — it does **not** say which stage gets the flag by default when a version is being built.

`PipelineDefinitionVersion.AddStage`'s actual rule is `isEntry: _stages.Count == 0` — the first stage **added** (insertion order), not the first by `SortOrder` value. This is adjacent to, but not identical to, what §9A forbids: every existing test (`PipelineDefinitionVersionTests.cs`, `ChangePipelineStageHandlerTests.cs`, `GetPipelineStagesHandlerTests.cs`, `GetOpportunityAvailableActionsHandlerTests.cs`) happens to add stages in ascending `SortOrder` sequence, so insertion order and sort order coincide in every case that exists today — no current test can distinguish "first-added" from "first-by-sort-order." (Task 19's own reference test needed its `AddStage` calls swapped mid-implementation specifically to make its `IsEntry` assertion true — recorded in the execution plan's Task 19 annotation — which is the tell that this rule was never independently specified, only made self-consistent.)

The discriminating test would be: `AddStage("Teklif Verildi", sortOrder: 1)` called *before* `AddStage("Bekliyor", sortOrder: 0)`, then assert which one is `IsEntry`. Today that returns the `sortOrder: 1` stage — almost certainly not the intended product behavior for an admin configuring stages out of insertion order. **Not writing this test until the owner confirms whether "first stage added" or "lowest `SortOrder`" is the intended rule** — writing it now would either lock in an unconfirmed accident or need to be deleted once the real rule is settled.

**Resolved:** neither "first-added" nor "lowest `SortOrder`" is the binding rule — `PipelineStage.IsEntry` (the persisted, partial-unique-indexed flag) is the sole source of truth, `SortOrder` is presentation-only. `AddStage`'s current first-stage-default is kept as a creation-time convenience, not the semantic rule. This resolution also surfaces a real code gap not previously identified: `OpenOpportunityHandler.ResolveEntryStageAsync` must reject opening against a pipeline version with no active entry stage (currently silent) — see the resolution doc for the exact fix and the four required test proof-points.

---

## 0. Decision provenance — the three named behaviors

| Behavior | Verdict | Evidence |
|---|---|---|
| Draft → Lost is allowed | **Explicitly binding**, not accidental | `PLAN.md` §6A table, row "`Draft → Lost`": *"`Lose(reason)` also allowed from `Draft`... Target model's diagram shows an abandoned-Draft path as legitimate \| Already consistent, not a gap — worth an explicit product confirmation only."* Verified directly at `PLAN.md:215`. |
| Tenant with no pipeline may `Open` and leave pipeline fields null | **Explicitly binding** | `PLAN.md` §2.3 recommendation + `OpenOpportunityHandler.cs:113` doc comment: *"Returns (null, null) if the tenant has none configured yet — `Open()` already treats that as valid."* `Opportunity.Open`'s own guard only rejects a stage supplied *without* its version — the all-null case is explicitly legal. |
| First stage added becomes entry by default | **Implementation choice, not binding text** — see Decision 2 above | No citation in `PLAN.md` §2.3 or `BINDING_SPEC.md` §9A to this specific default; it originates in `PipelineDefinitionVersion.AddStage`'s `_stages.Count == 0` check, written during Task 19 with no binding-doc reference. |

No accidental behavior is being preserved here merely because a test exists — each row above was checked against the plan doc directly, not inferred from the test suite.

---

## 1. Real, project-acknowledged gap: `OpportunityAuthorizationTests.cs` was never built

This is the single most defensible finding — it isn't sourced from the audit checklist at all, it's the project's **own plan** naming a deliverable that was never delivered.

`PLAN.md` §18 ("Exact files expected to be added") lists:

> `tests/CRM.Tests/Integration/OpportunityAuthorizationTests.cs` (or similar) — coarse/`OwnedBy` authorization integration coverage

`PLAN.md` §16 ("Application" test-plan row) calls for: *"per-command coarse-authorization allow/deny; `OwnedBy` scope allow/deny once §2.2 resolves."*

**What exists today:** every CRM.Tests command-handler test uses `StubAuthorizer.AlwaysAllow`/`AlwaysDeny` — a fixed-answer fake, never the real `AccessAuthorizer` wired to real `RoleAssignment`/`PermissionSetItem` grant data. `OwnedBy` scope is tested **only** for `ListOpportunitiesHandler` (`ListOpportunitiesHandlerTests.OwnedBy_scope_returns_only_that_principals_opportunities`) — no mutation handler has a test proving a real `OwnedBy`-scoped grant allows a mutation when the actor owns the record and denies it when they don't.

This means: **zero tests today prove the real Access module (`AccessAuthorizer` + `AccessScopeResolver`, seeded with real `Account`/`RoleAssignment`/`Role`/`PermissionSet` rows) correctly authorizes or denies an actual CRM Opportunity command end-to-end.** Every "authorization" test in `CRM.Tests/Application/` proves only that the handler *calls* `IAuthorizer` and *reacts* to its answer — not that the real PDP produces the right answer for a real CRM resource.

**Recommendation (P0):** build `tests/CRM.Tests/Integration/OpportunityAuthorizationTests.cs` per the plan's own naming, seeding real Access-module data (`Account`, `ExternalIdentity`, `RoleAssignment`, `Role`, `PermissionSet`, `PermissionSetItem`) and running at least: one command through a tenant-wide grant (allow), one through an `OwnedBy` grant on the caller's own record (allow), one through an `OwnedBy` grant on someone else's record (deny), one with no grant at all (deny). `src/Modules/Access/Application/BootstrapTenantAccessHandler.cs` and `tests/Access.Tests/Integration/GrantRevokeRoleAssignmentHandlerTests.cs` show the existing seeding pattern to reuse — no new infrastructure needed.

---

## 2. Real, untested code path: unprivileged runtime role through the application/handler pipeline

Every existing `CRM.Tests/Application/*` and `CRM.Tests/Integration/WinOpportunityHandlerTests.cs` test uses `PostgresFixture.CreateAdminContext()` — the Testcontainers superuser connection, which unconditionally bypasses RLS. In production, `Program.cs:19` wires `CrmDbContext` to `CrmConnectionString.Resolve()`, which defaults to the unprivileged `fynovio_app` role (`CrmConnectionString.cs`). **No existing test runs a single command handler under the same security posture the real application uses.** The existing RLS proof (`TenantIsolationTests`, `PipelineRlsTests`) tests raw EF queries directly against the runtime role — never a full handler (authorization → idempotency → domain → evidence/outbox → commit) under that role.

**Verified, not assumed:** `PostgresFixture` already exposes exactly what's needed — `RuntimeConnectionStringAsync()` provisions the unprivileged role, `PostgresFixture.CreateContext(connectionString)` is a static factory already used by `TenantIsolationTests`/`PipelineRlsTests`. I ran a throwaway proof-of-concept (`CreateOpportunityHandler` through `PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync())`, inside `CreateOpportunityHandlerTests`) and it passed cleanly (1/1, 285ms) before being reverted — the infrastructure genuinely works end-to-end through a real handler, this isn't a guess.

**Recommendation (P0):** add one full successful mutation path and one full successful query path under the runtime role, reusing this exact pattern. Prefer covering `WinOpportunityHandler` (the template handler, highest-risk — money + evidence + outbox) and `ListOpportunitiesHandler` (the one query with non-trivial scope filtering) rather than every handler, per the checklist's own "prefer covering more than one mutation if the infrastructure differs" — the infrastructure here doesn't differ per handler, so two representative cases (one command, one query) demonstrate the pipeline works; adding all twelve would be duplicate proof of the same fact.

---

## 3. HTTP/Host end-to-end coverage

Current `Host.Tests/OpportunityEndpointsTests.cs` proves exactly two things: unauthenticated → 401, authenticated non-member → 403. That's it — confirmed by direct read, matches the inventory doc.

**Real, binding-documented gap (not invented):** `PLAN.md` §16 ("API" row): *"happy path per endpoint; validation errors; 401/403/404 distinctions; concurrency conflict; idempotent retry; tenant isolation over HTTP (a cross-tenant token must never see another tenant's Opportunity, mirroring the domain-layer `TenantIsolationTests` pattern at the HTTP boundary)."* None of this exists yet. The existing test file's own code comment names this too: *"A full 'authorized happy path' test... Left as the next test to add in this file once Tasks 6-20's fixtures are available to import."* Those fixtures are now available (Phase 2 is complete).

**Exact HTTP surface, verified against `src/Host/Endpoints/OpportunityEndpoints.cs` (12 routes, no invented endpoints):**

```
POST   /opportunities
POST   /opportunities/{id}/lines
POST   /opportunities/{id}/lines/{lineId}/cancel
POST   /opportunities/{id}/open
POST   /opportunities/{id}/stage
POST   /opportunities/{id}/win
POST   /opportunities/{id}/lose
POST   /opportunities/{id}/reassign
GET    /opportunities/{id}
GET    /opportunities
GET    /opportunities/{id}/actions
GET    /pipelines/{versionId}/stages
```

**No "Update Opportunity" endpoint exists, and none is binding-scoped.** `PLAN.md` §8 is explicit: *"No generic `UpdateOpportunity`/PATCH... `EstimatedAmount`/`Currency`/`ExpiryDate`(post-Open)/`CustomFields`/`PartyRef` stay immutable in Phase 2 — deferred."* Do not add tests expecting an Update endpoint.

**Exact error contract, verified against `CrmProblemDetailsExceptionHandler.cs` (matches `PLAN.md` §15 exactly except the Decision-1 gap above):**

| Exception | Status | `type` |
|---|---|---|
| `OpportunityNotFoundException` | 404 | `not_found` |
| `OpportunityAuthorizationDeniedException` | 403 | `forbidden` |
| `OpportunityConcurrencyConflictException` | 409 | `concurrency_conflict` |
| `InvalidPipelineTransitionException` | 409 | `invalid_pipeline_transition` |
| `IdempotencyKeyReusedException` | 409 | `idempotency_key_reused` |
| `ArgumentException` | 400 | `validation_error` |
| `InvalidOperationException` | 409 | `illegal_lifecycle_transition` |

**New finding — `GET /pipelines/{versionId}/stages` performs no action authorization at all.** `GetPipelineStagesHandler`'s constructor is `(CrmDbContext context)` — no `IAuthorizer` dependency, no authorization call anywhere in `HandleAsync` (verified by direct read, `GetPipelineStagesHandler.cs`). `PLAN.md` §9A's own API table assigns this route `crm.opportunity.read` as its `ActionKey` and lists `404` among its possible statuses — but the handler can only ever return `200` with an empty list for a nonexistent or cross-tenant version under RLS; nothing 403s or 404s. This is a real divergence between the plan's own table and the shipped code, distinct from Decision 1, and currently untested at any level.

**Recommendation (P0):** build the happy-path + error-contract HTTP test matrix per `PLAN.md` §16's own list, for the endpoints where the two owner decisions above don't block it (all of them except the specific 403-vs-404-on-record-level-denial assertion). Flag the `GetPipelineStages` authorization gap to the owner alongside the two decisions above — it may need its own small fix (add the missing `IAuthorizer` call) before a meaningful test can assert anything beyond "it returns 200."

---

## 4. Cross-tenant non-leak at HTTP level

Not covered today — `Host.Tests` never seeds a second tenant's Opportunity and probes it. RLS-only tests (`TenantIsolationTests`) prove the DB layer; nothing proves the HTTP layer doesn't leak existence through response shape.

The query side already has the right shape to test against (`GetOpportunityHandler` → `null` → 404, identical for "doesn't exist" and "exists in another tenant," since RLS means the same-tenant-context load simply won't find it — no special-casing). The mutation side is blocked by Decision 1 above for the *specific* record-level-denial status code, but the **nonexistent-ID** case (genuinely no row, any tenant) is not blocked — every mutation handler already throws `OpportunityNotFoundException` → 404 for that, and that path *is* testable today without waiting on Decision 1.

**Recommendation (P0, partial — the part not blocked by Decision 1):** add HTTP-level tests for `GET /opportunities/{id}` against (a) a nonexistent ID and (b) another tenant's real ID — assert identical `404` + identical body shape for both. Defer the mutation-endpoint cross-tenant-existence assertions until Decision 1 is resolved.

---

## 5. Record-level authorization scopes

`AccessScopeResolver.cs`'s own doc comment: *"Phase 1.5 implements exactly three outcomes: `None`, `All`, or `AnyOf([OwnedBy])` (round 3 §8 freeze #10)."* `AccessAuthorizer.cs`'s own doc comment: *"RBAC + tenant-wide scope + `owner` relation only — Phase 1.5's frozen scope (round 3 §8 freeze #10)."* `docs/schema/identity-access-schema.md:22`: *"Organization/Territory scope evaluation... NOT (IMPLEMENT NOW)"*; `:185`: *"Organization-node/Territory scope columns on `RoleAssignment` — added additively once a real fact provider exists."*

**Team, Territory, Organization Unit scopes do not exist in this codebase.** This is a named, frozen, adversarially-reviewed Phase 1.5 decision (`round 3 §8 freeze #10`), not an oversight — `PLAN.md` §1 states it as a table row: *"Sharing / Org / Team / Territory scope \| Frozen out — do not build it in Phase 2."* Writing tests for these would test code that cannot exist without an Architecture Delta, and would violate `BINDING_SPEC.md` §19's own non-goal ("inventing new field-security semantics outside Phase 1.5" — same principle extends to scope semantics).

**What does exist and is legitimately under-tested:** `Own`/`OwnedBy` — see §1 above (List has it; mutations don't, real gap). `All`/`None` scopes are tested for `List` only; no mutation-side test exercises a tenant-wide (`All`-equivalent, i.e. `relation == null`) grant through the real `AccessAuthorizer` either — same gap as §1, same recommended fix.

Composite scope evaluation (multiple `ScopeTerm`s combined in one `AnyOf`) is not a real gap: `ScopeTerm` has exactly one concrete subtype (`OwnedBy`) today, so `AnyOf` can never contain more than one *kind* of term in practice, and `ListOpportunitiesHandler.ApplyAnyOf` already defensively fails closed on any unrecognized term (verified, `ListOpportunitiesHandler.cs:48-55`) — there is nothing to combine yet.

---

## 6. Field-level security

**Does not exist. Confirmed by four independent, converging primary sources, not inference:**

- `src/Contracts/AuthorizationDecision.cs:4-6`: *"`MatchedGrants`/`Obligations` are not here yet: restriction/field-security/step-up don't exist in Phase 1.5 (gap-closure §4)."*
- `docs/schema/identity-access-schema.md:184`: *"Sharing, Field Security, Restriction/forbid policy tables — DESIGN/FREEZE... no schema exists yet."*
- `docs/plans/enterprise-access-foundation/2026-09-17-enterprise-access-foundation-review-round3-final.md`, Freeze List item 14: Sharing + Field Security are named Access subdomains, explicitly not implemented.
- `PLAN.md` §1 table: *"Field-Level Security (READ/WRITE) \| Frozen out — do not build it in Phase 2."* §14 step 7 restates: *"not applicable — frozen out (§1)."*

`BINDING_SPEC.md` §4B's requirement ("Phase 2 MUST inspect and consume Phase 1.5 Field-Level Security **if it exists**") is conditional, and the condition resolves false — this was independently verified, not silently skipped. There is no protected-field concept, no masking, no omission-on-read anywhere in the CRM module or its DTOs (`OpportunityDto`, `OpportunitySummaryDto` expose every field unconditionally). **No FLS tests should be written; there is nothing to test.**

---

## 7. Policy decisions / obligations / approval

**Does not exist. `AuthorizationEffect` (`src/Contracts/AuthorizationEffect.cs`) has exactly two enum values: `Deny`, `Allow`.** There is no third value the type system could even carry `RequireApproval` in without a breaking change to the shared contract every module depends on. `PLAN.md` §1: *"Policy obligations / `RequireApproval` / masking / limits \| Frozen out — no runtime exists."* No `Workflow`/`Rules`/`HumanTask`/`Approval` module exists anywhere under `src/Modules/` (confirmed by directory listing).

The proposed-vs-current-state evaluation question (`BINDING_SPEC.md` §4A's amount-threshold example) is **vacuously satisfied**, per `PLAN.md` §1's own closing row: `ResourceDescriptor` has no slot for a proposed value, and no Phase 1.5 grant depends on one — there is no rule to violate. **No approval/obligation tests should be written; nothing produces those outcomes today, and inventing the rule in Phase 2 would violate `BINDING_SPEC.md` §19's explicit non-goal against redesigning Phase 1.5's policy engine.**

---

## 8. Available Actions / Available Transitions

`GetOpportunityAvailableActionsHandler`'s own doc comment confirms the checklist's closing "Remember" bullet is already true in code, not just in principle: *"this is a UX aid only (binding spec §13A); `WinOpportunityHandler` etc. re-check both independently at execution time regardless of what this query said."* Verified — every mutation handler does its own independent `AuthorizeAsync` call; the projection is never trusted as authoritative.

**Real, currently untested gaps (verified against `GetOpportunityAvailableActionsHandler.cs` directly, not invented):**

- **Terminal states (Won/Lost) are never tested.** All three existing tests cover Draft and Open only. The handler's logic (`canOpen`/`canChangeStage`/`canWin` all gated on `Status == Draft` or `Status == Open`; `canLose` excludes Won/Lost; `canReassign` excludes Won/Lost) implies a Won or Lost opportunity should report every action `false` — nothing proves this today.
- **Open-with-no-billable-line is never tested.** `canWin` requires `opportunity.Lines.Any(l => !l.IsOptional && !l.IsCanceled)` in addition to `Status == Open` — no test distinguishes "Open but nothing billable" (`canWin=false`) from "Open with a billable line" (`canWin=true`); the one existing Open-state test always seeds a billable line.
- **`AllowedTargetStageIds` excluding an inactive stage is never directly tested.** The query correctly filters `s.IsActive` (`GetOpportunityAvailableActionsHandler.cs:44`), but no seeded scenario includes an inactive stage alongside active ones to prove the filter does anything — the one multi-stage test (`ChangePipelineStageHandlerTests.SeedAsync`) seeds an inactive stage but that's a different handler/test class.

These are concrete, code-verified omissions in the existing three-test file, not invented scope. **Recommendation (P1):** add the three cases above to `GetOpportunityAvailableActionsHandlerTests.cs`.

---

## 9. Pipeline entry-stage edge cases and transition matrix

**Entry-stage edge cases — real, code-verified gap, paired findings:**

`ChangePipelineStageHandler.cs:70-73` rejects a target stage that is `!IsActive` — §2.4's resolved rule is correctly enforced *for stage changes*. But `OpenOpportunityHandler.ResolveEntryStageAsync` (`OpenOpportunityHandler.cs:133-136`) filters only `s.IsEntry`, **never `s.IsActive`**, when auto-assigning the entry stage on `Open()`. If the tenant's entry-flagged stage has since been deactivated, `OpenOpportunity` will silently assign an inactive stage to a newly-opened Opportunity — the exact same stage `ChangePipelineStage` would refuse to let anyone move *to*. This is an inconsistency between the implementations of two adjacent resolved decisions (§2.3 and §2.4), and it is exactly the question `BINDING_SPEC.md` §9A names and leaves open: *"What happens if the configured entry stage is disabled?"* No test today exercises this scenario in either direction. **P1, report-and-ask rather than silently fix** — it may be intended (auto-open even with a "stale" entry configuration) or a real defect; that's the owner's call, not an audit call.

Two further, real, testable-today edge cases from `OpenOpportunityHandler.ResolveEntryStageAsync`'s actual logic (verified by direct read, not inferred):

- **Multiple `PipelineDefinition`s per tenant:** the resolver always picks the oldest (`OrderBy(p => p.Id).FirstOrDefault`) — this is an explicit, documented implementation choice ("per this plan's own scope note"), not underspecified, and is a good candidate for a regression-lock test (two pipelines seeded, assert the older one's stage is used).
- **Version pinning:** nothing re-resolves an already-`Open` Opportunity's pipeline stage/version if the tenant's `PipelineDefinitionVersion` later gets a new version — this is true by construction (no handler ever touches `PipelineDefinitionVersionId` after `Open`, and `ChangePipelineStage` validates the target belongs to the *opportunity's current* version, never the tenant's latest), but not explicitly regression-locked by a test that seeds a second version after opening and asserts the Opportunity keeps its original one.

**Transition matrix — explicitly resolved, not a gap:** `ChangePipelineStageHandler.cs`'s own doc comment: *"architecture plan §7/§9 OD#4: unrestricted within the current pipeline version's active stages — no transition matrix."* Backward transitions, skip-stage transitions, and any active-stage-to-any-other-active-stage-in-the-same-version transition are all equally legal by explicit design — there is no matrix to test against, forbidden-transition tests would test a rule that doesn't exist. The one untested edge inside this resolved model: **same-stage → same-stage** (`ChangeStage(currentStageId)`). `Opportunity.ChangeStage` has no guard against this — it's presumably a legal no-op, but nothing proves it doesn't corrupt `RowVersion`/evidence/outbox in some unintended way (e.g. does it still write an outbox event for a stage change that didn't change anything?). **P1, real and cheap to close.**

**Recommendation (P1):** four targeted tests — entry-stage-inactive-on-open (paired with the owner's answer to whether this is intended), multi-pipeline oldest-wins regression lock, version-pinning-after-new-version-created, same-stage-to-same-stage no-op behavior.

---

## Summary — what's real vs. what's not

**Real gaps, worth closing (in priority order — both former blockers are now resolved, see `docs/architecture-analysis/2026-09-19-crm-phase2-authorization-delta-and-entry-stage-resolution.md`):**
1. `AuthorizationDenialStage` delta (`Contracts` + `AccessAuthorizer`) + CRM mutation-handler PEP wiring — Decision 1's concrete fix. (P0)
2. `OpportunityAuthorizationTests.cs` — named in the project's own plan (§18), never built; now also the right place to assert Decision 1's 403-vs-404 behavior end to end. (P0)
3. `OpenOpportunityHandler.ResolveEntryStageAsync` fix (filter `IsActive`, throw on missing/inactive entry stage instead of silent null) + `PipelineConfigurationInvalidException` — Decision 2's concrete fix. (P0)
4. Runtime-role coverage through the real handler pipeline — infrastructure exists, verified working, zero tests use it outside raw-SQL RLS checks. (P0)
5. HTTP happy-path + error-contract matrix per `PLAN.md` §16, including the now-unblocked cross-tenant/record-scope cases. (P0)
6. Cross-tenant non-leak at HTTP level, full matrix (nonexistent ID + another tenant's real ID, now both unblocked). (P0)
7. `GetPipelineStages` missing authorization call — needs the `crm.opportunity.read` check added, then a test. (P0)
8. `GetOpportunityAvailableActionsHandler` — terminal states, no-billable-line Open state, inactive-stage exclusion. (P1)
9. Entry-stage decision-2 test proof-points (lower-sortOrder-doesn't-move-entry, multi-pipeline tie-break, version pinning, same-stage transition). (P1)

**Not real — do not build, confirmed by named, frozen Phase 1.5 decisions:**
- Field-level security (READ or WRITE) — §6.
- Policy obligations / `RequireApproval` / masking / conditional decisions — §7.
- Team / Territory / Organization Unit access scopes — §5.
- Authorization evaluating proposed-vs-current mutation values — vacuously N/A, no rule exists to violate.
- Update Opportunity HTTP capability — explicitly out of scope (`PLAN.md` §8).
- Pipeline transition matrix / forbidden-transition rules — explicitly resolved as "none" (OD#4).

Writing tests for the "not real" bucket would violate `BINDING_SPEC.md` §19's own non-goal list and would test code that cannot exist without an Architecture Delta the owner hasn't approved.
