# Enterprise Access Foundation — Phase 1.5 Execution Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use subagent-driven-development
> (recommended) or executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking. **Standard (see `CLAUDE.md` "Plan checkbox
> tracking"):** mark a step `[x]` only once its commit exists, and add a
> `→ Commit: \`<hash>\` "<message>"` line under it — never mark ahead of actual state.
>
> **Status: All 10 tasks done (2026-09-17), branch `access-phase1-5-foundation`, not
> yet merged.** Owner approved via "onay veriyorum devam edebilirsin" (Task 0).
> Executed inline in the main session (executing-plans, not subagent-driven-
> development) — subagents on this account are forced to Haiku, weak for the
> judgment-heavy work this foundation required (project-status memory). Final state:
> `dotnet build` clean (0/0), `dotnet format --verify-no-changes` clean,
> `dotnet ef migrations has-pending-model-changes` reports none, full suite
> **109/109** (Access 33 + CRM 46 + MasterData 30, no regression). **Three real bugs**
> were found only by running against a real Postgres container (not visible from
> code review): `role_permission_sets` was missing `tenant_id` entirely (RLS
> migration failed); `Contracts.ActionKey`'s own regex rejected the underscores in
> its own production keys (`access.role_assignment.grant`); test fixtures needed the
> action registry seeded before bootstrapping, matching what `Host` does at startup.
> All three fixed in place — see commit `77a3ea4`. **Two deviations from the plan's
> exact task boundaries**, both for build-safety (never a broken-build intermediate
> commit, per this repo's own convention): Tasks 1–3 (Contracts primitives, Access
> domain rebuild, persistence configs + migration) were combined into one commit
> (`ac97550`) because the Persistence layer can't compile against the old
> `Permission`/`RolePermission` types once the domain model changes them out from
> under it; Task 5's two RLS migrations collapsed into Task 4's single one, since
> Task 3's migration already created all ten tables in one pass (see `010b807`'s
> migration for the full table list).
>
> **Read before starting, in order:**
> 1. `docs/plans/2026-09-17-enterprise-access-foundation-review.md` (round 1 — full
>    benchmark research, target data model rationale, freeze list)
> 2. `docs/plans/2026-09-17-enterprise-access-foundation-review-round2.md` (round 2 —
>    reconciliation against the owner's revised PDF)
> 3. `docs/plans/2026-09-17-enterprise-access-foundation-review-round3-final.md`
>    (round 3 — closure matrix, final ownership matrix, final pipeline)
> 4. `docs/plans/2026-09-17-enterprise-access-owner-decisions-final.md` (round 4 —
>    the two owner decisions: system catalog template→tenant-local, Opportunity
>    Owner = AssignedPrincipal + explicit Reassign)
> 5. `docs/plans/2026-09-17-enterprise-access-foundation-gap-closure.md` (HOW-level
>    technical decisions this plan is built on — RoleAssignment scope removal, text
>    ActionKey PK, Contracts surface, escalation guard, **the bootstrap problem**)
>
> **Do not re-litigate any decision recorded in those five files.** This plan
> implements exactly their frozen scope — no more, no less (Scope Lock, round 3 §9).

**Goal:** Rebuild `src/Modules/Access/` from a bare Role→Permission schema into the
Enterprise Access Control Plane foundation: Contracts-level authorization primitives,
`ActionRegistry`/`PermissionSet`/`Role`/`RoleAssignment`/`TenantAccessState`, a
default-deny PDP (`IAuthorizer`) and query-scope resolver (`IAccessScopeResolver`),
Grant/Revoke commands with the full binding-core discipline (RLS, tenant-safe FKs,
idempotency, evidence, outbox), and a bootstrap path for a tenant's first grant. Bring
Access to the same production-hardening standard CRM/MasterData already have.

**Explicitly OUT of scope for Phase 1.5** (per the five source documents — DESIGN/FREEZE,
not IMPLEMENT NOW):
- Organization/Territory scope evaluation, Team relation, Sharing, Field Security
  runtime, Restriction/forbid policies, ServicePrincipal/Agent runtime, ActingFor/
  impersonation, Direct PermissionSet assignment, Delegation, JIT, Break-glass, SoD,
  Access Review, Entitlements, Approval runtime, continuous template reconciliation.
- **No CRM code changes of any kind.** `Opportunity.Reassign()` and any authorization
  enforcement on CRM commands are Phase 2. This plan touches only `Contracts`,
  `src/Modules/Access/`, `src/Host/Program.cs`, `scripts/create-runtime-role.sql`, and
  `tests/Access.Tests/`.
- No HTTP/API surface for Access. Everything here is an in-process contract + handler,
  consumed directly by tests (and, in Phase 2, by CRM's command handlers).
- No admin UI, no granular per-admin delegation allowlist (gap-closure §5) — the only
  guard is "you must hold `access.role_assignment.grant`/`.revoke` yourself."

If a task reveals a need to build something on the DESIGN/FREEZE list: **STOP → write
an Architecture Delta note → get owner approval → update this plan's scope** (round 3
§9). Do not silently expand it.

---

## Task 0: Pre-flight — confirm the five source documents

**No code in this task.**

- [x] **Step 1:** Read all five documents listed in the header above, in order.
- [x] **Step 2:** Confirm with the platform owner that no new information since round 4
  changes the two remaining owner-input items (system catalog template lifecycle —
  OPEN, not needed for this plan; CRM Opportunity owner field mapping — resolved as
  `AssignedPrincipal`, not touched by this plan).
- [x] **Step 3:** Confirm the gap-closure document's 9 HOW-decisions (RoleAssignment
  scope removal, text ActionKey PK, runtime-not-CI registry enforcement, minimal
  Contracts surface, self-check escalation guard, bootstrap handler, `relation="owner"`
  only, Access-internal `PrincipalType`, CRM-owner-is-Phase-2) as the concrete basis
  for every task below.

→ **Confirmed by Ibrahim (2026-09-17):** "onay veriyorum devam edebilirsin" — approved
as written, proceeding to Task 1.

---

## Task 1: Contracts — authorization primitives
→ Commit: `ac97550` "feat(access): rebuild domain model + persistence configs + RebuildAccessAuthorizationModel migration" (combined with Tasks 2–3, see status banner)

