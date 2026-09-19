# CRM Phase 2 — Owner Decisions: Authorization Delta & Pipeline Entry-Stage Resolution

**Status:** RESOLVED by owner 2026-09-19. Both decisions were raised as blockers in `docs/plans/crm-phase2/2026-09-19-crm-phase2-test-gap-audit.md` ("Two owner decisions needed before any test (or code) touches these paths") and are closed here.

---

## ARCHITECTURE DELTA — Phase 1.5 Authorization Contract

### Resolution

**Option (a): make a minimal Architecture Delta to Phase 1.5's authorization contract.** Do not relax the tenant-non-leak rule for mutations, and do not let CRM derive the coarse-vs-record distinction itself.

### Binding HTTP semantics (restated, now binding)

| Situation | Status |
|---|---|
| Unauthenticated caller | 401 |
| Authenticated, lacks the coarse action/capability entirely | 403 |
| Passes coarse authorization, record does not exist | 404 |
| Passes coarse authorization, record exists, record-level policy/scope denies | 404 — externally indistinguishable from "does not exist" |

CRM must never produce this distinction by comparing `AssignedPrincipal == Actor`, evaluating owner/team/territory itself, or any other CRM-local reconstruction of an authorization outcome — that would be a second authorization implementation inside CRM, which is forbidden. The distinction must come from Phase 1.5's own PDP, because Phase 1.5 already computes it internally and simply doesn't expose it.

### Why this is safe as a *minimal* delta

`AccessAuthorizer.AuthorizeAsync` (`src/Modules/Access/Application/AccessAuthorizer.cs`) already computes, in order:

1. Is the action registered at all? (`action_not_registered`)
2. Is the principal resolvable? (`principal_not_recognized`)
3. Load `items` = every `PermissionSetItem` reachable from the actor's active `RoleAssignment`s for this action.
4. If any `item.Relation is null` → tenant-wide grant → **Allow**.
5. If any `item.Relation == OwnerRelation` and it matches the resource's owner → **Allow**.
6. Otherwise → **Deny** (`no_matching_grant`).