**Files:**
- Create: `src/Contracts/ActionKey.cs`
- Create: `src/Contracts/ActorContext.cs`
- Create: `src/Contracts/ResourceDescriptor.cs`
- Create: `src/Contracts/AuthorizationEffect.cs`
- Create: `src/Contracts/AuthorizationDecision.cs`
- Create: `src/Contracts/AuthorizationRequest.cs`
- Create: `src/Contracts/IAuthorizer.cs`
- Create: `src/Contracts/AccessScope.cs`
- Create: `src/Contracts/ScopeTerm.cs`
- Create: `src/Contracts/IAccessScopeResolver.cs`
- Create: `src/Contracts/IActionCatalog.cs`
- Test: `tests/Access.Tests/Domain/ActionKeyTests.cs` (this test project doesn't exist
  yet — Task 9 creates the `.csproj`; this file is added now and picked up once that
  project exists. If your workflow requires the project to compile at every commit,
  move this one test file's creation into Task 9 Step 1 instead and skip it here.)

No ORM, no I/O — matches `Contracts`'s existing rule (doc 08's "no ORM, I/O, policy
evaluation or service locator" row; `AGENTS.md` Architecture Rules).

- [x] **Step 1: `ActionKey`**

```csharp
// src/Contracts/ActionKey.cs
using System.Text.RegularExpressions;

namespace Contracts;

/// <summary>Stable business-action vocabulary key, owned by the module that names it
/// (e.g. `crm.opportunity.win`) and evaluated by Access. Immutable once published —
/// a rename is a deprecate-and-introduce, never an in-place edit (gap-closure §2).</summary>
public readonly partial record struct ActionKey
{
    public string Value { get; }

    public ActionKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", nameof(value));
        if (!Format().IsMatch(value))
            throw new ArgumentException(
                "ActionKey must be at least three lowercase dot-separated segments, e.g. 'crm.opportunity.win'.",
                nameof(value));

        Value = value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*){2,}$")]
    private static partial Regex Format();
}
```

- [x] **Step 2: `ActorContext`**

```csharp
// src/Contracts/ActorContext.cs
namespace Contracts;

/// <summary>The trusted, per-request actor — built once from the authenticated
/// session, never from a caller-supplied command body (round 3 §4 final pipeline,
/// step 2-3). Deliberately minimal: no `ActingFor`/`ImpersonatedBy` yet — those are
/// DESIGN/FREEZE (gap-closure §4) until a real on-behalf-of scenario exists.</summary>
public readonly record struct ActorContext
{
    public TenantId TenantId { get; }
    public PrincipalRef Principal { get; }
    public Guid CorrelationId { get; }

    public ActorContext(TenantId tenantId, PrincipalRef principal, Guid correlationId)
    {
        TenantId = tenantId;
        Principal = principal;
        CorrelationId = correlationId;
    }
}
```

- [x] **Step 3: `ResourceDescriptor`**

```csharp
// src/Contracts/ResourceDescriptor.cs
namespace Contracts;

/// <summary>What the PEP knows about the resource being authorized — supplied by the
/// owning domain, never read by Access from a private table (round 3 §10 final
/// invariant). `Id` is null for CREATE actions (round 3 §11). `OwnerPrincipal` is the
/// only relationship fact Phase 1.5 evaluates (`relation="owner"`, gap-closure §7);
/// org/territory/attributes are added when those fact providers exist, not reserved
/// empty today (gap-closure §4).</summary>
public readonly record struct ResourceDescriptor
{
    public string ResourceType { get; }
    public long? Id { get; }
    public PrincipalRef? OwnerPrincipal { get; }

    public ResourceDescriptor(string resourceType, long? id, PrincipalRef? ownerPrincipal)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("Resource type is required.", nameof(resourceType));

        ResourceType = resourceType;
        Id = id;
        OwnerPrincipal = ownerPrincipal;
    }
}
```

- [x] **Step 4: `AuthorizationEffect`, `AuthorizationDecision`, `AuthorizationRequest`**

```csharp
// src/Contracts/AuthorizationEffect.cs
namespace Contracts;

public enum AuthorizationEffect
{
    Deny,
    Allow
}
```

```csharp
// src/Contracts/AuthorizationDecision.cs
namespace Contracts;

/// <summary>`Revision` is the `TenantAccessRevision` the decision was computed against
/// (round 3 §7/§8 freeze #17) — never a second, independently-invented counter.
/// `MatchedGrants`/`Obligations` are not here yet: restriction/field-security/step-up
/// don't exist in Phase 1.5 (gap-closure §4).</summary>
public readonly record struct AuthorizationDecision
{
    public AuthorizationEffect Effect { get; }
    public string ReasonCode { get; }
    public Guid DecisionId { get; }
    public long Revision { get; }

    public AuthorizationDecision(AuthorizationEffect effect, string reasonCode, Guid decisionId, long revision)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
            throw new ArgumentException("Reason code is required.", nameof(reasonCode));

        Effect = effect;
        ReasonCode = reasonCode;
        DecisionId = decisionId;
        Revision = revision;
    }

    public bool IsAllowed => Effect == AuthorizationEffect.Allow;
}
```

```csharp
// src/Contracts/AuthorizationRequest.cs
namespace Contracts;

public readonly record struct AuthorizationRequest
{
    public ActorContext Actor { get; }
    public ActionKey Action { get; }
    public ResourceDescriptor Resource { get; }

    public AuthorizationRequest(ActorContext actor, ActionKey action, ResourceDescriptor resource)
    {
        Actor = actor;
        Action = action;
        Resource = resource;
    }
}
```

- [x] **Step 5: `IAuthorizer`**

```csharp
// src/Contracts/IAuthorizer.cs
namespace Contracts;

/// <summary>The single-resource authorization contract (round 1 decision #28 — kept
/// separate from `IAccessScopeResolver`, which answers the collection/query question).</summary>
public interface IAuthorizer
{
    Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken = default);
}
```

- [x] **Step 6: `AccessScope`, `ScopeTerm`, `IAccessScopeResolver`**

```csharp
// src/Contracts/ScopeTerm.cs
namespace Contracts;

/// <summary>A closed term in an `AccessScope.AnyOf` union. Phase 1.5 implements only
/// `OwnedBy` (round 1 §10.2, round 3 §8 IMPLEMENT NOW #14) — new terms (InOrgNodes,
/// InTerritories, SharedWith) are added as new sealed records when their fact provider
/// exists, never guessed at today.</summary>
public abstract record ScopeTerm
{
    public sealed record OwnedBy(PrincipalRef Principal) : ScopeTerm;
}
```

```csharp
// src/Contracts/AccessScope.cs
namespace Contracts;

/// <summary>The query-authorization contract's return shape. A domain adapter
/// translates this into a SQL filter — never fetches unauthorized rows and
/// post-filters (round 1 §10.2). An adapter that meets an unrecognized future
/// `ScopeTerm` subtype must treat it as `None` (fail-closed), not skip it.</summary>
public abstract record AccessScope
{
    public sealed record None : AccessScope;
    public sealed record All : AccessScope;
    public sealed record AnyOf(IReadOnlyList<ScopeTerm> Terms) : AccessScope;
}
```

```csharp
// src/Contracts/IAccessScopeResolver.cs
namespace Contracts;

/// <summary>Critical invariant (round 3 §5, must hold as a contract test — Task 9):
/// `Authorize(actor, action, row) == Allow` IFF `row` is included by
/// `ResolveAccessScope(actor, action, resourceType)`.</summary>
public interface IAccessScopeResolver
{
    Task<AccessScope> ResolveAsync(ActorContext actor, ActionKey action, string resourceType, CancellationToken cancellationToken = default);
}
```

- [x] **Step 7: `IActionCatalog`**

```csharp
// src/Contracts/IActionCatalog.cs
namespace Contracts;

/// <summary>Read-only check against the platform-owned action registry (round 3
/// ownership matrix: "Action Registry = Platform + owning business domain
/// vocabulary"). Access's PDP denies any unregistered or deprecated key
/// (gap-closure §3) — this is the runtime enforcement, there is no CI-time analyzer
/// in Phase 1.5.</summary>
public interface IActionCatalog
{
    Task<bool> IsActiveAsync(ActionKey action, CancellationToken cancellationToken = default);
}
```

- [x] **Step 8: Build and commit**

```bash
dotnet build src/Contracts/Contracts.csproj
```

Expected: builds with 0 warnings/errors.

```bash
git add src/Contracts/
git commit -m "feat(contracts): add authorization primitives (ActionKey, ActorContext, ResourceDescriptor, AuthorizationRequest/Decision, IAuthorizer, AccessScope, IAccessScopeResolver, IActionCatalog)"
```

---

## Task 2: Access domain model rebuild
→ Commit: `ac97550` (same commit as Task 1 — see status banner) plus a follow-up fix in `77a3ea4` (RolePermissionSet was missing `TenantId` entirely, found by the RLS migration failing against a real database)

**Files:**
- Delete: `src/Modules/Access/Domain/Authorization/Permission.cs`
- Delete: `src/Modules/Access/Domain/Authorization/RolePermission.cs`
- Modify: `src/Modules/Access/Domain/Authorization/Role.cs`
- Modify: `src/Modules/Access/Domain/Authorization/RoleAssignment.cs`
- Create: `src/Modules/Access/Domain/Authorization/ActionRegistryEntry.cs`
- Create: `src/Modules/Access/Domain/Authorization/PermissionSet.cs`
- Create: `src/Modules/Access/Domain/Authorization/PermissionSetItem.cs`
- Create: `src/Modules/Access/Domain/Authorization/RolePermissionSet.cs`
- Create: `src/Modules/Access/Domain/Authorization/PrincipalType.cs`
- Create: `src/Modules/Access/Domain/Authorization/TenantAccessState.cs`
- Test: `tests/Access.Tests/Domain/RoleTests.cs`
- Test: `tests/Access.Tests/Domain/PermissionSetTests.cs`
- Test: `tests/Access.Tests/Domain/RoleAssignmentTests.cs`
- Test: `tests/Access.Tests/Domain/TenantAccessStateTests.cs`

(Test project doesn't exist until Task 9 — write these files now, they compile once
Task 9's `.csproj` exists; don't run them until then, or move the four test files into
Task 9 if your workflow needs green tests every commit.)

- [x] **Step 1: `ActionRegistryEntry`** (replaces `Permission`)

```csharp
// src/Modules/Access/Domain/Authorization/ActionRegistryEntry.cs
namespace Access.Domain.Authorization;

/// <summary>Platform-owned projection of the code-manifest action vocabulary
/// (`Access.Application.AccessActionCatalog.All`, Task 5) — natural key, no surrogate
/// id (gap-closure §2). Never written by a tenant. A key that disappears from the
/// manifest is marked `IsDeprecated`, never deleted (docs/schema/identity-access-schema.md
/// successor; round 1 decision #5 "rename = deprecate + new key").</summary>
public sealed class ActionRegistryEntry
{
    public string ActionKey { get; private set; } = null!;
    public string OwnerModule { get; private set; } = null!;
    public string ResourceType { get; private set; } = null!;
    public string? RiskClass { get; private set; }
    public bool IsDeprecated { get; private set; }

    private ActionRegistryEntry() { }

    public static ActionRegistryEntry Create(string actionKey, string ownerModule, string resourceType, string? riskClass = null)
    {
        if (string.IsNullOrWhiteSpace(actionKey))
            throw new ArgumentException("Action key is required.", nameof(actionKey));
        if (string.IsNullOrWhiteSpace(ownerModule))
            throw new ArgumentException("Owner module is required.", nameof(ownerModule));
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("Resource type is required.", nameof(resourceType));

        return new ActionRegistryEntry
        {
            ActionKey = actionKey,
            OwnerModule = ownerModule,
            ResourceType = resourceType,
            RiskClass = riskClass
        };
    }

    public void Deprecate() => IsDeprecated = true;

    /// <summary>A previously-deprecated key reappearing in the manifest (e.g. a revert)
    /// is un-deprecated rather than requiring a brand-new key.</summary>
    public void Reactivate() => IsDeprecated = false;
}
```

- [x] **Step 2: `PermissionSetItem`**

```csharp
// src/Modules/Access/Domain/Authorization/PermissionSetItem.cs
using Contracts;

namespace Access.Domain.Authorization;

/// <summary>A single action grant inside a `PermissionSet`, optionally narrowed by a
/// relationship constraint. Phase 1.5's closed set is `null` (unrestricted within
/// scope) or `"owner"` (gap-closure §7) — a new relation value is a new CHECK
/// constraint added when its evaluator exists, not reserved today.</summary>
public sealed class PermissionSetItem
{
    public const string OwnerRelation = "owner";

    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }

    /// <summary>Real property, set by EF's relationship fixup when the item is added
    /// to `PermissionSet.Items` and `SaveChanges()` runs — not passed to `Create()`.
    /// Same pattern as `CRM.Domain.OpportunityLine.OpportunityId` (never a shadow
    /// property here: `AccessAuthorizer`/`AccessScopeResolver` query it directly).</summary>
    public long PermissionSetId { get; private set; }

    public string ActionKey { get; private set; } = null!;
    public string? Relation { get; private set; }

    private PermissionSetItem() { }

    internal static PermissionSetItem Create(TenantId tenantId, string actionKey, string? relation)
    {
        if (string.IsNullOrWhiteSpace(actionKey))
            throw new ArgumentException("Action key is required.", nameof(actionKey));
        if (relation is not null && relation != OwnerRelation)
            throw new ArgumentException($"Only '{OwnerRelation}' is a supported relation constraint in Phase 1.5.", nameof(relation));

        return new PermissionSetItem { TenantId = tenantId, ActionKey = actionKey, Relation = relation };
    }
}
```

- [x] **Step 3: `PermissionSet`**

```csharp
// src/Modules/Access/Domain/Authorization/PermissionSet.cs
using Contracts;

namespace Access.Domain.Authorization;

/// <summary>Reusable business-capability bundle — the first-class abstraction between
/// `Role` and `ActionKey` (round 1 decision #6). `Origin` records whether this row was
/// provisioned from a platform template or authored directly by the tenant
/// (round 4 Decision A) — it does not imply a reconciler exists; there is none in
/// Phase 1.5 (gap-closure §6).</summary>
public sealed class PermissionSet
{
    public const string OriginTenant = "tenant";
    public const string OriginSystemTemplate = "system_template";

    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Key { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Origin { get; private set; } = null!;

    private readonly List<PermissionSetItem> _items = [];
    public IReadOnlyCollection<PermissionSetItem> Items => _items;

    private PermissionSet() { }

    public static PermissionSet Create(TenantId tenantId, string key, string name, string origin = OriginTenant)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key is required.", nameof(key));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (origin is not (OriginTenant or OriginSystemTemplate))
            throw new ArgumentException("Origin must be 'tenant' or 'system_template'.", nameof(origin));

        return new PermissionSet { TenantId = tenantId, Key = key, Name = name, Origin = origin };
    }

    public PermissionSetItem Grant(string actionKey, string? relation = null)
    {
        if (_items.Any(i => i.ActionKey == actionKey))
            throw new InvalidOperationException($"'{actionKey}' is already granted by permission set '{Key}'.");

        var item = PermissionSetItem.Create(TenantId, actionKey, relation);
        _items.Add(item);
        return item;
    }
}
```

- [x] **Step 4: `RolePermissionSet`** (replaces `RolePermission`)

```csharp
// src/Modules/Access/Domain/Authorization/RolePermissionSet.cs
namespace Access.Domain.Authorization;

/// <summary>Pure join row, composite PK — same convention the old `RolePermission`
/// used, now joining `Role` to `PermissionSet` instead of directly to an action
/// (round 1 decision #6: Role -> PermissionSet -> ActionKey).</summary>
public sealed class RolePermissionSet
{
    public long RoleId { get; private set; }
    public long PermissionSetId { get; private set; }

    private RolePermissionSet() { }

    public static RolePermissionSet Create(long roleId, long permissionSetId) =>
        new() { RoleId = roleId, PermissionSetId = permissionSetId };
}
```

- [x] **Step 5: `Role` (revised)**

```csharp
// src/Modules/Access/Domain/Authorization/Role.cs
using Contracts;

namespace Access.Domain.Authorization;

/// <summary>A tenant-facing security persona (round 1 decision #7: Role is never
/// organization hierarchy). `TenantId` is NOT NULL — the old `tenant_id = null` "system
/// role" model is retired; a platform-template-provisioned row is still a real,
/// tenant-owned row with `Origin = OriginSystemTemplate` (round 4 Decision A,
/// gap-closure §6).</summary>
public sealed class Role
{
    public const string OriginTenant = "tenant";
    public const string OriginSystemTemplate = "system_template";

    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Key { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Origin { get; private set; } = null!;

    private Role() { }

    public static Role Create(TenantId tenantId, string key, string name, string origin = OriginTenant)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key is required.", nameof(key));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (origin is not (OriginTenant or OriginSystemTemplate))
            throw new ArgumentException("Origin must be 'tenant' or 'system_template'.", nameof(origin));

        return new Role { TenantId = tenantId, Key = key, Name = name, Origin = origin };
    }
}
```

- [x] **Step 6: `PrincipalType`**

```csharp
// src/Modules/Access/Domain/Authorization/PrincipalType.cs
namespace Access.Domain.Authorization;

/// <summary>Access-internal closed set (round 1 decision: "principal_type + principal_id
/// freeze, 1.5'te yalnız user"; gap-closure §8 — deliberately NOT a Contracts type,
/// since the cross-module identity key stays `PrincipalRef`). Adding `Service`/`Agent`/
/// `Group` later is a new enum member + CHECK constraint value, not a redesign.</summary>
public enum PrincipalType
{
    User
}
```

- [x] **Step 7: `RoleAssignment` (revised — scope columns removed)**

```csharp
// src/Modules/Access/Domain/Authorization/RoleAssignment.cs
using Contracts;

namespace Access.Domain.Authorization;

/// <summary>Grants, separate from membership. Every assignment is tenant-wide in
/// Phase 1.5 — `ScopeType`/`ScopeId` are removed, not reserved-and-denied
/// (gap-closure §1): Organization's fact provider doesn't exist, and a perpetually
/// deny-only enum value is dead weight. Org-node scope is added additively in the
/// migration that ships alongside a real Organization module. `Network` (cross-tenant)
/// is gone unconditionally (round 3 §6) — cross-tenant access is never modeled as an
/// ordinary assignment scope.</summary>
public sealed class RoleAssignment : IHasRowVersion
{
    public const string SourceManual = "manual";
    public const string SourceBootstrap = "bootstrap";

    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public PrincipalType PrincipalType { get; private set; }
    public long AccountId { get; private set; }
    public long RoleId { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }
    public string Source { get; private set; } = null!;
    public long GrantedByAccountId { get; private set; }
    public string? Reason { get; private set; }
    public long RowVersion { get; private set; } = 1;

    private RoleAssignment() { }

    public static RoleAssignment Grant(
        TenantId tenantId,
        long accountId,
        long roleId,
        long grantedByAccountId,
        string source,
        string? reason = null,
        DateTimeOffset? validFrom = null)
    {
        if (source is not (SourceManual or SourceBootstrap))
            throw new ArgumentException("Source must be 'manual' or 'bootstrap'.", nameof(source));

        return new RoleAssignment
        {
            TenantId = tenantId,
            PrincipalType = Authorization.PrincipalType.User,
            AccountId = accountId,
            RoleId = roleId,
            GrantedByAccountId = grantedByAccountId,
            Source = source,
            Reason = reason,
            ValidFrom = validFrom ?? DateTimeOffset.UtcNow
        };
    }

    public void Revoke(DateTimeOffset? at = null)
    {
        if (ValidTo is not null)
            throw new InvalidOperationException("Assignment is already revoked.");

        var revokedAt = at ?? DateTimeOffset.UtcNow;
        if (revokedAt < ValidFrom)
            throw new ArgumentOutOfRangeException(nameof(at), "Cannot revoke before the assignment's valid_from.");

        ValidTo = revokedAt;
    }

    public bool IsActiveAt(DateTimeOffset at) => ValidFrom <= at && (ValidTo is null || at < ValidTo);

    void IHasRowVersion.IncrementRowVersion() => RowVersion++;
}
```

- [x] **Step 8: `TenantAccessState`**

```csharp
// src/Modules/Access/Domain/Authorization/TenantAccessState.cs
using Contracts;

namespace Access.Domain.Authorization;

/// <summary>The one canonical `TenantAccessRevision` counter (round 3 freeze #17) —
/// increments whenever effective authorization configuration changes (Role,
/// PermissionSet, PermissionSetItem, RoleAssignment). Never conflated with
/// `IHasRowVersion.RowVersion`, which is per-row optimistic concurrency.</summary>
public sealed class TenantAccessState
{
    public TenantId TenantId { get; private set; }
    public long Revision { get; private set; }
    public long RowVersion { get; private set; } = 1;

    private TenantAccessState() { }

    public static TenantAccessState Initialize(TenantId tenantId) =>
        new() { TenantId = tenantId, Revision = 0 };

    public void BumpRevision() => Revision++;
}
```

- [x] **Step 9: Domain tests**

```csharp
// tests/Access.Tests/Domain/RoleTests.cs
using Access.Domain.Authorization;
using Contracts;

namespace Access.Tests.Domain;

public sealed class RoleTests
{
    [Fact]
    public void Create_rejects_invalid_origin()
    {
        Assert.Throws<ArgumentException>(() => Role.Create(new TenantId(1), "sales_rep", "Sales Representative", "bogus"));
    }

    [Fact]
    public void Create_sets_tenant_id_not_null()
    {
        var role = Role.Create(new TenantId(1), "sales_rep", "Sales Representative");
        Assert.Equal(new TenantId(1), role.TenantId);
    }
}
```

```csharp
// tests/Access.Tests/Domain/PermissionSetTests.cs
using Access.Domain.Authorization;
using Contracts;

namespace Access.Tests.Domain;

public sealed class PermissionSetTests
{
    [Fact]
    public void Grant_rejects_duplicate_action_key()
    {
        var set = PermissionSet.Create(new TenantId(1), "opportunity_owner", "Opportunity Owner");
        set.Grant("crm.opportunity.read", PermissionSetItem.OwnerRelation);

        Assert.Throws<InvalidOperationException>(() => set.Grant("crm.opportunity.read"));
    }

    [Fact]
    public void Grant_rejects_unsupported_relation()
    {
        var set = PermissionSet.Create(new TenantId(1), "opportunity_owner", "Opportunity Owner");
        Assert.Throws<ArgumentException>(() => set.Grant("crm.opportunity.read", "team_member"));
    }
}
```

```csharp
// tests/Access.Tests/Domain/RoleAssignmentTests.cs
using Access.Domain.Authorization;
using Contracts;

namespace Access.Tests.Domain;

public sealed class RoleAssignmentTests
{
    [Fact]
    public void Revoke_twice_throws()
    {
        var assignment = RoleAssignment.Grant(new TenantId(1), accountId: 1, roleId: 1, grantedByAccountId: 2, RoleAssignment.SourceManual);
        assignment.Revoke();

        Assert.Throws<InvalidOperationException>(() => assignment.Revoke());
    }

    [Fact]
    public void IsActiveAt_false_after_revocation()
    {
        var validFrom = DateTimeOffset.UtcNow.AddDays(-1);
        var assignment = RoleAssignment.Grant(new TenantId(1), 1, 1, 2, RoleAssignment.SourceManual, validFrom: validFrom);
        assignment.Revoke(validFrom.AddHours(1));

        Assert.True(assignment.IsActiveAt(validFrom.AddMinutes(30)));
        Assert.False(assignment.IsActiveAt(validFrom.AddHours(2)));
    }
}
```

```csharp
// tests/Access.Tests/Domain/TenantAccessStateTests.cs
using Access.Domain.Authorization;
using Contracts;

namespace Access.Tests.Domain;

public sealed class TenantAccessStateTests
{
    [Fact]
    public void BumpRevision_increments_monotonically()
    {
        var state = TenantAccessState.Initialize(new TenantId(1));
        state.BumpRevision();
        state.BumpRevision();

        Assert.Equal(2, state.Revision);
    }
}
```

- [x] **Step 10: Commit** (test project not wired to solution yet — build the module only)

```bash
dotnet build src/Modules/Access/Access.csproj
```

```bash
git add src/Modules/Access/Domain/ tests/Access.Tests/Domain/
git commit -m "feat(access): rebuild domain model — ActionRegistryEntry, PermissionSet, RolePermissionSet, revised Role/RoleAssignment, TenantAccessState"
```

---

## Task 3: Access persistence configurations + schema migration
→ Commit: `ac97550` (same commit as Tasks 1–2) plus the `77a3ea4` follow-up fix (migration regenerated after the RolePermissionSet tenant_id fix)

**Files:**
- Delete: `src/Modules/Access/Persistence/Configurations/PermissionConfiguration.cs`
- Delete: `src/Modules/Access/Persistence/Configurations/RolePermissionConfiguration.cs`
- Modify: `src/Modules/Access/Persistence/Configurations/RoleConfiguration.cs`
- Modify: `src/Modules/Access/Persistence/Configurations/RoleAssignmentConfiguration.cs`
- Modify: `src/Modules/Access/Persistence/AccessDbContext.cs`
- Create: `src/Modules/Access/Persistence/Configurations/ActionRegistryEntryConfiguration.cs`
- Create: `src/Modules/Access/Persistence/Configurations/PermissionSetConfiguration.cs`
- Create: `src/Modules/Access/Persistence/Configurations/PermissionSetItemConfiguration.cs`
- Create: `src/Modules/Access/Persistence/Configurations/RolePermissionSetConfiguration.cs`
- Create: `src/Modules/Access/Persistence/Configurations/TenantAccessStateConfiguration.cs`
- Create (generated): `src/Modules/Access/Persistence/Migrations/<timestamp>_RebuildAccessAuthorizationModel.cs`

- [x] **Step 1: `ActionRegistryEntryConfiguration`**

```csharp
// src/Modules/Access/Persistence/Configurations/ActionRegistryEntryConfiguration.cs
using Access.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class ActionRegistryEntryConfiguration : IEntityTypeConfiguration<ActionRegistryEntry>
{
    public void Configure(EntityTypeBuilder<ActionRegistryEntry> builder)
    {
        builder.ToTable("actions", AccessDbContext.AccessSchema);

        // Natural key — no surrogate id (gap-closure §2).
        builder.HasKey(a => a.ActionKey);
        builder.Property(a => a.ActionKey).HasMaxLength(200);

        builder.Property(a => a.OwnerModule).IsRequired();
        builder.Property(a => a.ResourceType).IsRequired();
        builder.Property(a => a.IsDeprecated).HasDefaultValue(false);

        // Not tenant-scoped — platform-owned catalog, same treatment as the old
        // `permissions` table (no tenant_id, no RLS).
    }
}
```

- [x] **Step 2: `PermissionSetConfiguration` + `PermissionSetItemConfiguration`**

```csharp
// src/Modules/Access/Persistence/Configurations/PermissionSetConfiguration.cs
using Access.Domain.Authorization;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class PermissionSetConfiguration : IEntityTypeConfiguration<PermissionSet>
{
    public void Configure(EntityTypeBuilder<PermissionSet> builder)
    {
        builder.ToTable("permission_sets", AccessDbContext.AccessSchema);

        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(p => p.Key).IsRequired();
        builder.Property(p => p.Name).IsRequired();
        builder.Property(p => p.Origin).HasMaxLength(20).IsRequired();

        // Composite FK target for permission_set_items and role_permission_sets.
        builder.HasIndex(p => new { p.TenantId, p.Id }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.Key }).IsUnique();

        builder.HasMany(p => p.Items)
            .WithOne()
            .HasForeignKey(i => new { i.TenantId, i.PermissionSetId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_permission_sets_origin",
            "origin IN ('tenant','system_template')"));
    }
}
```

```csharp
// src/Modules/Access/Persistence/Configurations/PermissionSetItemConfiguration.cs
using Access.Domain.Authorization;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class PermissionSetItemConfiguration : IEntityTypeConfiguration<PermissionSetItem>
{
    public void Configure(EntityTypeBuilder<PermissionSetItem> builder)
    {
        builder.ToTable("permission_set_items", AccessDbContext.AccessSchema);

        builder.HasKey(i => i.Id);

        builder.Property(i => i.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(i => i.ActionKey).IsRequired();
        builder.Property(i => i.Relation).HasMaxLength(20);

        builder.HasOne<ActionRegistryEntry>()
            .WithMany()
            .HasForeignKey(i => i.ActionKey)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.TenantId, i.ActionKey });

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_permission_set_items_relation",
            "relation IS NULL OR relation = 'owner'"));
    }
}
```

- [x] **Step 3: `RolePermissionSetConfiguration`**

```csharp
// src/Modules/Access/Persistence/Configurations/RolePermissionSetConfiguration.cs
using Access.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class RolePermissionSetConfiguration : IEntityTypeConfiguration<RolePermissionSet>
{
    public void Configure(EntityTypeBuilder<RolePermissionSet> builder)
    {
        builder.ToTable("role_permission_sets", AccessDbContext.AccessSchema);

        builder.HasKey(rp => new { rp.RoleId, rp.PermissionSetId });

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<PermissionSet>()
            .WithMany()
            .HasForeignKey(rp => rp.PermissionSetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [x] **Step 4: `RoleConfiguration` (revised)**

```csharp
// src/Modules/Access/Persistence/Configurations/RoleConfiguration.cs
using Access.Domain.Authorization;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles", AccessDbContext.AccessSchema);

        builder.HasKey(r => r.Id);

        // NOT NULL now — the tenant_id=null "system role" model is retired
        // (round 4 Decision A, gap-closure §6).
        builder.Property(r => r.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(r => r.Key).IsRequired();
        builder.Property(r => r.Name).IsRequired();
        builder.Property(r => r.Origin).HasMaxLength(20).IsRequired();

        builder.HasIndex(r => new { r.TenantId, r.Id }).IsUnique();
        builder.HasIndex(r => new { r.TenantId, r.Key }).IsUnique();
        builder.HasIndex(r => new { r.TenantId, r.Name }).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_roles_origin",
            "origin IN ('tenant','system_template')"));
    }
}
```

- [x] **Step 5: `RoleAssignmentConfiguration` (revised)**

```csharp
// src/Modules/Access/Persistence/Configurations/RoleAssignmentConfiguration.cs
using Access.Domain.Authorization;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        builder.ToTable("role_assignments", AccessDbContext.AccessSchema);

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(r => r.PrincipalType)
            .HasConversion(p => p.ToString().ToLowerInvariant(), v => Enum.Parse<PrincipalType>(v, ignoreCase: true))
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(r => r.Source).HasMaxLength(16).IsRequired();
        builder.Property(r => r.Reason).HasMaxLength(500);
        builder.Property(r => r.RowVersion).IsConcurrencyToken().IsRequired();

        // account_id/granted_by_account_id: no FK across the access/identity schema
        // boundary — same "reference by value" convention as before (identity-access-schema.md).
        builder.HasIndex(r => new { r.TenantId, r.AccountId });

        // Composite tenant-safe FK now (AGENTS.md binding-core #1) — was a bare FK before.
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(r => new { r.TenantId, r.RoleId })
            .HasPrincipalKey(role => new { role.TenantId, role.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_role_assignments_principal_type",
            "principal_type = 'user'"));
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_role_assignments_source",
            "source IN ('manual','bootstrap')"));
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_role_assignments_valid_range",
            "valid_to IS NULL OR valid_to > valid_from"));
    }
}
```

- [x] **Step 6: `TenantAccessStateConfiguration`**

```csharp
// src/Modules/Access/Persistence/Configurations/TenantAccessStateConfiguration.cs
using Access.Domain.Authorization;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class TenantAccessStateConfiguration : IEntityTypeConfiguration<TenantAccessState>
{
    public void Configure(EntityTypeBuilder<TenantAccessState> builder)
    {
        builder.ToTable("tenant_access_state", AccessDbContext.AccessSchema);

        // tenant_id is the natural PK — no surrogate id, same precedent as
        // idempotency_records' composite natural key (CRM Revision 3).
        builder.Property(t => t.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();
        builder.HasKey(t => t.TenantId);

        builder.Property(t => t.Revision).HasDefaultValue(0L);
        builder.Property(t => t.RowVersion).IsConcurrencyToken().IsRequired();
    }
}
```

- [x] **Step 7: `AccessDbContext` — add the new `DbSet`s**

```csharp
// src/Modules/Access/Persistence/AccessDbContext.cs
// Replace the existing DbSet block with:
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<ActionRegistryEntry> Actions => Set<ActionRegistryEntry>();
    public DbSet<PermissionSet> PermissionSets => Set<PermissionSet>();
    public DbSet<PermissionSetItem> PermissionSetItems => Set<PermissionSetItem>();
    public DbSet<RolePermissionSet> RolePermissionSets => Set<RolePermissionSet>();
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();
    public DbSet<TenantAccessState> TenantAccessStates => Set<TenantAccessState>();
```

(Remove the old `Permissions`/`RolePermissions` DbSets — their types no longer exist.)

- [x] **Step 8: Generate the migration**

```bash
dotnet tool restore
dotnet ef migrations add RebuildAccessAuthorizationModel \
  --project src/Modules/Access/Access.csproj \
  --startup-project src/Modules/Access/Access.csproj \
  --output-dir Persistence/Migrations
```

Expected diff (verify against `docs/schema/identity-access-schema.md` after Task 10
rewrites it): drops `permissions`, `role_permissions`; alters `roles` (`tenant_id`
NOT NULL, drops `is_system`, adds `key`/`origin` + two unique indexes); alters
`role_assignments` (drops `scope_type`/`scope_id`, adds `principal_type`/`source`/
`granted_by_account_id`/`reason`, changes the `role_id` FK to the composite
`(tenant_id, role_id)` form, adds the `valid_range` CHECK); creates `actions`,
`permission_sets`, `permission_set_items`, `role_permission_sets`,
`tenant_access_state`.

**No data migration needed** — `AccessDbContext` isn't registered in `Host` yet
(Task 8), so no environment has ever written a row through it. Confirm this before
running the migration:

```bash
dotnet ef migrations has-pending-model-changes \
  --project src/Modules/Access/Access.csproj \
  --startup-project src/Modules/Access/Access.csproj
```

Expected (after generating): "No changes."

- [x] **Step 9: `dotnet format` + commit**

```bash
dotnet format
git add src/Modules/Access/Persistence/
git commit -m "feat(access): persistence configs + RebuildAccessAuthorizationModel migration"
```

---

## Task 4: Access RLS + runtime role grants
→ Commit: `010b807` "feat(access): RLS on all tenant-scoped Access/Identity tables + runtime role grants". Covers all 10 tenant-scoped tables in one migration, not just Task 3's 7 — see the deviation note in the status banner (Task 5's planned second RLS migration was unnecessary since table creation wasn't split across two migrations during execution).

**Files:**
- Create (generated shell, hand-written body — the one permitted exception):
  `src/Modules/Access/Persistence/Migrations/<timestamp>_EnableAccessRowLevelSecurity.cs`
- Modify: `scripts/create-runtime-role.sql`

- [x] **Step 1: Generate the empty migration shell**

```bash
dotnet ef migrations add EnableAccessRowLevelSecurity \
  --project src/Modules/Access/Access.csproj \
  --startup-project src/Modules/Access/Access.csproj \
  --output-dir Persistence/Migrations
```

Expected: an empty `Up`/`Down` pair (no model changes — RLS has no EF representation).

- [x] **Step 2: Hand-write the RLS body** (mirrors
  `src/Modules/CRM/Persistence/Migrations/20260916081636_EnableRowLevelSecurity.cs`
  exactly — same `NULLIF(current_setting(...), '')::bigint` pattern, same
  `FORCE ROW LEVEL SECURITY`)

```csharp
// src/Modules/Access/Persistence/Migrations/<timestamp>_EnableAccessRowLevelSecurity.cs
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Access.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableAccessRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RLS has no EF Core model representation — the one hand-written migration
            // body AGENTS.md permits ("Enforcement Scope").
            foreach (var (schema, table) in TenantScopedTables)
            {
                migrationBuilder.Sql($"ALTER TABLE {schema}.{table} ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE {schema}.{table} FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"""
                    CREATE POLICY tenant_isolation ON {schema}.{table}
                        USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
                        WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (schema, table) in TenantScopedTables)
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON {schema}.{table};");
                migrationBuilder.Sql($"ALTER TABLE {schema}.{table} NO FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE {schema}.{table} DISABLE ROW LEVEL SECURITY;");
            }
        }

        // "identity.accounts" and "identity.external_identities" are deliberately
        // excluded — same exemption as before (accounts is platform-global;
        // external_identities is account-scoped, not tenant-scoped). "access.actions"
        // is excluded — platform-owned catalog, no tenant_id column.
        // "access.outbox_messages"/"idempotency_records"/"evidence_records" are NOT
        // here — those tables don't exist until Task 5; they get RLS from Task 5's
        // own migration (EnableAccessEvidenceOutboxIdempotencyRls), applied after the
        // table-creation migration in timestamp order.
        private static readonly (string Schema, string Table)[] TenantScopedTables =
        [
            ("identity", "tenant_memberships"),
            ("access", "roles"),
            ("access", "permission_sets"),
            ("access", "permission_set_items"),
            ("access", "role_permission_sets"),
            ("access", "role_assignments"),
            ("access", "tenant_access_state")
        ];
    }
}
```

- [x] **Step 3: Update `scripts/create-runtime-role.sql`** — append after the existing
  MasterData block:

```sql
-- Identity + Access modules (share one assembly/DbContext, two schemas).
GRANT USAGE ON SCHEMA identity TO fynovio_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA identity TO fynovio_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA identity TO fynovio_app;

ALTER DEFAULT PRIVILEGES IN SCHEMA identity
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO fynovio_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA identity
    GRANT USAGE, SELECT ON SEQUENCES TO fynovio_app;

GRANT USAGE ON SCHEMA access TO fynovio_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA access TO fynovio_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA access TO fynovio_app;

ALTER DEFAULT PRIVILEGES IN SCHEMA access
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO fynovio_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA access
    GRANT USAGE, SELECT ON SEQUENCES TO fynovio_app;

-- access.evidence_records is append-only, same convention as crm/masterdata.
REVOKE UPDATE, DELETE ON access.evidence_records FROM fynovio_app;
```

- [x] **Step 4: `dotnet format` + commit**

```bash
dotnet format
git add src/Modules/Access/Persistence/Migrations/ scripts/create-runtime-role.sql
git commit -m "feat(access): RLS on all tenant-scoped Access/Identity tables + runtime role grants"
```

---

## Task 5: Action Registry — manifest + idempotent seeder
→ Commit: `a733b66` "feat(access): action registry manifest + idempotent seeder + IActionCatalog service". The Outbox/Evidence/Idempotency entities + configs this task also called for were created earlier, in `ac97550`, alongside the domain rebuild (they're referenced by `AccessDbContext`'s DbSets from the start). The step's own second RLS migration was folded into Task 4's single migration (see above) — deviated deliberately, not an omission.

**Files:**
- Create: `src/Modules/Access/Application/AccessActionCatalog.cs`
- Create: `src/Modules/Access/Application/AccessActionCatalogSeeder.cs`
- Create: `src/Modules/Access/Application/AccessActionCatalogService.cs` (implements `IActionCatalog`)
- Create: `src/Modules/Access/Outbox/OutboxMessage.cs`
- Create: `src/Modules/Access/Evidence/EvidenceRecord.cs`
- Create: `src/Modules/Access/Idempotency/IdempotencyRecord.cs`
- Modify: `src/Modules/Access/Persistence/AccessDbContext.cs` (add three new `DbSet`s)
- Create: `src/Modules/Access/Persistence/Configurations/OutboxMessageConfiguration.cs`
- Create: `src/Modules/Access/Persistence/Configurations/EvidenceRecordConfiguration.cs`
- Create: `src/Modules/Access/Persistence/Configurations/IdempotencyRecordConfiguration.cs`
- Create (generated): `<timestamp>_AddAccessOutboxIdempotencyEvidence.cs`
- Create (generated shell, hand-written body): `<timestamp>_EnableAccessEvidenceOutboxIdempotencyRls.cs`
- Test: `tests/Access.Tests/Application/AccessActionCatalogSeederTests.cs`

Access needs its **own** copies of `OutboxMessage`/`EvidenceRecord`/`IdempotencyRecord`
— a module may reference `Contracts` only, never another module's types
(`AGENTS.md`), same deliberate duplication already documented for
`RowVersionInterceptor` (`docs/schema/identity-access-schema.md` Revision 3 note).

- [x] **Step 1: Copy the three CRM shapes into Access**, changing only the namespace.

```csharp
// src/Modules/Access/Outbox/OutboxMessage.cs
using Contracts;