Step 6 is the ambiguous one: it fires both when `items` is **empty** (the actor has no grant for this action at all — a coarse denial) and when `items` is **non-empty but none of them matched this resource** (the actor has an owner-scoped grant, it just isn't for this record — a record-level denial). The PDP already has this distinction in hand at the moment it returns `Deny`; it just isn't surfaced. No new business rule is invented by exposing it.

### Concrete shape

Add one new enum to `Contracts`, additive to the existing `AuthorizationDecision` record struct (no existing field removed or renamed, `AuthorizationEffect` stays exactly `{Deny, Allow}`):

```csharp
namespace Contracts;

public enum AuthorizationDenialStage
{
    None,    // Effect == Allow; this field is meaningless on an Allow decision
    Coarse,  // action not registered, principal unrecognized, or zero grants exist for this action at all
    Record   // at least one grant for this action exists, but none of them cover this specific record
}
```

`AuthorizationDecision` gains `public AuthorizationDenialStage DenialStage { get; }`, defaulting to `None`, populated by the constructor (or a new overload — implementation detail for whoever picks this up, not a further open decision).

`AccessAuthorizer.AuthorizeAsync` changes only at the points that already return `Deny`:

- `action_not_registered` → `DenialStage.Coarse`
- `principal_not_recognized` → `DenialStage.Coarse`
- `no_matching_grant` **when `items.Count == 0`** → `DenialStage.Coarse`
- `no_matching_grant` **when `items.Count > 0` but none matched** → `DenialStage.Record`

No change to `AccessScopeResolver`, `IAuthorizer`'s method signature, `ResourceDescriptor`, or any query-side code. `AuthorizeResolveAccessScopeEquivalenceTests` (the frozen round-3 §5 invariant test) is unaffected — it tests `Allow`/scope equivalence, not denial-stage detail.

### CRM-side wiring — refined 2026-09-19 to preserve internal diagnostic semantics

**Revision note:** the original version of this document proposed throwing `OpportunityNotFoundException` directly for a `Record`-stage denial. Owner review flagged that this collapses the internal exception *type* itself to "not found," losing the ability to tell "genuinely missing resource" apart from "resource exists, record-level access denied" in logs/evidence/telemetry — even though the two must still be externally indistinguishable over HTTP. Revised design below keeps the distinct exception type and only collapses the **external ProblemDetails shape**.

`OpportunityAuthorizationDeniedException` gains two fields, `AuthorizationDenialStage DenialStage` and `long? OpportunityId` (null only for `CreateOpportunity`, where `ResourceDescriptor.Id` is null and `DenialStage` can therefore only ever be `Coarse`):

```csharp
public sealed class OpportunityAuthorizationDeniedException : InvalidOperationException
{
    public string ActionKey { get; }
    public string ReasonCode { get; }
    public AuthorizationDenialStage DenialStage { get; }
    public long? OpportunityId { get; }

    public OpportunityAuthorizationDeniedException(string actionKey, string reasonCode, AuthorizationDenialStage denialStage, long? opportunityId = null)
        : base($"Action '{actionKey}' was denied ({reasonCode}).")
    {
        ActionKey = actionKey;
        ReasonCode = reasonCode;
        DenialStage = denialStage;
        OpportunityId = opportunityId;
    }
}
```

Every mutation handler's denial branch becomes:

```csharp
if (!decision.IsAllowed)
    throw new OpportunityAuthorizationDeniedException(ActionKeyValue, decision.ReasonCode, decision.DenialStage, opportunity.Id);
```

(`CreateOpportunityHandler` omits the trailing `opportunity.Id` — there is no resource yet.)

The exception **type stays `OpportunityAuthorizationDeniedException` in both cases** — full internal semantics (which action, which reason code, which evaluation stage, which resource) are always retrievable from the thrown exception for logging/telemetry. Only `CrmProblemDetailsExceptionHandler`'s external mapping collapses the two cases:

```csharp
var (status, type, title) = exception switch
{
    // Record-level denial must be externally indistinguishable from a genuinely missing
    // resource (tenant non-leak rule) — same status, same type, and the SAME title text
    // OpportunityNotFoundException would produce, not exception.Message (which would leak
    // "was denied" instead of "was not found"). DenialStage.Record only ever occurs once a
    // resource was already loaded, so OpportunityId is always set here.
    OpportunityAuthorizationDeniedException { DenialStage: AuthorizationDenialStage.Record } ex =>
        (StatusCodes.Status404NotFound, "not_found", $"Opportunity {ex.OpportunityId} was not found."),
    OpportunityNotFoundException => (StatusCodes.Status404NotFound, "not_found", exception.Message),
    OpportunityAuthorizationDeniedException => (StatusCodes.Status403Forbidden, "forbidden", exception.Message),
    OpportunityConcurrencyConflictException => (StatusCodes.Status409Conflict, "concurrency_conflict", exception.Message),
    InvalidPipelineTransitionException => (StatusCodes.Status409Conflict, "invalid_pipeline_transition", exception.Message),
    IdempotencyKeyReusedException => (StatusCodes.Status409Conflict, "idempotency_key_reused", exception.Message),
    ArgumentException => (StatusCodes.Status400BadRequest, "validation_error", exception.Message),
    InvalidOperationException => (StatusCodes.Status409Conflict, "illegal_lifecycle_transition", exception.Message),
    _ => (0, null, null)
};
```

**Why the title matters:** the handler previously used `Title = exception.Message` uniformly. If a `Record`-stage denial kept `OpportunityAuthorizationDeniedException`'s own message ("Action 'crm.opportunity.win' was denied..."), the response body itself would leak that the record exists, even at the same 404 status — defeating the entire point of the non-leak rule. Constructing the title identically to `OpportunityNotFoundException`'s own message format closes that.

This is preference (1) from the owner's stated order: existing exception infrastructure (`OpportunityAuthorizationDeniedException`), extended additively, whose *external* mapping (not its internal type) does the collapsing — no new exception type, no new abstraction. CRM is not computing anything here — it's a Policy Enforcement Point branching on a value Phase 1.5's PDP already authoritatively decided; that is what a PEP does, not a second PDP.

`GetOpportunityAvailableActionsHandler` needs the equivalent treatment for consistency (it already returns `null` on "not found," and per-action results are already boolean — a `Record`-stage denial on any individual action's `Authorize(...)` call should simply make that action `false`, which is already the correct behavior with no code change; the endpoint's own "whole record" 404 path is unaffected).

`GetOpportunityHandler` (query side) needs **no change** — it already collapses any denial (coarse or record) to `null → 404`, which already matches the non-leak rule; `DenialStage` is irrelevant there since both stages already produce the same outward result.

### Scope of this delta

Applies to every module that consumes `IAuthorizer`/`AuthorizationDecision`, not just CRM — but no other module currently branches on denial reason in a way this changes; this is additive and backward compatible for any caller that ignores the new field.

**Explicitly out of scope for this delta** (do not let it grow): no new `AuthorizationEffect` value, no obligations/approval semantics, no field-level security, no scope terms beyond `OwnedBy`/`All`/`None`. Those remain frozen per `PLAN.md` §1, unaffected by this change.

### Required follow-up work (P0, now unblocked)

1. Implement the `AuthorizationDenialStage` addition in `Contracts` + `AccessAuthorizer`.
2. Update every CRM mutation handler's denial branch to the two-way throw above (`CreateOpportunityHandler` has no resource to be "record-denied" against — `ResourceDescriptor.Id = null` for CREATE — so it only ever gets `Coarse`; every other mutation handler needs the change).
3. `tests/Access.Tests/` — unit-test `AccessAuthorizer` directly: a request with zero grants for the action → `DenialStage.Coarse`; a request with an `OwnedBy` grant that doesn't match the resource → `DenialStage.Record`.
4. `tests/CRM.Tests/Integration/OpportunityAuthorizationTests.cs` (the file already identified as missing in the test-gap audit) — extend its planned coverage to assert the HTTP-visible consequence: a coarse denial on a real command → 403; an `OwnedBy`-scoped grant on someone else's record → 404, identical body shape to a genuinely nonexistent ID.
5. `Host.Tests` — the cross-tenant/record-scope HTTP tests deferred in the audit's §3/§4 are now unblocked.

---

## OPEN DECISION RESOLUTION — Pipeline Entry Stage Semantics

### Resolution

**Entry stage is explicit pipeline configuration. `PipelineStage.IsEntry` is the sole source of truth.** Neither "first stage added" nor "lowest `SortOrder`" is the binding definition — `SortOrder` is presentation/process ordering only and must never be read as an entry-stage signal anywhere in the codebase.

### Invariant (binding)

For any `PipelineDefinitionVersion` a tenant is actively using to open an Opportunity against:

> Exactly one usable (active, correctly-scoped) entry stage must exist. If the tenant's pipeline configuration cannot produce one, opening an Opportunity against that pipeline must fail loudly — never silently proceed with a missing, inactive, or otherwise invalid entry stage.

This is a **different case** from "tenant has no pipeline configured at all," which remains legal per the existing §2.3 decision (`Opportunity.Open` already accepts null version/stage, and this stays true). The two cases must be told apart:

| Case | Outcome |
|---|---|
| Tenant has zero `PipelineDefinition`s | Legal — `Open` proceeds with `PipelineDefinitionVersionId = null`, `PipelineStageId = null` (existing, unchanged behavior) |
| Tenant's `PipelineDefinition` has zero versions | Legal, same as above — nothing to open against yet |
| A version exists but has **zero** stages flagged `IsEntry` | **Error** — configuration/domain conflict, must not silently open with a null stage |
| A version exists and its entry stage is `IsActive == false` | **Error** — not a usable entry stage |
| (Structurally excluded already) an entry stage belonging to a different version | Cannot occur — the query is already scoped to the resolved version |

### Existing-Opportunity pinning (restated, now binding — was already true by construction, now explicit)

Once an Opportunity is opened, its `PipelineDefinitionVersionId`/`PipelineStageId` are pinned, history-safe references. None of the following may retroactively move an already-open Opportunity:

- The entry stage changing (via `MarkEntry`) on the version the Opportunity is already pinned to.
- A stage being reordered (not currently a supported operation at all — no domain method mutates `SortOrder` post-creation, so this is presently vacuous, not a gap to build).
- A new `PipelineDefinitionVersion` being published for the same `PipelineDefinition`.

This already holds today: no handler re-resolves an open Opportunity's pipeline fields after `Open()`, and `ChangePipelineStage` only validates the target against the Opportunity's *current* version, never the tenant's latest. This is a regression-lock requirement, not new behavior to build.

### Disposition of `PipelineDefinitionVersion.AddStage`'s current `_stages.Count == 0 => IsEntry` default

**Kept, reclassified as a creation-time convenience default only — not the semantic rule.** `IsEntry` (the persisted flag, partial-unique-indexed) remains the only thing any query or command ever reads. `AddStage`'s default merely saves a caller from an extra `MarkEntry` call when building a version's very first stage; it carries no meaning once more than one stage exists, and nothing may ever infer entry status from sort order or insertion order at read time.

`MarkEntry` already exists (`PipelineDefinitionVersion.cs`) as the explicit configuration operation, including its documented safe two-phase persistence pattern (`PipelineConstraintTests.MarkEntry_persisted_via_the_safe_two_phase_pattern_moves_entry_either_direction` already proves this at the persistence layer) — no new API is needed for decision 2's mechanism, only the test coverage below and the `OpenOpportunityHandler` fix that follows from the invariant above.

### Required follow-up work

**Code fix (P0 — this is a real implementation gap the invariant above creates, not just a missing test):** `OpenOpportunityHandler.ResolveEntryStageAsync` currently:

```csharp
var stageId = await context.PipelineStages
    .Where(s => s.TenantId == tenantId && s.PipelineDefinitionVersionId == resolvedVersionId && s.IsEntry)
    .Select(s => (long?)s.Id)
    .SingleOrDefaultAsync(cancellationToken);

return (resolvedVersionId, stageId);
```

This does **not** filter `IsActive`, and silently returns `stageId = null` (while still returning a non-null `resolvedVersionId`) when no `IsEntry` row exists — both violate the invariant above. Needs: filter `s.IsActive` in the same `Where`, and when a version was resolved (tenant *does* have a configured pipeline) but no active entry stage exists, throw a new domain-conflict exception rather than returning `(resolvedVersionId, null)`. A new exception type following the existing convention (`src/Modules/CRM/Application/*Exception.cs`) — suggested name `PipelineConfigurationInvalidException`, mapped in `CrmProblemDetailsExceptionHandler` to `409 "invalid_pipeline_configuration"` (a domain conflict, same status family as the existing pipeline/lifecycle conflict types, not a new class of error).

**Tests required to prove the resolution (P0/P1, extends the audit's §9 list), all four must exist before this is considered closed:**

1. First stage added to a fresh version becomes entry by default (already covered — `PipelineDefinitionVersionTests.AddStage_the_first_stage_added_becomes_entry_by_default`, regression-lock only).
2. Adding a stage with a **lower** `SortOrder` than the existing entry stage does **not** move entry to it (new — this is the discriminating test the audit flagged; today's code already passes it *for the reason stated in the resolution* — entry is decided once at first-add, never revisited by later `AddStage` calls regardless of `SortOrder` — but nothing currently asserts it).
3. `MarkEntry` explicitly moves entry to a target stage, and only that stage (already covered — `PipelineDefinitionVersionTests.MarkEntry_moves_the_entry_flag_to_the_target_stage_only`, regression-lock only).
4. **New, required by the code fix above:** `OpenOpportunityHandler` rejects opening against a version whose entry stage is inactive, and rejects opening against a version that has no entry stage at all — both as an explicit error, not a silent null-stage open. This is the test that did not exist before this resolution and directly exercises the invariant.

---

## Disposition of the test-gap audit

`docs/plans/crm-phase2/2026-09-19-crm-phase2-test-gap-audit.md`'s "Two owner decisions needed before any test (or code) touches these paths" section is superseded by this document. The audit's P0/P1 gap list otherwise stands; item 5 (`GetPipelineStages` missing authorization) and the §9 entry-stage items are now concrete, unblocked work items per the resolutions above rather than open questions.