namespace Access.Outbox;

/// <summary>Written in the same SaveChanges() transaction as the domain state it
/// describes (AGENTS.md binding-core #3). Deliberately duplicated from
/// `CRM.Outbox.OutboxMessage` — modules may reference Contracts only.</summary>
public sealed class OutboxMessage
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string AggregateType { get; private set; } = null!;
    public long AggregateId { get; private set; }
    public long AggregateVersion { get; private set; }
    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = null!;
    public string Source { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public Guid CorrelationId { get; private set; }
    public Guid? CausationId { get; private set; }
    public string Payload { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(
        TenantId tenantId, string aggregateType, long aggregateId, long aggregateVersion,
        string eventType, string source, string subject, Guid correlationId, Guid? causationId, string payload)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type is required.", nameof(eventType));
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload is required.", nameof(payload));

        return new OutboxMessage
        {
            TenantId = tenantId, AggregateType = aggregateType, AggregateId = aggregateId,
            AggregateVersion = aggregateVersion, EventId = Guid.NewGuid(), EventType = eventType,
            Source = source, Subject = subject, CorrelationId = correlationId, CausationId = causationId,
            Payload = payload, OccurredAt = DateTimeOffset.UtcNow
        };
    }

    public void MarkProcessed()
    {
        if (ProcessedAt is not null)
            throw new InvalidOperationException("Outbox message already processed.");
        ProcessedAt = DateTimeOffset.UtcNow;
    }
}
```

```csharp
// src/Modules/Access/Evidence/EvidenceRecord.cs
using Contracts;

namespace Access.Evidence;

/// <summary>Append-only. `Detail` carries the JSON of what changed — Access config
/// mutations count as risk-catalogued per AGENTS.md, so every Grant/Revoke writes one.
/// Deliberately duplicated from `CRM.Evidence.EvidenceRecord`.</summary>
public sealed class EvidenceRecord
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string AggregateType { get; private set; } = null!;
    public long AggregateId { get; private set; }
    public long AggregateVersion { get; private set; }
    public string PrincipalIssuer { get; private set; } = null!;
    public string PrincipalSubject { get; private set; } = null!;
    public string Action { get; private set; } = null!;
    public string Detail { get; private set; } = null!;
    public Guid CorrelationId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private EvidenceRecord() { }

    public static EvidenceRecord Create(
        TenantId tenantId, string aggregateType, long aggregateId, long aggregateVersion,
        PrincipalRef principal, string action, string detail, Guid correlationId)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action is required.", nameof(action));
        if (string.IsNullOrWhiteSpace(detail))
            throw new ArgumentException("Detail is required.", nameof(detail));

        return new EvidenceRecord
        {
            TenantId = tenantId, AggregateType = aggregateType, AggregateId = aggregateId,
            AggregateVersion = aggregateVersion, PrincipalIssuer = principal.Issuer,
            PrincipalSubject = principal.Subject, Action = action, Detail = detail,
            CorrelationId = correlationId, OccurredAt = DateTimeOffset.UtcNow
        };
    }
}
```

```csharp
// src/Modules/Access/Idempotency/IdempotencyRecord.cs
using Contracts;

namespace Access.Idempotency;

/// <summary>Deliberately duplicated from `CRM.Idempotency.IdempotencyRecord`. Grant/
/// Revoke lookups happen AFTER authorization (round 3 §9/§13 final invariant) — see
/// Task 7's handlers.</summary>
public sealed class IdempotencyRecord
{
    public TenantId TenantId { get; private set; }
    public string PrincipalIssuer { get; private set; } = null!;
    public string PrincipalSubject { get; private set; } = null!;
    public string Operation { get; private set; } = null!;
    public string IdempotencyKey { get; private set; } = null!;
    public string RequestHash { get; private set; } = null!;
    public int ResponseStatus { get; private set; }
    public string ResponsePayload { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    private IdempotencyRecord() { }

    public static IdempotencyRecord Create(
        TenantId tenantId, PrincipalRef principal, string operation, string idempotencyKey,
        string requestHash, int responseStatus, string responsePayload, TimeSpan retention)
    {
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("Operation is required.", nameof(operation));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        if (string.IsNullOrWhiteSpace(requestHash))
            throw new ArgumentException("Request hash is required.", nameof(requestHash));

        var now = DateTimeOffset.UtcNow;
        return new IdempotencyRecord
        {
            TenantId = tenantId, PrincipalIssuer = principal.Issuer, PrincipalSubject = principal.Subject,
            Operation = operation, IdempotencyKey = idempotencyKey, RequestHash = requestHash,
            ResponseStatus = responseStatus, ResponsePayload = responsePayload,
            CreatedAt = now, ExpiresAt = now.Add(retention)
        };
    }
}
```

- [x] **Step 2: EF configurations** (mirror the CRM configurations' shape — composite
  PK for `IdempotencyRecord`, `evidence_records` append-only at the DB-grant level
  from Task 4's script, standard columns otherwise)

```csharp
// src/Modules/Access/Persistence/Configurations/OutboxMessageConfiguration.cs
using Access.Outbox;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", AccessDbContext.AccessSchema);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.TenantId).HasConversion(id => id.Value, v => new TenantId(v)).IsRequired();
        builder.Property(m => m.EventType).IsRequired();
        builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(m => m.EventId).IsUnique();
        builder.HasIndex(m => new { m.TenantId, m.ProcessedAt });
    }
}
```

```csharp
// src/Modules/Access/Persistence/Configurations/EvidenceRecordConfiguration.cs
using Access.Evidence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class EvidenceRecordConfiguration : IEntityTypeConfiguration<EvidenceRecord>
{
    public void Configure(EntityTypeBuilder<EvidenceRecord> builder)
    {
        builder.ToTable("evidence_records", AccessDbContext.AccessSchema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TenantId).HasConversion(id => id.Value, v => new TenantId(v)).IsRequired();
        builder.Property(e => e.Action).IsRequired();
        builder.Property(e => e.Detail).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.AggregateType, e.AggregateId });
        builder.HasIndex(e => e.CorrelationId);
    }
}
```

```csharp
// src/Modules/Access/Persistence/Configurations/IdempotencyRecordConfiguration.cs
using Access.Idempotency;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records", AccessDbContext.AccessSchema);
        builder.Property(r => r.TenantId).HasConversion(id => id.Value, v => new TenantId(v)).IsRequired();
        builder.HasKey(r => new { r.TenantId, r.PrincipalIssuer, r.PrincipalSubject, r.Operation, r.IdempotencyKey });
        builder.Property(r => r.ResponsePayload).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(r => r.ExpiresAt);
    }
}
```

- [x] **Step 3: `AccessDbContext` — add the three `DbSet`s**

```csharp
// add alongside the existing DbSets:
    public DbSet<Access.Outbox.OutboxMessage> OutboxMessages => Set<Access.Outbox.OutboxMessage>();
    public DbSet<Access.Evidence.EvidenceRecord> EvidenceRecords => Set<Access.Evidence.EvidenceRecord>();
    public DbSet<Access.Idempotency.IdempotencyRecord> IdempotencyRecords => Set<Access.Idempotency.IdempotencyRecord>();
```

- [x] **Step 4: `AccessActionCatalog`** — the static manifest. Phase 1.5 registers
  **only Access's own vocabulary** — `crm.opportunity.*` is NOT seeded here
  (gap-closure §3; CRM isn't touched by this plan).

```csharp
// src/Modules/Access/Application/AccessActionCatalog.cs
namespace Access.Application;

public sealed record ActionRegistryDescriptor(string ActionKey, string OwnerModule, string ResourceType, string? RiskClass = null);

/// <summary>The code-manifest source of truth for the action registry (round 1
/// decision #5). `AccessActionCatalogSeeder` (Step 5) projects this into
/// `access.actions` idempotently at Host startup.</summary>
public static class AccessActionCatalog
{
    public static readonly IReadOnlyList<ActionRegistryDescriptor> All =
    [
        new("access.role_assignment.grant", "Access", "Access.RoleAssignment", RiskClass: "high"),
        new("access.role_assignment.revoke", "Access", "Access.RoleAssignment", RiskClass: "high"),
        new("access.role.manage", "Access", "Access.Role"),
        new("access.permission_set.manage", "Access", "Access.PermissionSet")
    ];
}
```

- [x] **Step 5: `AccessActionCatalogSeeder`** — idempotent upsert, deprecate-not-delete.

```csharp
// src/Modules/Access/Application/AccessActionCatalogSeeder.cs
using Access.Domain.Authorization;
using Access.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

public static class AccessActionCatalogSeeder
{
    public static async Task EnsureSeededAsync(AccessDbContext context, CancellationToken cancellationToken = default)
    {
        var existing = await context.Actions.ToDictionaryAsync(a => a.ActionKey, cancellationToken);
        var manifestKeys = AccessActionCatalog.All.Select(d => d.ActionKey).ToHashSet();

        foreach (var descriptor in AccessActionCatalog.All)
        {
            if (existing.TryGetValue(descriptor.ActionKey, out var entry))
            {
                if (entry.IsDeprecated)
                    entry.Reactivate();
                continue;
            }

            context.Actions.Add(ActionRegistryEntry.Create(
                descriptor.ActionKey, descriptor.OwnerModule, descriptor.ResourceType, descriptor.RiskClass));
        }

        foreach (var entry in existing.Values.Where(e => !manifestKeys.Contains(e.ActionKey) && !e.IsDeprecated))
            entry.Deprecate();

        await context.SaveChangesAsync(cancellationToken);
    }
}
```

- [x] **Step 6: `AccessActionCatalogService`** (implements `Contracts.IActionCatalog`)

```csharp
// src/Modules/Access/Application/AccessActionCatalogService.cs
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

public sealed class AccessActionCatalogService(AccessDbContext context) : IActionCatalog
{
    public async Task<bool> IsActiveAsync(ActionKey action, CancellationToken cancellationToken = default) =>
        await context.Actions.AnyAsync(a => a.ActionKey == action.Value && !a.IsDeprecated, cancellationToken);
}
```

- [x] **Step 7: Generate the table-creation migration**

```bash
dotnet ef migrations add AddAccessOutboxIdempotencyEvidence \
  --project src/Modules/Access/Access.csproj \
  --startup-project src/Modules/Access/Access.csproj \
  --output-dir Persistence/Migrations
```

- [x] **Step 8: RLS for these three tables** — a second hand-written RLS migration,
  generated *after* Step 7's so it applies later in timestamp order (Task 4's RLS
  migration already covers `tenant_memberships`/`roles`/`permission_sets`/
  `permission_set_items`/`role_permission_sets`/`role_assignments`/
  `tenant_access_state` — those tables exist by Task 3; these three don't exist until
  Step 7 above, so they need their own RLS migration, not a place in Task 4's).

```bash
dotnet ef migrations add EnableAccessEvidenceOutboxIdempotencyRls \
  --project src/Modules/Access/Access.csproj \
  --startup-project src/Modules/Access/Access.csproj \
  --output-dir Persistence/Migrations
```

```csharp
// src/Modules/Access/Persistence/Migrations/<timestamp>_EnableAccessEvidenceOutboxIdempotencyRls.cs
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Access.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableAccessEvidenceOutboxIdempotencyRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantScopedTables)
            {
                migrationBuilder.Sql($"ALTER TABLE access.{table} ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE access.{table} FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"""
                    CREATE POLICY tenant_isolation ON access.{table}
                        USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
                        WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantScopedTables)
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON access.{table};");
                migrationBuilder.Sql($"ALTER TABLE access.{table} NO FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE access.{table} DISABLE ROW LEVEL SECURITY;");
            }
        }

        private static readonly string[] TenantScopedTables =
        [
            "outbox_messages",
            "idempotency_records",
            "evidence_records"
        ];
    }
}
```

- [x] **Step 9: `dotnet format` + commit**

```bash
dotnet format
git add src/Modules/Access/Application/ src/Modules/Access/Outbox/ src/Modules/Access/Evidence/ \
        src/Modules/Access/Idempotency/ src/Modules/Access/Persistence/
git commit -m "feat(access): action registry manifest + idempotent seeder + outbox/evidence/idempotency + RLS"
```

---

## Task 6: PDP (`IAuthorizer`) and query scope resolver (`IAccessScopeResolver`)
→ Commit: `9f0bb74` "feat(access): default-deny PDP (AccessAuthorizer) and query scope resolver (AccessScopeResolver)". The equivalence contract test and additional authorizer unit tests (Steps 4–5) were written in Task 9's commit (`77a3ea4`) once `tests/Access.Tests` existed, per this task's own note.

**Files:**
- Create: `src/Modules/Access/Application/AccessAuthorizer.cs`
- Create: `src/Modules/Access/Application/AccessScopeResolver.cs`
- Create: `src/Modules/Access/Application/PrincipalResolver.cs`
- Test: `tests/Access.Tests/Application/AccessAuthorizerTests.cs`
- Test: `tests/Access.Tests/Application/AuthorizeResolveAccessScopeEquivalenceTests.cs`

- [x] **Step 1: `PrincipalResolver`** — shared `PrincipalRef → AccountId` lookup, used
  by both the PDP and the scope resolver, and by Task 7's handlers.

```csharp
// src/Modules/Access/Application/PrincipalResolver.cs
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

public sealed class PrincipalResolver(AccessDbContext context)
{
    /// <summary>Fail-closed: an unrecognized `PrincipalRef` resolves to `null`, never
    /// to an assumed identity (round 1 decision #2 default-deny).</summary>
    public async Task<long?> ResolveAccountIdAsync(PrincipalRef principal, CancellationToken cancellationToken = default) =>
        await context.ExternalIdentities
            .Where(e => e.Issuer == principal.Issuer && e.Subject == principal.Subject)
            .Select(e => (long?)e.AccountId)
            .SingleOrDefaultAsync(cancellationToken);
}
```

- [x] **Step 2: `AccessAuthorizer`**

```csharp
// src/Modules/Access/Application/AccessAuthorizer.cs
using Access.Domain.Authorization;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

/// <summary>Default-deny PDP. RBAC + tenant-wide scope + `owner` relation only —
/// Phase 1.5's frozen scope (round 3 §8 freeze #10). Fail-closed on every unresolved
/// input: unregistered action, unrecognized principal, no matching grant.</summary>
public sealed class AccessAuthorizer(
    AccessDbContext context,
    PrincipalResolver principalResolver,
    IActionCatalog actionCatalog) : IAuthorizer
{
    public async Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        var decisionId = Guid.NewGuid();
        var revision = await GetRevisionAsync(request.Actor.TenantId, cancellationToken);

        if (!await actionCatalog.IsActiveAsync(request.Action, cancellationToken))
            return Deny("action_not_registered", decisionId, revision);

        var accountId = await principalResolver.ResolveAccountIdAsync(request.Actor.Principal, cancellationToken);
        if (accountId is null)
            return Deny("principal_not_recognized", decisionId, revision);

        var now = DateTimeOffset.UtcNow;
        var items = await context.RoleAssignments
            .Where(a => a.TenantId == request.Actor.TenantId && a.AccountId == accountId
                && a.ValidFrom <= now && (a.ValidTo == null || now < a.ValidTo))
            .Join(context.RolePermissionSets, a => a.RoleId, rps => rps.RoleId, (a, rps) => rps.PermissionSetId)
            .Join(context.PermissionSetItems, psId => psId, i => i.PermissionSetId, (psId, i) => i)
            .Where(i => i.ActionKey == request.Action.Value)
            .ToListAsync(cancellationToken);

        if (items.Any(i => i.Relation is null))
            return Allow("tenant_scope_grant", decisionId, revision);

        if (items.Any(i => i.Relation == PermissionSetItem.OwnerRelation)
            && request.Resource.OwnerPrincipal is { } owner
            && owner == request.Actor.Principal)
            return Allow("owner_relation_grant", decisionId, revision);

        return Deny("no_matching_grant", decisionId, revision);
    }

    private async Task<long> GetRevisionAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        await context.TenantAccessStates
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.Revision)
            .SingleOrDefaultAsync(cancellationToken);

    private static AuthorizationDecision Allow(string reason, Guid decisionId, long revision) =>
        new(AuthorizationEffect.Allow, reason, decisionId, revision);

    private static AuthorizationDecision Deny(string reason, Guid decisionId, long revision) =>
        new(AuthorizationEffect.Deny, reason, decisionId, revision);
}
```

- [x] **Step 3: `AccessScopeResolver`**

```csharp
// src/Modules/Access/Application/AccessScopeResolver.cs
using Access.Domain.Authorization;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

/// <summary>Phase 1.5 implements exactly three outcomes: `None`, `All`, or
/// `AnyOf([OwnedBy])` (round 3 §8 freeze #10). The critical invariant
/// (round 3 §5) is verified by AuthorizeResolveAccessScopeEquivalenceTests.</summary>
public sealed class AccessScopeResolver(
    AccessDbContext context,
    PrincipalResolver principalResolver,
    IActionCatalog actionCatalog) : IAccessScopeResolver
{
    public async Task<AccessScope> ResolveAsync(ActorContext actor, ActionKey action, string resourceType, CancellationToken cancellationToken = default)
    {
        if (!await actionCatalog.IsActiveAsync(action, cancellationToken))
            return new AccessScope.None();

        var accountId = await principalResolver.ResolveAccountIdAsync(actor.Principal, cancellationToken);
        if (accountId is null)
            return new AccessScope.None();

        var now = DateTimeOffset.UtcNow;
        var items = await context.RoleAssignments
            .Where(a => a.TenantId == actor.TenantId && a.AccountId == accountId
                && a.ValidFrom <= now && (a.ValidTo == null || now < a.ValidTo))
            .Join(context.RolePermissionSets, a => a.RoleId, rps => rps.RoleId, (a, rps) => rps.PermissionSetId)
            .Join(context.PermissionSetItems, psId => psId, i => i.PermissionSetId, (psId, i) => i)
            .Where(i => i.ActionKey == action.Value)
            .ToListAsync(cancellationToken);

        if (items.Any(i => i.Relation is null))
            return new AccessScope.All();

        if (items.Any(i => i.Relation == PermissionSetItem.OwnerRelation))
            return new AccessScope.AnyOf([new ScopeTerm.OwnedBy(actor.Principal)]);

        return new AccessScope.None();
    }
}
```

- [x] **Step 4: The equivalence contract test** (round 3 §5 — the load-bearing test)

```csharp
// tests/Access.Tests/Application/AuthorizeResolveAccessScopeEquivalenceTests.cs
using Access.Application;
using Contracts;

namespace Access.Tests.Application;

/// <summary>Guards the round 3 §5 invariant: Authorize(actor, action, row) == Allow
/// IFF row ∈ ResolveAccessScope(actor, action, resourceType). Uses a synthetic
/// resource — no CRM/domain adapter exists yet (that's Phase 2); this proves the
/// Access-side contract is internally consistent before any consumer depends on it.</summary>
public sealed class AuthorizeResolveAccessScopeEquivalenceTests : IClassFixture<AccessTestFixture>
{
    private readonly AccessTestFixture _fixture;

    public AuthorizeResolveAccessScopeEquivalenceTests(AccessTestFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(true)]  // resource owned by the actor
    [InlineData(false)] // resource owned by someone else
    public async Task Owner_relation_grant_is_consistent_across_both_contracts(bool actorOwnsResource)
    {
        var (authorizer, scopeResolver, actor, action) = await _fixture.GivenOwnerRelationGrantAsync();
        var ownerPrincipal = actorOwnsResource ? actor.Principal : new PrincipalRef("test-idp", "someone-else");
        var resource = new ResourceDescriptor("Test.Resource", 1, ownerPrincipal);

        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(actor, action, resource));
        var scope = await scopeResolver.ResolveAsync(actor, action, resource.ResourceType);

        var includedByScope = scope switch
        {
            AccessScope.None => false,
            AccessScope.All => true,
            AccessScope.AnyOf anyOf => anyOf.Terms.OfType<ScopeTerm.OwnedBy>().Any(t => t.Principal == ownerPrincipal),
            _ => false
        };

        Assert.Equal(decision.IsAllowed, includedByScope);
    }
}
```

(`AccessTestFixture` — the shared test setup that seeds a tenant, an account, a
role/permission-set with an `owner`-relation grant, and returns wired
`AccessAuthorizer`/`AccessScopeResolver` instances against an in-memory or
Testcontainers-backed `AccessDbContext` — write it in Task 9 alongside
`PostgresFixture`, since both need the same container-backed context. If you reach
this step before Task 9, write a minimal version here now and consolidate in Task 9.)

- [x] **Step 5: Additional authorizer unit tests**

```csharp
// tests/Access.Tests/Application/AccessAuthorizerTests.cs — add cases for:
// - unregistered ActionKey -> Deny("action_not_registered")
// - unrecognized PrincipalRef -> Deny("principal_not_recognized")
// - no RoleAssignment at all -> Deny("no_matching_grant")
// - revoked RoleAssignment (ValidTo in the past) -> Deny("no_matching_grant")
// - unrestricted (relation=null) grant -> Allow("tenant_scope_grant") regardless of resource owner
// Each case follows the same Arrange/Act/Assert shape as Step 4's fixture.
```

- [x] **Step 6: Commit**

```bash
git add src/Modules/Access/Application/AccessAuthorizer.cs src/Modules/Access/Application/AccessScopeResolver.cs \
        src/Modules/Access/Application/PrincipalResolver.cs tests/Access.Tests/Application/
git commit -m "feat(access): default-deny PDP (AccessAuthorizer) and query scope resolver (AccessScopeResolver)"
```

---

## Task 7: Bootstrap + Grant/Revoke commands
→ Commit: `db6b192` "feat(access): bootstrap handler + Grant/Revoke RoleAssignment commands", with a follow-up fix in `77a3ea4` (`BootstrapTenantAccessHandler.RolePermissionSet.Create` call updated for the `TenantId` parameter added by the Task 2/3 fix). Integration tests (Step 5) written in `77a3ea4`.

**Files:**
- Create: `src/Modules/Access/Application/BootstrapTenantAccessCommand.cs`
- Create: `src/Modules/Access/Application/BootstrapTenantAccessHandler.cs`
- Create: `src/Modules/Access/Application/GrantRoleAssignmentCommand.cs`
- Create: `src/Modules/Access/Application/GrantRoleAssignmentHandler.cs`
- Create: `src/Modules/Access/Application/RevokeRoleAssignmentCommand.cs`
- Create: `src/Modules/Access/Application/RevokeRoleAssignmentHandler.cs`
- Create: `src/Modules/Access/Application/AuthorizationDeniedException.cs`
- Create: `src/Modules/Access/Application/IdempotencyKeyReusedException.cs`
- Test: `tests/Access.Tests/Integration/BootstrapTenantAccessHandlerTests.cs`
- Test: `tests/Access.Tests/Integration/GrantRevokeRoleAssignmentHandlerTests.cs`

Handler shape mirrors `CRM.Application.CompleteOpportunityHandler` exactly (one
transaction, `SetTenantContextAsync`, one `SaveChanges()`), with the ordering
correction from round 3 §9: **authorize before the idempotency lookup**, since Grant/
Revoke's own authorization gate now exists (unlike `CompleteOpportunityHandler`, which
had none to get the ordering wrong against).

- [x] **Step 1: `AccessDbContext` tenant-context extension** (mirror `CrmDbContextTenantExtensions`)

```csharp
// src/Modules/Access/Persistence/AccessDbContextTenantExtensions.cs
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Persistence;

public static class AccessDbContextTenantExtensions
{
    public static async Task SetTenantContextAsync(this AccessDbContext context, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Tenant context must be set inside an explicit transaction.");

        var value = tenantId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('app.tenant_id', {value}, true)", cancellationToken);
    }
}
```

- [x] **Step 2: `BootstrapTenantAccessHandler`** (gap-closure §6 — the one path with no
  `Authorize()` call, and it must stay that way)

```csharp
// src/Modules/Access/Application/BootstrapTenantAccessCommand.cs
using Contracts;

namespace Access.Application;

/// <summary>NOT exposed over HTTP, ever. Called only from tenant provisioning (once
/// TenantLifecycle's real ProvisionTenant exists) or directly from test fixtures in
/// Phase 1.5. This IS Decision A's "template -> tenant-local instance" mechanism —
/// today's manual invocation, not a reconciler (gap-closure §6).</summary>
public sealed record BootstrapTenantAccessCommand(
    TenantId TenantId,
    PrincipalRef TenantAdministrator,
    Guid CorrelationId);
```

```csharp
// src/Modules/Access/Application/BootstrapTenantAccessHandler.cs
using Access.Domain.Authorization;
using Access.Evidence;
using Access.Outbox;
using Access.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

public sealed class BootstrapTenantAccessHandler(AccessDbContext context)
{
    private const string TenantAdministratorRoleKey = "tenant_administrator";
    private const string TenantAdministratorPermissionSetKey = "tenant_administration";

    public async Task HandleAsync(BootstrapTenantAccessCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        if (await context.TenantAccessStates.AnyAsync(s => s.TenantId == command.TenantId, cancellationToken))
            throw new InvalidOperationException($"Tenant {command.TenantId} has already been bootstrapped.");

        var accountId = await context.ExternalIdentities
            .Where(e => e.Issuer == command.TenantAdministrator.Issuer && e.Subject == command.TenantAdministrator.Subject)
            .Select(e => (long?)e.AccountId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Tenant administrator principal must have a linked Account/ExternalIdentity before bootstrap.");

        var permissionSet = PermissionSet.Create(command.TenantId, TenantAdministratorPermissionSetKey, "Tenant Administration", PermissionSet.OriginSystemTemplate);
        foreach (var descriptor in AccessActionCatalog.All)
            permissionSet.Grant(descriptor.ActionKey);
        context.PermissionSets.Add(permissionSet);

        var role = Role.Create(command.TenantId, TenantAdministratorRoleKey, "Tenant Administrator", Role.OriginSystemTemplate);
        context.Roles.Add(role);
        await context.SaveChangesAsync(cancellationToken); // assigns Id to permissionSet/role before the join row

        context.RolePermissionSets.Add(RolePermissionSet.Create(role.Id, permissionSet.Id));

        var assignment = RoleAssignment.Grant(command.TenantId, accountId, role.Id, accountId, RoleAssignment.SourceBootstrap, reason: "Tenant bootstrap");
        context.RoleAssignments.Add(assignment);

        context.TenantAccessStates.Add(TenantAccessState.Initialize(command.TenantId));

        var detail = $$"""{"role":"{{TenantAdministratorRoleKey}}","accountId":{{accountId}}}""";
        context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId, nameof(RoleAssignment), assignment.Id, assignment.RowVersion,
            command.TenantAdministrator, "AccessBootstrapped", detail, command.CorrelationId));
        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(RoleAssignment), assignment.Id, assignment.RowVersion,
            "enterprise.access.tenant.bootstrapped.v1", "/enterprise/access", $"role-assignments/{assignment.Id}",
            command.CorrelationId, null, detail));

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
```

- [x] **Step 3: `GrantRoleAssignmentCommand`/`Handler`**

```csharp
// src/Modules/Access/Application/GrantRoleAssignmentCommand.cs
using Contracts;

namespace Access.Application;

public sealed record GrantRoleAssignmentCommand(
    TenantId TenantId,
    PrincipalRef GrantedBy,
    PrincipalRef Grantee,
    string RoleKey,
    string? Reason,
    Guid CorrelationId,
    string IdempotencyKey);

public sealed record GrantRoleAssignmentResult(long RoleAssignmentId, bool Replayed);
```

```csharp
// src/Modules/Access/Application/AuthorizationDeniedException.cs
namespace Access.Application;

public sealed class AuthorizationDeniedException(string action, string reasonCode)
    : Exception($"Action '{action}' was denied: {reasonCode}");
```

```csharp
// src/Modules/Access/Application/IdempotencyKeyReusedException.cs
namespace Access.Application;

public sealed class IdempotencyKeyReusedException(string operation, string idempotencyKey)
    : Exception($"Idempotency key '{idempotencyKey}' for operation '{operation}' was reused with a different request.");
```

```csharp
// src/Modules/Access/Application/GrantRoleAssignmentHandler.cs
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Access.Domain.Authorization;
using Access.Domain.Identity;
using Access.Evidence;
using Access.Outbox;
using Access.Idempotency;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

/// <summary>Order (round 3 §9 final invariant): tenant context -> authorize ->
/// idempotency lookup -> mutation -> evidence/outbox/idempotency-write -> commit.
/// Authorizing before the idempotency lookup means a principal whose grant authority
/// was revoked between two identical requests gets denied on the retry, never a
/// stale cached "success" (the bug this plan's gap-closure §9/round 3 flagged in
/// CompleteOpportunityHandler's current ordering).</summary>
public sealed class GrantRoleAssignmentHandler(AccessDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "GrantRoleAssignment";
    private const string EventType = "enterprise.access.role_assignment.granted.v1";
    private const string EventSource = "/enterprise/access";
    private const int SucceededStatus = 200;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    public async Task<GrantRoleAssignmentResult> HandleAsync(GrantRoleAssignmentCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        var actor = new ActorContext(command.TenantId, command.GrantedBy, command.CorrelationId);
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey("access.role_assignment.grant"), new ResourceDescriptor("Access.RoleAssignment", null, null)),
            cancellationToken);
        if (!decision.IsAllowed)
            throw new AuthorizationDeniedException("access.role_assignment.grant", decision.ReasonCode);

        var requestHash = HashRequest(command);
        var existing = await context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
            r => r.TenantId == command.TenantId && r.PrincipalIssuer == command.GrantedBy.Issuer
                && r.PrincipalSubject == command.GrantedBy.Subject && r.Operation == Operation
                && r.IdempotencyKey == command.IdempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);

            await transaction.CommitAsync(cancellationToken);
            var stored = JsonSerializer.Deserialize<GrantedPayload>(existing.ResponsePayload)
                ?? throw new InvalidOperationException("Stored idempotency response is empty.");
            return new GrantRoleAssignmentResult(stored.RoleAssignmentId, Replayed: true);
        }

        var granteeAccountId = await context.TenantMemberships
            .Join(context.ExternalIdentities, m => m.AccountId, e => e.AccountId, (m, e) => new { m, e })
            .Where(x => x.m.TenantId == command.TenantId && x.e.Issuer == command.Grantee.Issuer && x.e.Subject == command.Grantee.Subject
                && x.m.Status == MembershipStatus.Active)
            .Select(x => (long?)x.m.AccountId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Grantee must be an active member of this tenant.");

        var grantedByAccountId = await context.ExternalIdentities
            .Where(e => e.Issuer == command.GrantedBy.Issuer && e.Subject == command.GrantedBy.Subject)
            .Select(e => (long?)e.AccountId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Granting principal must have a linked Account/ExternalIdentity.");

        var role = await context.Roles.SingleOrDefaultAsync(r => r.TenantId == command.TenantId && r.Key == command.RoleKey, cancellationToken)
            ?? throw new InvalidOperationException($"Role '{command.RoleKey}' does not exist for tenant {command.TenantId}.");

        var assignment = RoleAssignment.Grant(command.TenantId, granteeAccountId, role.Id, grantedByAccountId, RoleAssignment.SourceManual, command.Reason);
        context.RoleAssignments.Add(assignment);

        var state = await context.TenantAccessStates.SingleAsync(s => s.TenantId == command.TenantId, cancellationToken);
        state.BumpRevision();

        var payload = new GrantedPayload(0, command.RoleKey); // RoleAssignmentId filled after SaveChanges below
        await context.SaveChangesAsync(cancellationToken); // assigns assignment.Id

        payload = payload with { RoleAssignmentId = assignment.Id };
        var payloadJson = JsonSerializer.Serialize(payload);

        context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId, nameof(RoleAssignment), assignment.Id, assignment.RowVersion,
            command.GrantedBy, "RoleAssignment.Grant", payloadJson, command.CorrelationId));
        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(RoleAssignment), assignment.Id, assignment.RowVersion,
            EventType, EventSource, $"role-assignments/{assignment.Id}", command.CorrelationId, null, payloadJson));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId, command.GrantedBy, Operation, command.IdempotencyKey, requestHash, SucceededStatus, payloadJson, IdempotencyRetention));

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new GrantRoleAssignmentResult(assignment.Id, Replayed: false);
    }

    private static string HashRequest(GrantRoleAssignmentCommand command)
    {
        var canonical = string.Create(CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.GrantedBy}|{command.Grantee}|{command.RoleKey}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed record GrantedPayload(long RoleAssignmentId, string RoleKey);
}
```

- [x] **Step 4: `RevokeRoleAssignmentCommand`/`Handler`** — same shape as Step 3,
  using `access.role_assignment.revoke`, calling `assignment.Revoke()`, event type
  `enterprise.access.role_assignment.revoked.v1`. Write the full handler body
  following `GrantRoleAssignmentHandler`'s exact structure (authorize → idempotency
  lookup → load the target `RoleAssignment` by id → `Revoke()` → bump revision →
  evidence/outbox/idempotency → commit). Do not skip the authorize-before-idempotency
  ordering.

- [x] **Step 5: Integration tests** — bootstrap → grant → replay (same idempotency
  key + same request → `Replayed: true`, no second `RoleAssignment` row) → reused key
  with a different request → `IdempotencyKeyReusedException` → revoke → attempt a
  second grant by the now-unauthorized revoked principal → `AuthorizationDeniedException`.
  Use `PostgresFixture` (Task 9) for a real transactional/RLS-backed run.

- [x] **Step 6: Commit**

```bash
dotnet format
git add src/Modules/Access/Application/ src/Modules/Access/Persistence/AccessDbContextTenantExtensions.cs \
        tests/Access.Tests/Integration/
git commit -m "feat(access): bootstrap handler + Grant/Revoke RoleAssignment commands (authorize-before-idempotency)"
```

---

## Task 8: Host registration
→ Commit: `b74cdb5` "feat(host): register AccessDbContext, IAuthorizer, IAccessScopeResolver; seed action registry at startup"

**Files:**
- Modify: `src/Host/Program.cs`

- [x] **Step 1: Register `AccessDbContext`, the PDP/scope-resolver, and seed the catalog**

```csharp
// src/Host/Program.cs — add alongside the existing AddDbContext calls:
using Access.Application;
using Access.Persistence;

builder.Services.AddDbContext<AccessDbContext>(options => options
    .UseNpgsql(
        builder.Configuration.GetConnectionString("Access") ?? AccessConnectionString.Resolve(),
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", AccessDbContext.AccessSchema))
    .UseSnakeCaseNamingConvention()
    .AddInterceptors(new RowVersionInterceptor()));

builder.Services.AddScoped<PrincipalResolver>();
builder.Services.AddScoped<IActionCatalog, AccessActionCatalogService>();
builder.Services.AddScoped<IAuthorizer, AccessAuthorizer>();
builder.Services.AddScoped<IAccessScopeResolver, AccessScopeResolver>();

// ... after `var app = builder.Build();`, before `app.Run();`:
using (var scope = app.Services.CreateScope())
{
    var accessDb = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
    await AccessActionCatalogSeeder.EnsureSeededAsync(accessDb);
}
```

- [x] **Step 2: Build and run migrations locally, verify**

```bash
dotnet ef database update \
  --project src/Modules/Access/Access.csproj \
  --startup-project src/Host/Host.csproj
dotnet build
```

Expected: 0 warnings, 0 errors; `access.*`/`identity.*` tables exist with RLS enabled
(spot-check: `psql -d fynovio_platform -c "\d+ access.role_assignments"` shows the
`tenant_isolation` policy).

- [x] **Step 3: Commit**

```bash
git add src/Host/Program.cs
git commit -m "feat(host): register AccessDbContext, IAuthorizer, IAccessScopeResolver; seed action registry at startup"
```

---

## Task 9: `tests/Access.Tests` — the full test project
→ Commit: `77a3ea4` "test(access): tests/Access.Tests + three real bugs found by running against real Postgres" — 33/33 passing, 109/109 across the full solution. This commit's message documents the three real bugs the test run caught (see status banner) — none were visible from code review alone.

**Files:**
- Create: `tests/Access.Tests/Access.Tests.csproj`
- Create: `tests/Access.Tests/Integration/PostgresFixture.cs`
- Create: `tests/Access.Tests/Integration/AccessRlsTests.cs`
- Create: `tests/Access.Tests/Architecture/ModuleBoundaryTests.cs`
- Modify: `tests/CRM.Tests/Architecture/ModuleBoundaryTests.cs` (symmetry check)
- (Consolidate here, if not already created in earlier tasks): `Domain/*`,
  `Application/*`, `Integration/BootstrapTenantAccessHandlerTests.cs`,
  `Integration/GrantRevokeRoleAssignmentHandlerTests.cs`

- [x] **Step 1: `.csproj`** (mirrors `tests/CRM.Tests/CRM.Tests.csproj`)

```xml
<!-- tests/Access.Tests/Access.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="NetArchTest.Rules" Version="1.3.2" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.15.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Modules\Access\Access.csproj" />
  </ItemGroup>

</Project>
```

- [x] **Step 2: `PostgresFixture`** (mirrors `tests/CRM.Tests/Integration/PostgresFixture.cs`)

```csharp
// tests/Access.Tests/Integration/PostgresFixture.cs
using Access.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Access.Tests.Integration;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("fynovio_platform_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private string? _runtimeConnectionString;

    public string AdminConnectionString => _container.GetConnectionString();

    public async Task<string> RuntimeConnectionStringAsync()
    {
        if (_runtimeConnectionString is not null)
            return _runtimeConnectionString;

        await using (var context = CreateAdminContext())
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE ROLE fynovio_app LOGIN PASSWORD 'runtime' NOSUPERUSER NOBYPASSRLS;
                GRANT USAGE ON SCHEMA identity TO fynovio_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA identity TO fynovio_app;
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA identity TO fynovio_app;
                GRANT USAGE ON SCHEMA access TO fynovio_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA access TO fynovio_app;
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA access TO fynovio_app;
                REVOKE UPDATE, DELETE ON access.evidence_records FROM fynovio_app;
                """);
        }

        var builder = new NpgsqlConnectionStringBuilder(AdminConnectionString) { Username = "fynovio_app", Password = "runtime" };
        _runtimeConnectionString = builder.ConnectionString;
        return _runtimeConnectionString;
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateAdminContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public AccessDbContext CreateAdminContext() => CreateContext(AdminConnectionString);

    public static AccessDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AccessDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", AccessDbContext.AccessSchema))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AccessDbContext(options);
    }
}
```

- [x] **Step 3: `AccessRlsTests`** — mirror the CRM tenant-isolation integration test
  pattern: seed two tenants' `Role`/`RoleAssignment` rows via the admin (superuser)
  connection, then connect via `RuntimeConnectionStringAsync()` (unprivileged
  `fynovio_app`), set `app.tenant_id` for tenant A, and assert tenant B's rows are
  invisible on every RLS-covered table from Task 4's and Task 5's migrations. This is FF03 — run as the
  unprivileged role, never the superuser (`AGENTS.md` binding-core #2).

- [x] **Step 4: `Architecture/ModuleBoundaryTests.cs`** (new, in `Access.Tests`)

```csharp
// tests/Access.Tests/Architecture/ModuleBoundaryTests.cs
using System.Reflection;
using Access.Persistence;
using NetArchTest.Rules;

namespace Access.Tests.Architecture;

public sealed class ModuleBoundaryTests
{
    private static readonly Assembly AccessAssembly = typeof(AccessDbContext).Assembly;

    [Fact]
    public void Access_does_not_depend_on_another_module()
    {
        var result = Types.InAssembly(AccessAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("CRM", "MasterData", "Organization", "TenantLifecycle")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Access_domain_does_not_depend_on_persistence()
    {
        var result = Types.InAssembly(AccessAssembly)
            .That().ResideInNamespace("Access.Domain")
            .ShouldNot().HaveDependencyOn("Access.Persistence")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.FailingTypeNames is null ? "OK" : string.Join(", ", result.FailingTypeNames);
}
```

- [x] **Step 5: Symmetry check in `tests/CRM.Tests`** — `ModuleBoundaryTests.
  Crm_does_not_depend_on_another_module` already lists `"Access"` in its
  `HaveDependencyOnAny` call (verified in Round 1's repo scan) — no change needed,
  just confirm it still passes once `Access.csproj` gains real content.

- [x] **Step 6: Wire the solution, run everything**

```bash
dotnet sln add tests/Access.Tests/Access.Tests.csproj   # if not auto-discovered
export DOCKER_HOST=unix://$HOME/.docker/run/docker.sock
dotnet test tests/Access.Tests/Access.Tests.csproj
dotnet test   # full suite — confirm no CRM/MasterData regression
```

Expected: all `Access.Tests` green; CRM.Tests and MasterData.Tests unchanged (46 + 30,
per the last recorded status).

- [x] **Step 7: `dotnet format` + commit**

```bash
dotnet format
git add tests/Access.Tests/
git commit -m "test(access): tests/Access.Tests — domain, architecture, application, RLS and handler integration tests"
```

---

## Task 10: Docs and CI sync
→ Commit: `c381629` "docs(access): sync identity-access-schema.md and AGENTS.md status to the rebuilt Access module"

**Files:**
- Modify: `docs/schema/identity-access-schema.md`
- Modify: `AGENTS.md` (Status section)
- Regenerate: `graphify-out/graph.json`, `graphify-out/GRAPH_REPORT.md`, `graphify-out/manifest.json`

- [x] **Step 1: Rewrite `docs/schema/identity-access-schema.md`** to Revision 4 —
  replace the ERD and design notes to match Tasks 2-7's actual shape: drop
  `PERMISSIONS`/`ROLE_PERMISSIONS`, add `ACTIONS`/`PERMISSION_SETS`/
  `PERMISSION_SET_ITEMS`/`ROLE_PERMISSION_SETS`/`TENANT_ACCESS_STATE`; `ROLES.tenant_id`
  is now `NOT NULL`; `ROLE_ASSIGNMENTS` drops `scope_type`/`scope_id`, gains
  `principal_type`/`source`/`granted_by_account_id`/`reason`. Reference this plan and
  the four review/decision documents as the design authority, same as the existing
  file references doc 19.

- [x] **Step 2: Update `AGENTS.md` "Status"** — replace "the Identity+Access schema
  (entities, `AccessDbContext`, first migration — no RLS, tests or Host registration
  yet)" with the new state: Access module rebuilt (ActionRegistry/PermissionSet/Role/
  RoleAssignment/TenantAccessState), RLS enabled, `tests/Access.Tests` (N tests) green,
  registered in `Host`, `IAuthorizer`/`IAccessScopeResolver` available for Phase 2 to
  consume. Note explicitly: **no CRM command yet calls `Authorize()`** — that's Phase 2.

- [x] **Step 3: Regenerate the graph**

```bash
graphify update .
```

- [x] **Step 4: Final full-suite verification**

```bash
dotnet build
dotnet format --verify-no-changes
dotnet ef migrations has-pending-model-changes \
  --project src/Modules/Access/Access.csproj \
  --startup-project src/Modules/Access/Access.csproj
export DOCKER_HOST=unix://$HOME/.docker/run/docker.sock
dotnet test
```

Expected: clean build, no format diffs, no pending model changes, full suite green.

- [x] **Step 5: Commit**

```bash
git add docs/schema/identity-access-schema.md AGENTS.md graphify-out/
git commit -m "docs(access): sync identity-access-schema.md, AGENTS.md status, and graphify to the rebuilt Access module"
```

---

## Definition of Done

- [x] All 10 tasks committed, each with a `→ Commit:` line filled in per
  `CLAUDE.md`'s plan checkbox tracking standard.
- [x] `tests/Access.Tests` exists and is green (domain, architecture/NetArchTest,
  application incl. the Authorize/ResolveAccessScope equivalence test, integration
  incl. RLS-as-unprivileged-role and bootstrap/grant/revoke).
- [x] `AccessDbContext` registered in `Host` (code-level: `AddDbContext` + DI wiring,
  commit `b74cdb5`). **Not verified/not done:** no local or deployment
  `ConnectionStrings__Access` has been set to the `fynovio_app` unprivileged role —
  the default connection string resolves to the `postgres` superuser, same gap CRM
  had until wired (`project-status` memory). This means RLS is proven by
  `tests/Access.Tests` (which does connect as `fynovio_app`) but does **not** yet
  protect a locally-run `Host` process. Flagging explicitly rather than checking
  this off as done — it's a real remaining task, likely bundled with Phase 2's own
  environment setup.
- [x] `dotnet ef migrations has-pending-model-changes` reports none for Access.
- [x] `dotnet format --verify-no-changes` passes.
- [x] Full `dotnet test` green — CRM (46) + MasterData (30) unchanged, Access (new)
  all passing.
- [x] `docs/schema/identity-access-schema.md` and `AGENTS.md` reflect the rebuilt
  module; `graphify-out/` regenerated.
- [x] **No CRM file was modified.** `Opportunity.Reassign()`, action-key enforcement
  on `CompleteOpportunityHandler`, and any HTTP surface are confirmed still Phase 2.
- [x] The two owner-open items from round 4 (system catalog template lifecycle,
  which this plan deliberately leaves un-automated per gap-closure §6; CRM owner
  field mapping, which this plan doesn't touch) remain correctly open — not silently
  resolved by this plan's code.
