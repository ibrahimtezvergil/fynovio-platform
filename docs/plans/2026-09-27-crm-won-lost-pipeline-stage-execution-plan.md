# CRM Won/Lost Pipeline-Stage Integration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use subagent-driven-development (recommended) or executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. A step's checkbox is marked done only after its commit exists — see the step that contains `git commit`, which gets a `→ Commit: `<hash>` "<message>"` line directly under it once that commit lands.

**Goal:** Make Won/Lost regular, board-visible pipeline stages (fixed `Kind`, editable label) instead of a status decoupled from `PipelineStageId`; guarantee no CRM-enabled tenant is ever pipeline-less; backfill existing data; and enforce the new invariants with a real `CHECK` constraint — exactly as resolved in `docs/architecture-analysis/2026-09-27-crm-opportunity-pipeline-won-lost-stage-integration.md` (read that document first; every decision below cites it and does not re-argue it).

**Architecture:** Five phases, **strict order, do not reorder** (the doc's own §5/§6 ordering — later phases assume earlier ones shipped): **A** foundation type (`PipelineStage.Kind`) → **B** no tenant may stay pipeline-less → **C** Won/Lost become real stages (aggregate + handlers + new command) → **D** one-time backfill of existing data → **E** the `CHECK` constraint and the authorization-boundary hardening that only make sense once A–D are in place.

**Tech Stack:** .NET/C#, EF Core (PostgreSQL), xUnit, the existing CRM module's idempotency/outbox/evidence handler template (`WinOpportunityHandler.cs` is the reference shape every command handler in this plan follows).

**Granularity note:** given this plan spans five ordered phases across two modules (CRM, and a Host-level orchestration touch), steps are grouped slightly coarser than the ideal 2–5 minutes where a group is mechanically inseparable (e.g. "add the property and its migration") — every step still contains complete, real code, no placeholders.

---

## File Structure

**New files:**
- `src/Modules/CRM/Application/PipelineNotProvisionedException.cs` — Phase B
- `src/Modules/CRM/Application/MoveOpportunityToPipelineCommand.cs`, `MoveOpportunityToPipelineHandler.cs`, `MoveOpportunityToPipelineResult.cs` — Phase C
- `src/Modules/CRM/Domain/OpportunityStageHistoryEntry.cs` — Phase C
- `src/Modules/CRM/Persistence/Configurations/OpportunityStageHistoryEntryConfiguration.cs` — Phase C
- `src/Host/Bootstrap/BackfillCrmPipelinesCommand.cs` (covers both the Phase B one-time provisioning pass and the Phase D backfill — one operator command, two idempotent sub-steps) — Phases B & D
- Migrations (Phase A, C, D, E — exact filenames chosen at Task time via `dotnet ef migrations add`, listed here with the name to pass)

**Modified files:**
- `src/Modules/CRM/Domain/PipelineStage.cs` — Phase A (`Kind`)
- `src/Modules/CRM/Domain/PipelineDefinitionVersion.cs` — Phase A (`AddWonStage`/`AddLostStage`/`BackfillSystemStage`)
- `src/Modules/CRM/Persistence/Configurations/PipelineStageConfiguration.cs` — Phase A
- `src/Modules/CRM/Application/OpenOpportunityHandler.cs` — Phase B
- `src/Modules/CRM/Application/SetPipelineLifecycleHandler.cs` — Phase B
- `src/Host/Bootstrap/EnableModuleCommand.cs` — Phase B
- `src/Host/Endpoints/CrmProblemDetailsExceptionHandler.cs` — Phase B
- `src/Modules/CRM/Application/ProvisionPipelineHandler.cs` — Phase C
- `src/Modules/CRM/Application/CreatePipelineDraftHandler.cs` (also holds `PublishPipelineVersionHandler`) — Phase C
- `src/Modules/CRM/Domain/Opportunity.cs` — Phase C (`ClosedFromStageId`, `Win`/`Lose` signatures, `MoveToPipeline`)
- `src/Modules/CRM/Persistence/Configurations/OpportunityConfiguration.cs` — Phase C, E
- `src/Modules/CRM/Application/WinOpportunityHandler.cs`, `LoseOpportunityHandler.cs` — Phase C
- `src/Modules/CRM/Application/ChangePipelineStageHandler.cs` — Phase C
- `src/Host/Program.cs` — Phase C (DI + endpoint registration)
- `src/Host/Endpoints/OpportunityEndpoints.cs` — Phase C

---

## Phase A — Foundation: `PipelineStage.Kind`

### Task 1: Add `PipelineStageKind` and `PipelineStage.Kind`

**Files:**
- Modify: `src/Modules/CRM/Domain/PipelineStage.cs`
- Test: `tests/CRM.Tests/Domain/PipelineStageTests.cs` (create if it doesn't already exist — check first)

- [x] **Step 1: Write the failing test**

```csharp
using Contracts;
using CRM.Domain;
using Xunit;

namespace CRM.Tests.Domain;

public sealed class PipelineStageKindTests
{
    [Fact]
    public void Create_defaults_to_open_kind()
    {
        var stage = PipelineStage.Create(TestData.NextTenant(), 1, "Qualification", 10, isEntry: true);

        Assert.Equal(PipelineStageKind.Open, stage.Kind);
    }

    [Fact]
    public void Create_accepts_an_explicit_kind()
    {
        var stage = PipelineStage.Create(TestData.NextTenant(), 1, "Won", 990, isEntry: false, PipelineStageKind.Won);

        Assert.Equal(PipelineStageKind.Won, stage.Kind);
    }
}
```

Run: `dotnet test tests/CRM.Tests --filter PipelineStageKindTests`
Expected: FAIL — `PipelineStageKind` and the 6-argument `Create` overload do not exist yet.

- [x] **Step 2: Add the enum and the property**

In `src/Modules/CRM/Domain/PipelineStage.cs`, add above the class:

```csharp
public enum PipelineStageKind
{
    Open,
    Won,
    Lost
}
```

Add the property (alongside the existing ones):

```csharp
    public PipelineStageKind Kind { get; private set; } = PipelineStageKind.Open;
```

Change the factory signature (existing signature grows an optional trailing parameter — every existing call site, which never passes it, keeps compiling unchanged):

```csharp
    internal static PipelineStage Create(TenantId tenantId, long pipelineDefinitionVersionId, string name, int sortOrder, bool isEntry, PipelineStageKind kind = PipelineStageKind.Open)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (sortOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order cannot be negative.");

        return new PipelineStage
        {
            TenantId = tenantId,
            PipelineDefinitionVersionId = pipelineDefinitionVersionId,
            Name = name,
            SortOrder = sortOrder,
            IsActive = true,
            IsEntry = isEntry,
            Kind = kind,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
```

- [x] **Step 3: Run test to verify it passes**

Run: `dotnet test tests/CRM.Tests --filter PipelineStageKindTests`
Expected: PASS

- [x] **Step 4: Run the full CRM.Tests suite to confirm no existing test broke**

Run: `dotnet test tests/CRM.Tests`
Expected: PASS, same count as before this task plus 2.

- [x] **Step 5: Commit**

```bash
git add src/Modules/CRM/Domain/PipelineStage.cs tests/CRM.Tests/Domain/PipelineStageKindTests.cs
git commit -m "feat(crm): add PipelineStage.Kind (Open/Won/Lost)"
```
→ Commit: `1c6a6ec` "feat(crm): add PipelineStage.Kind (Open/Won/Lost)"
(follow-up fix: `c88b0ff` "fix(crm): remove out-of-scope EF migration from Task 1 (belongs to Task 2)" — the implementer initially also generated an EF migration/model-snapshot change, which is Task 2's job with a different column shape; caught on spec review, removed)
(follow-up fix: `d138074` "fix(crm): ignore Kind property in EF configuration until Task 2" — added `builder.Ignore(s => s.Kind)` in `PipelineStageConfiguration.cs`, not in the original plan text, to stop EF's default convention from silently mapping the new property as an int column before Task 2's real configuration; code-quality-reviewed and approved. **Task 2's implementer must remove this `Ignore` line before adding the real `Property(...)` configuration.**)
Independently verified: 229/229 passing. Code-quality review: approved, no issues, "Ready to merge: YES."

### Task 2: Persist `Kind`, add the EF configuration and migration

**Files:**
- Modify: `src/Modules/CRM/Persistence/Configurations/PipelineStageConfiguration.cs`
- Create: `src/Modules/CRM/Persistence/Migrations/<timestamp>_AddPipelineStageKind.cs` (generated)

- [x] **Step 1: Add the property conversion to `PipelineStageConfiguration.cs`**

Add inside `Configure`, next to the other `builder.Property(...)` calls:

```csharp
        builder.Property(s => s.Kind)
            .HasConversion(k => ToDb(k), v => FromDb(v))
            .HasMaxLength(8)
            .HasDefaultValue(PipelineStageKind.Open)
            .IsRequired();
```

Add the CHECK constraint inside the existing `builder.ToTable("pipeline_stages", table => { ... })` block, alongside the other `table.HasCheckConstraint(...)` lines:

```csharp
            table.HasCheckConstraint("ck_pipeline_stages_kind", "kind IN ('open','won','lost')");
```

Add the two private conversion methods at the bottom of the class, mirroring `OpportunityConfiguration.ToDb`/`FromDb`:

```csharp
    private static string ToDb(PipelineStageKind kind) => kind switch
    {
        PipelineStageKind.Open => "open",
        PipelineStageKind.Won => "won",
        PipelineStageKind.Lost => "lost",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static PipelineStageKind FromDb(string value) => value switch
    {
        "open" => PipelineStageKind.Open,
        "won" => PipelineStageKind.Won,
        "lost" => PipelineStageKind.Lost,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
```

- [x] **Step 2: Generate the migration**

Run: `dotnet ef migrations add AddPipelineStageKind --project src/Modules/CRM --startup-project src/Host`
Expected: a new `<timestamp>_AddPipelineStageKind.cs`/`.Designer.cs` pair under `src/Modules/CRM/Persistence/Migrations/`, adding a `kind varchar(8) NOT NULL DEFAULT 'open'` column plus the CHECK constraint on `pipeline_stages`.

- [x] **Step 3: Apply it to the local dev database and verify**

Run: `dotnet ef database update --project src/Modules/CRM --startup-project src/Host`
Expected: succeeds; `\d pipeline_stages` in `psql` shows the new `kind` column and `ck_pipeline_stages_kind` constraint.

- [x] **Step 4: Run the full CRM.Tests suite**

Run: `dotnet test tests/CRM.Tests`
Expected: PASS.

- [x] **Step 5: Commit**

```bash
git add src/Modules/CRM/Persistence/Configurations/PipelineStageConfiguration.cs src/Modules/CRM/Persistence/Migrations/
git commit -m "feat(crm): persist PipelineStage.Kind"
```
→ Commit: `4e69a4b` "feat(crm): persist PipelineStage.Kind"
Independently verified: 229/229 passing; `psql \d crm.pipeline_stages` confirms the `kind varchar(8) NOT NULL DEFAULT 'open'` column and `ck_pipeline_stages_kind` CHECK constraint. Code-quality review: approved, no issues, "Ready to merge: Yes."

### Task 3: `PipelineDefinitionVersion.AddWonStage`/`AddLostStage`/`BackfillSystemStage`

**Files:**
- Modify: `src/Modules/CRM/Domain/PipelineDefinitionVersion.cs`
- Test: `tests/CRM.Tests/Domain/PipelineDefinitionVersionTests.cs` (extend the existing file)

This resolves a gap the architecture doc's §5 item 6.a did not itself specify a mechanism for: `AddStage` (and therefore any normal stage-adding path) already refuses to run unless `Status == Draft` (`PipelineDefinitionVersion.cs`, existing code: `if (Status != PipelineVersionStatus.Draft) throw new InvalidOperationException("Only a draft pipeline version can be edited.");`). The one-time backfill in Phase D must add Won/Lost stages to **already-`Published`/`Superseded`** versions, which that guard forbids. `BackfillSystemStage` below is the doc's own "deliberate, one-time exception to 4.3's immutable stage set" (§5 item 6.a), made explicit and narrow — it can never be reached from any normal HTTP-facing code path.

- [x] **Step 1: Write the failing tests**

Add to `tests/CRM.Tests/Domain/PipelineDefinitionVersionTests.cs`:

```csharp
    [Fact]
    public void AddWonStage_adds_a_won_kind_stage_to_a_draft_version()
    {
        var definition = PipelineDefinition.Create(TestData.NextTenant(), "Sales");
        var version = definition.AddVersion(1);

        var stage = version.AddWonStage("Won", 990);

        Assert.Equal(PipelineStageKind.Won, stage.Kind);
        Assert.Contains(stage, version.Stages);
    }

    [Fact]
    public void AddWonStage_rejects_a_second_won_stage()
    {
        var definition = PipelineDefinition.Create(TestData.NextTenant(), "Sales");
        var version = definition.AddVersion(1);
        version.AddWonStage("Won", 990);

        Assert.Throws<InvalidOperationException>(() => version.AddWonStage("Won again", 991));
    }

    [Fact]
    public void BackfillSystemStage_rejects_a_draft_version()
    {
        var definition = PipelineDefinition.Create(TestData.NextTenant(), "Sales");
        var version = definition.AddVersion(1);

        Assert.Throws<InvalidOperationException>(() => version.BackfillSystemStage(PipelineStageKind.Won, "Won", 990));
    }

    [Fact]
    public void BackfillSystemStage_rejects_kind_open()
    {
        var definition = PipelineDefinition.Create(TestData.NextTenant(), "Sales");
        var version = definition.AddVersion(1);
        version.AddStage("Qualification", 10);
        version.Publish();

        Assert.Throws<ArgumentException>(() => version.BackfillSystemStage(PipelineStageKind.Open, "Whatever", 990));
    }

    [Fact]
    public void BackfillSystemStage_adds_a_won_stage_to_a_published_version()
    {
        var definition = PipelineDefinition.Create(TestData.NextTenant(), "Sales");
        var version = definition.AddVersion(1);
        version.AddStage("Qualification", 10);
        version.Publish();

        var stage = version.BackfillSystemStage(PipelineStageKind.Won, "Won", 990);

        Assert.Equal(PipelineStageKind.Won, stage.Kind);
        Assert.False(stage.IsEntry);
    }
```

Run: `dotnet test tests/CRM.Tests --filter PipelineDefinitionVersionTests`
Expected: FAIL — `AddWonStage`/`AddLostStage`/`BackfillSystemStage` don't exist yet.

- [x] **Step 2: Implement the three methods**

Add to `src/Modules/CRM/Domain/PipelineDefinitionVersion.cs`, after `AddStage`:

```csharp
    public PipelineStage AddWonStage(string name, int sortOrder) => AddSystemStage(PipelineStageKind.Won, name, sortOrder);

    public PipelineStage AddLostStage(string name, int sortOrder) => AddSystemStage(PipelineStageKind.Lost, name, sortOrder);

    private PipelineStage AddSystemStage(PipelineStageKind kind, string name, int sortOrder)
    {
        if (Status != PipelineVersionStatus.Draft) throw new InvalidOperationException("Only a draft pipeline version can be edited.");
        if (_stages.Any(s => s.Kind == kind)) throw new InvalidOperationException($"This version already has a {kind} stage.");
        if (_stages.Any(s => s.SortOrder == sortOrder)) throw new InvalidOperationException($"Sort order {sortOrder} is already used on this version.");

        var stage = PipelineStage.Create(TenantId, Id, name, sortOrder, isEntry: false, kind);
        _stages.Add(stage);
        return stage;
    }

    /// <summary>One-time migration exception
    /// (docs/architecture-analysis/2026-09-27-crm-opportunity-pipeline-won-lost-stage-integration.md
    /// §5 item 6.a) — adds a system Won/Lost stage to an ALREADY Published or Superseded
    /// version, which every other mutation on this aggregate forbids. Never call this from
    /// normal application code; it exists solely for the one-time backfill operator command
    /// (Host.Bootstrap.BackfillCrmPipelinesCommand).</summary>
    public PipelineStage BackfillSystemStage(PipelineStageKind kind, string name, int sortOrder)
    {
        if (kind == PipelineStageKind.Open) throw new ArgumentException("Only Won/Lost system stages can be backfilled.", nameof(kind));
        if (Status is not (PipelineVersionStatus.Published or PipelineVersionStatus.Superseded))
            throw new InvalidOperationException("Backfill only targets a published or superseded version.");
        if (_stages.Any(s => s.Kind == kind)) throw new InvalidOperationException($"This version already has a {kind} stage.");
        if (_stages.Any(s => s.SortOrder == sortOrder)) throw new InvalidOperationException($"Sort order {sortOrder} is already used on this version.");

        var stage = PipelineStage.Create(TenantId, Id, name, sortOrder, isEntry: false, kind);
        _stages.Add(stage);
        return stage;
    }
```

- [x] **Step 3: Run tests to verify they pass**

Run: `dotnet test tests/CRM.Tests --filter PipelineDefinitionVersionTests`
Expected: PASS (5 new tests).

- [x] **Step 4: Commit**

```bash
git add src/Modules/CRM/Domain/PipelineDefinitionVersion.cs tests/CRM.Tests/Domain/PipelineDefinitionVersionTests.cs
git commit -m "feat(crm): add Won/Lost system-stage creation, including the one-time backfill exception"
```
→ Commit: `9790201` "feat(crm): add Won/Lost system-stage creation, including the one-time backfill exception"
Independently verified: full suite 234/234 passing (229 baseline + 5 new). Escalated code-quality review specifically confirmed the two guard directions (Draft-only vs Published/Superseded-only) are airtight and non-overlapping against all 4 `PipelineVersionStatus` values, and that `BackfillSystemStage` has zero callers outside its own tests at this point in the plan. "Ready to merge: Yes."

**Escalate for review here** — this task adds the one deliberate exception to an otherwise-absolute domain invariant ("only a draft version can be edited"). Per this project's own effort-escalation rule (state-machine invariants), get this specific task reviewed before moving on, even though the rest of Phase A doesn't need it.

---

## Phase B — No CRM-enabled tenant may stay pipeline-less

Implements doc §5 item 7 / §6.1. This phase is independent of Phase C and could ship on its own.

### Task 4: `PipelineNotProvisionedException` and its HTTP mapping

**Files:**
- Create: `src/Modules/CRM/Application/PipelineNotProvisionedException.cs`
- Modify: `src/Host/Endpoints/CrmProblemDetailsExceptionHandler.cs`
- Test: `tests/CRM.Tests/Application/OpenOpportunityHandlerTests.cs` (extend — written against this exception in Task 5)

- [ ] **Step 1: Create the exception**

```csharp
namespace CRM.Application;

/// <summary>Thrown by OpenOpportunityHandler when a tenant has CRM enabled but no usable
/// pipeline at all — no PipelineDefinition, or a PipelineDefinition with no Published
/// version. Distinct from PipelineConfigurationInvalidException (a pipeline exists and is
/// published, but its entry stage is missing/inactive). Amends
/// docs/architecture-analysis/2026-09-19-crm-phase2-authorization-delta-and-entry-stage-resolution.md's
/// "tenant has zero PipelineDefinitions → legal" table entry — see
/// docs/architecture-analysis/2026-09-27-crm-opportunity-pipeline-won-lost-stage-integration.md §6.1.</summary>
public sealed class PipelineNotProvisionedException() : InvalidOperationException(
    "This tenant has no usable sales pipeline yet. Provisioning should have happened automatically when CRM was enabled — contact an operator if this persists.");
```

- [ ] **Step 2: Add the HTTP mapping**

In `src/Host/Endpoints/CrmProblemDetailsExceptionHandler.cs`, add a new arm to the `switch`, next to `PipelineConfigurationInvalidException`:

```csharp
            PipelineNotProvisionedException => (StatusCodes.Status409Conflict, "pipeline_not_provisioned", exception.Message),
```

- [ ] **Step 3: Commit**

```bash
git add src/Modules/CRM/Application/PipelineNotProvisionedException.cs src/Host/Endpoints/CrmProblemDetailsExceptionHandler.cs
git commit -m "feat(crm): add PipelineNotProvisionedException and its 409 mapping"
```

### Task 5: `OpenOpportunityHandler` throws instead of silently opening with a null stage

**Files:**
- Modify: `src/Modules/CRM/Application/OpenOpportunityHandler.cs:118-151` (the `ResolveEntryStageAsync` method)
- Test: `tests/CRM.Tests/Application/OpenOpportunityHandlerTests.cs`

- [ ] **Step 1: Write the failing integration tests**

Add to `tests/CRM.Tests/Application/OpenOpportunityHandlerTests.cs` (follow that file's existing fixture/seeding conventions — check the top of the file for its `SeedAsync`-style helper before writing these):

```csharp
    [Fact]
    public async Task HandleAsync_throws_when_the_tenant_has_no_pipeline_definition_at_all()
    {
        var tenant = TestData.NextTenant();
        // no PipelineDefinition seeded for this tenant at all
        var opportunity = await SeedDraftOpportunityAsync(tenant);
        var handler = new OpenOpportunityHandler(Context, StubAuthorizer.AlwaysAllow);

        await Assert.ThrowsAsync<PipelineNotProvisionedException>(() =>
            handler.HandleAsync(new OpenOpportunityCommand(tenant, TestData.Seller, opportunity.Id, opportunity.RowVersion,
                DateTimeOffset.UtcNow.AddDays(7), Guid.NewGuid().ToString(), Guid.NewGuid())));
    }

    [Fact]
    public async Task HandleAsync_throws_when_the_pipeline_definition_has_no_published_version()
    {
        var tenant = TestData.NextTenant();
        var definition = PipelineDefinition.Create(tenant, "Sales");
        Context.PipelineDefinitions.Add(definition);
        await Context.SaveChangesAsync(); // definition.Id assigned, no version added — stays unpublished
        var opportunity = await SeedDraftOpportunityAsync(tenant);
        var handler = new OpenOpportunityHandler(Context, StubAuthorizer.AlwaysAllow);

        await Assert.ThrowsAsync<PipelineNotProvisionedException>(() =>
            handler.HandleAsync(new OpenOpportunityCommand(tenant, TestData.Seller, opportunity.Id, opportunity.RowVersion,
                DateTimeOffset.UtcNow.AddDays(7), Guid.NewGuid().ToString(), Guid.NewGuid())));
    }
```

(If the file has no `SeedDraftOpportunityAsync` helper yet, add one matching its existing seeding style, or reuse whichever helper already seeds a bare Draft `Opportunity` for this tenant — check `ChangePipelineStageHandlerTests.cs`'s `.SeedAsync`/`.LoadAsync` pattern for the established shape in this test suite before inventing a new one.)

Run: `dotnet test tests/CRM.Tests --filter OpenOpportunityHandlerTests`
Expected: FAIL — `ResolveEntryStageAsync` still returns `(null, null)`, so `Open()` succeeds instead of throwing.

- [ ] **Step 2: Change `ResolveEntryStageAsync`**

Replace the two early `return (null, null);` lines in `src/Modules/CRM/Application/OpenOpportunityHandler.cs`:

```csharp
        if (definitionId is not { } resolvedDefinitionId)
            return (null, null);
```
→
```csharp
        if (definitionId is not { } resolvedDefinitionId)
            throw new PipelineNotProvisionedException();
```

and

```csharp
        if (versionId is not { } resolvedVersionId)
            return (null, null);
```
→
```csharp
        if (versionId is not { } resolvedVersionId)
            throw new PipelineNotProvisionedException();
```

Update the method's doc comment (currently says "Returns (null, null) if the tenant has no PipelineDefinition/PipelineDefinitionVersion configured at all — Open() already treats that as valid.") to:

```csharp
    /// <summary>Resolves the tenant's single Opportunity pipeline, per this plan's own
    /// scope note: oldest PipelineDefinition, its highest-numbered version, that
    /// version's IsEntry stage. Throws PipelineNotProvisionedException if the tenant has
    /// no PipelineDefinition or no Published version at all — amended 2026-09-27; every
    /// CRM-enabled tenant is now guaranteed one by EnableModuleCommand (Task 8), so this
    /// is a real error, not an expected state, if it's ever hit. Once a version is
    /// resolved, it must have exactly one usable (IsEntry, IsActive) stage or opening is a
    /// hard error — a configured-but-invalid pipeline is a different case from "nothing
    /// provisioned" and must never silently open with a null stage (2026-09-19 entry-stage
    /// resolution's binding invariant, unchanged by this amendment).</summary>
```

- [ ] **Step 3: Run tests to verify they pass**

Run: `dotnet test tests/CRM.Tests --filter OpenOpportunityHandlerTests`
Expected: PASS.

- [ ] **Step 4: Run the full CRM.Tests suite**

Run: `dotnet test tests/CRM.Tests`
Expected: PASS. If any other existing test relied on opening an opportunity for a tenant with no pipeline (check `tests/CRM.Tests` for any `Open` test seeding zero `PipelineDefinition`s), update that test to seed a minimal published pipeline first — that test was exercising the exact behavior this task deliberately removes.

- [ ] **Step 5: Commit**

```bash
git add src/Modules/CRM/Application/OpenOpportunityHandler.cs tests/CRM.Tests/Application/OpenOpportunityHandlerTests.cs
git commit -m "fix(crm): OpenOpportunity fails loudly instead of opening with a null pipeline stage"
```

### Task 6: `SetPipelineLifecycleHandler` — never let a tenant archive its last active pipeline

**Files:**
- Modify: `src/Modules/CRM/Application/SetPipelineLifecycleHandler.cs:40-44`
- Test: `tests/CRM.Tests/Application/SetPipelineLifecycleHandlerTests.cs` (extend)

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public async Task HandleAsync_rejects_archiving_the_tenants_only_active_pipeline_even_with_no_CrmSettings_row()
    {
        var tenant = TestData.NextTenant();
        var definition = PipelineDefinition.Create(tenant, "Sales");
        Context.PipelineDefinitions.Add(definition);
        await Context.SaveChangesAsync();
        // deliberately no CrmSettings row for this tenant — the pre-fix gap this test closes
        var handler = new SetPipelineLifecycleHandler(Context, StubAuthorizer.AlwaysAllow);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new SetPipelineLifecycleCommand(tenant, TestData.Seller, definition.Id, definition.RowVersion,
                IsActive: false, Archive: true, Restore: false, Guid.NewGuid().ToString(), Guid.NewGuid())));
    }
```

Run: `dotnet test tests/CRM.Tests --filter SetPipelineLifecycleHandlerTests`
Expected: FAIL — today this archives successfully because no `CrmSettings` row exists to trigger the existing guard.

- [ ] **Step 2: Widen the guard**

Replace lines 40–44 of `src/Modules/CRM/Application/SetPipelineLifecycleHandler.cs`:

```csharp
        if ((!command.IsActive && !command.Restore) || command.Archive)
        {
            if (await context.CrmSettings.AnyAsync(x => x.TenantId == command.TenantId && x.DefaultPipelineDefinitionId == definition.Id, cancellationToken))
                throw new InvalidOperationException("Select another default pipeline before deactivating or archiving this pipeline.");
        }
```
→
```csharp
        if ((!command.IsActive && !command.Restore) || command.Archive)
        {
            if (await context.CrmSettings.AnyAsync(x => x.TenantId == command.TenantId && x.DefaultPipelineDefinitionId == definition.Id, cancellationToken))
                throw new InvalidOperationException("Select another default pipeline before deactivating or archiving this pipeline.");
            // No CrmSettings row does not mean "no guard needed" — a tenant with none
            // configured yet can still be relying on this being its only pipeline (doc
            // 2026-09-27 §6.1/§5 item 7.c: closes the reopen-the-gap path).
            var otherActivePipelineExists = await context.PipelineDefinitions.AnyAsync(
                x => x.TenantId == command.TenantId && x.Id != definition.Id && x.IsActive && !x.IsArchived, cancellationToken);
            if (!otherActivePipelineExists)
                throw new InvalidOperationException("A tenant must always keep at least one active pipeline.");
        }
```

- [ ] **Step 3: Run tests to verify they pass**

Run: `dotnet test tests/CRM.Tests --filter SetPipelineLifecycleHandlerTests`
Expected: PASS.

- [ ] **Step 4: Run the full CRM.Tests suite**

Run: `dotnet test tests/CRM.Tests`
Expected: PASS — check especially any existing test that archives a pipeline in a tenant seeded with only one; it may now need a second pipeline seeded first if it wasn't already exercising the default-pipeline guard.

- [ ] **Step 5: Commit**

```bash
git add src/Modules/CRM/Application/SetPipelineLifecycleHandler.cs tests/CRM.Tests/Application/SetPipelineLifecycleHandlerTests.cs
git commit -m "fix(crm): a tenant can never archive or deactivate its only active pipeline"
```

### Task 7: Default pipeline seed constants for production auto-provisioning

**Files:**
- Create: `src/Host/Bootstrap/CrmDefaultPipelineSeed.cs`

This is deliberately separate from `CrmDevSeed` (`src/Host/Authentication/CrmDevSeed.cs`), which is dev-only and never runs in production — Task 8 needs a production-appropriate default that both the CLI/bootstrap path and `CrmDevSeed` can share going forward, so the two never drift.

- [ ] **Step 1: Create the file**

```csharp
namespace Host.Bootstrap;

/// <summary>The minimal pipeline every CRM-enabled tenant gets automatically
/// (docs/architecture-analysis/2026-09-27-crm-opportunity-pipeline-won-lost-stage-integration.md
/// §5 item 7.a/§6.1) — one open-kind entry stage, nothing else. A tenant can rename or
/// extend it immediately after through the ordinary pipeline-draft endpoints; this exists
/// only so "CRM enabled" never means "cannot open an opportunity."</summary>
public static class CrmDefaultPipelineSeed
{
    public const string PipelineName = "Sales pipeline";
    public static readonly IReadOnlyList<string> ActiveStageNames = ["Open"];
}
```

- [ ] **Step 2: Commit**

```bash
git add src/Host/Bootstrap/CrmDefaultPipelineSeed.cs
git commit -m "feat(crm): define the minimal default pipeline seed for auto-provisioning"
```

### Task 8: Auto-provision on CRM module enablement, at the Host level

**Files:**
- Modify: `src/Host/Bootstrap/EnableModuleCommand.cs:50-86` (`EnableAsync`)
- Test: manual verification (this method is exercised by `tests/Host.Tests`'s bootstrap CLI tests — extend the closest existing one; check `tests/Host.Tests` for the file that already covers `EnableModuleCommand.EnableAsync` before adding a new one)

This is deliberately **not** added inside `EnableTenantModuleHandler` (`src/Modules/Access/Application/`) — Access must never depend on CRM application code (module-boundary rule). `EnableModuleCommand.EnableAsync` is the one shared Host-level entry point both the `enable-tenant-module` CLI command and `bootstrap-tenant-admin --modules` already go through, so hooking it here covers both callers.

- [ ] **Step 1: Add the CRM module key constant and provisioning call**

In `src/Host/Bootstrap/EnableModuleCommand.cs`, add a constant near the top of the class:

```csharp
    private const string CrmModuleKey = "crm";
```

Change the `EnableTenantModuleStatus.Enabled` case inside `EnableAsync`:

```csharp
            case EnableTenantModuleStatus.Enabled:
                await output.WriteLineAsync(
                    $"Module '{moduleKey}' enabled for tenant {tenantId.Value} (template v{result.EnabledVersion}); "
                    + $"{result.GrantedAssignments} administrator role assignment(s) created.");
                return Success;
```
→
```csharp
            case EnableTenantModuleStatus.Enabled:
                await output.WriteLineAsync(
                    $"Module '{moduleKey}' enabled for tenant {tenantId.Value} (template v{result.EnabledVersion}); "
                    + $"{result.GrantedAssignments} administrator role assignment(s) created.");
                if (string.Equals(moduleKey, CrmModuleKey, StringComparison.OrdinalIgnoreCase))
                {
                    var pipelineHandler = scopedServices.GetRequiredService<CRM.Application.ProvisionPipelineHandler>();
                    var pipelineResult = await pipelineHandler.HandleAsync(
                        new CRM.Application.ProvisionPipelineCommand(tenantId, CrmDefaultPipelineSeed.PipelineName, CrmDefaultPipelineSeed.ActiveStageNames, RetiredStageNames: []),
                        cancellationToken);
                    await output.WriteLineAsync(pipelineResult.Status == CRM.Application.ProvisionPipelineStatus.Provisioned
                        ? $"Default pipeline provisioned (version {pipelineResult.PipelineDefinitionVersionId})."
                        : "Tenant already had a pipeline; default pipeline not created.");
                }
                return Success;
```

- [ ] **Step 2: Compile and run the Host test suite**

Run: `dotnet build src/Host`
Expected: succeeds (adds a `using CRM.Application;` if the compiler flags the fully-qualified names as unnecessary — either form is fine, the plan uses fully-qualified names above to avoid ambiguity with `Access.Application`, already imported in this file).

Run: `dotnet test tests/Host.Tests --filter EnableModuleCommand`
Expected: PASS on existing tests; if none currently assert on CRM specifically, add one:

```csharp
    [Fact]
    public async Task EnableAsync_provisions_a_default_pipeline_when_enabling_crm()
    {
        var tenantId = await BootstrapTenantAsync();
        await using var scope = Services.CreateAsyncScope();

        var exitCode = await EnableModuleCommand.EnableAsync(scope.ServiceProvider, tenantId, "crm", Output, Error, CancellationToken.None);

        Assert.Equal(EnableModuleCommand.Success, exitCode);
        var crmContext = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        Assert.True(await crmContext.PipelineDefinitions.AnyAsync(p => p.TenantId == tenantId));
    }
```

(Adapt `BootstrapTenantAsync`/`Output`/`Error` to whatever this test class's existing fixture already exposes — check the top of the file housing the current `EnableModuleCommand` tests before adding this.)

- [ ] **Step 3: Run it**

Run: `dotnet test tests/Host.Tests --filter EnableAsync_provisions_a_default_pipeline_when_enabling_crm`
Expected: PASS.

- [ ] **Step 4: Manually verify against the local dev database**

Run: `dotnet run --project src/Host -- enable-tenant-module --tenant-id <a bootstrapped tenant with CRM not yet enabled> --module crm`
Expected output includes both the "Module 'crm' enabled..." line and "Default pipeline provisioned (version N)." Then confirm: `psql fynovio_platform -c "select * from pipeline_definitions where tenant_id = <id>;"` shows one row.

- [ ] **Step 5: Commit**

```bash
git add src/Host/Bootstrap/EnableModuleCommand.cs tests/Host.Tests/
git commit -m "feat(crm): auto-provision a default pipeline when the CRM module is enabled"
```

### Task 9: One-time provisioning pass for tenants already stuck pipeline-less

**Files:**
- Create: `src/Host/Bootstrap/BackfillCrmPipelinesCommand.cs`
- Modify: `src/Host/Program.cs` (register the command in whatever dispatch list routes `args[0]` to bootstrap commands — check how `EnableModuleCommand.IsRequested`/`RunAsync` are wired into `Program.cs`'s command dispatch and follow the same pattern)

This command has two idempotent sub-steps and is reused unchanged in Phase D (Task 19) for the actual Won/Lost backfill — written once, extended there, not duplicated.

- [ ] **Step 1: Create the command with just the provisioning sub-step (the backfill sub-step is added in Task 19)**

```csharp
using Contracts;
using CRM.Application;
using Microsoft.EntityFrameworkCore;

namespace Host.Bootstrap;

/// <summary>`dotnet Host.dll backfill-crm-pipelines` — a one-time operator command with two
/// idempotent sub-steps, run once each in the order the design doc requires
/// (docs/architecture-analysis/2026-09-27-crm-opportunity-pipeline-won-lost-stage-integration.md
/// §6.2): (1) provision a default pipeline for any CRM-enabled tenant that still has none
/// (closes the gap Task 8's auto-provision doesn't cover retroactively), then (2, added in
/// a later task) backfill Won/Lost stages and stage-less opportunities. Safe to re-run —
/// each sub-step only acts on tenants/rows still needing it.</summary>
public static class BackfillCrmPipelinesCommand
{
    public const string Name = "backfill-crm-pipelines";
    public const int Success = 0;
    public const int NotPermitted = BootstrapCommand.NotPermitted;

    public static bool IsRequested(string[] args) => args.Length > 0 && args[0] == Name;

    public static async Task<int> RunAsync(
        IServiceProvider services, IConfiguration configuration, string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("Bootstrap:Enabled", false))
        {
            await error.WriteLineAsync("Refused: set Bootstrap:Enabled=true (environment variable Bootstrap__Enabled=true) to run this command.");
            return NotPermitted;
        }

        await using var scope = services.CreateAsyncScope();
        var provisioned = await ProvisionMissingPipelinesAsync(scope.ServiceProvider, cancellationToken);
        await output.WriteLineAsync($"Provisioned a default pipeline for {provisioned} tenant(s) that had none.");
        return Success;
    }

    private static async Task<int> ProvisionMissingPipelinesAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var accessContext = scopedServices.GetRequiredService<Access.Persistence.AccessDbContext>();
        var pipelineHandler = scopedServices.GetRequiredService<ProvisionPipelineHandler>();
        var crmContext = scopedServices.GetRequiredService<CRM.Persistence.CrmDbContext>();

        var crmEnabledTenantIds = await accessContext.EnabledModules
            .Where(m => m.ModuleKey == "crm")
            .Select(m => m.TenantId)
            .ToListAsync(cancellationToken);

        var provisionedCount = 0;
        foreach (var tenantId in crmEnabledTenantIds)
        {
            await crmContext.SetTenantContextAsync(tenantId, cancellationToken);
            var alreadyHasOne = await crmContext.PipelineDefinitions.AnyAsync(p => p.TenantId == tenantId, cancellationToken);
            if (alreadyHasOne) continue;

            var result = await pipelineHandler.HandleAsync(
                new ProvisionPipelineCommand(tenantId, CrmDefaultPipelineSeed.PipelineName, CrmDefaultPipelineSeed.ActiveStageNames, RetiredStageNames: []),
                cancellationToken);
            if (result.Status == ProvisionPipelineStatus.Provisioned) provisionedCount++;
        }
        return provisionedCount;
    }
}
```

**Check before running this task's tests:** the exact entity/property names `Access.Persistence.AccessDbContext.EnabledModules` and its `ModuleKey`/`TenantId` shape are assumed from `EnableTenantModuleHandler`'s domain — confirm the real names with `grep -n "EnabledModule" src/Modules/Access/Domain/*.cs src/Modules/Access/Persistence/AccessDbContext.cs` before writing this step's final code; adjust the query above to match whatever that grep turns up if it differs.

- [ ] **Step 2: Wire the command into `Program.cs`'s dispatch**

Find where `EnableModuleCommand.IsRequested(args)` is checked in `src/Host/Program.cs` (search for `EnableModuleCommand.IsRequested`) and add a sibling branch immediately after it, calling `BackfillCrmPipelinesCommand.IsRequested`/`RunAsync` the same way.

- [ ] **Step 3: Write a test**

```csharp
    [Fact]
    public async Task RunAsync_provisions_a_pipeline_for_a_crm_enabled_tenant_with_none()
    {
        var tenantId = await BootstrapTenantAsync();
        await using var scope = Services.CreateAsyncScope();
        await EnableModuleCommand.EnableAsync(scope.ServiceProvider, tenantId, "crm", Output, Error, CancellationToken.None);
        // simulate the pre-Task-8 gap: delete the auto-provisioned pipeline directly for this test
        var crmContext = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        crmContext.PipelineDefinitions.RemoveRange(crmContext.PipelineDefinitions.Where(p => p.TenantId == tenantId));
        await crmContext.SaveChangesAsync();

        var exitCode = await BackfillCrmPipelinesCommand.RunAsync(Services, Configuration, ["backfill-crm-pipelines"], Output, Error, CancellationToken.None);

        Assert.Equal(BackfillCrmPipelinesCommand.Success, exitCode);
        Assert.True(await crmContext.PipelineDefinitions.AnyAsync(p => p.TenantId == tenantId));
    }
```

- [ ] **Step 4: Run it**

Run: `dotnet test tests/Host.Tests --filter RunAsync_provisions_a_pipeline_for_a_crm_enabled_tenant_with_none`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Host/Bootstrap/BackfillCrmPipelinesCommand.cs src/Host/Program.cs tests/Host.Tests/
git commit -m "feat(crm): one-time operator pass to provision pipelines for already-enabled tenants that have none"
```

**Phase B is now complete and independently shippable.** Nothing in Phase C depends on anything below this line being merged first, but the plan's required ordering (doc §6.2) still means Phase C should land before Phase D's backfill runs.

---

## Phase C — Won/Lost become real stages

### Task 10: `ProvisionPipelineHandler` and pipeline drafts always get Won/Lost stages

**Files:**
- Modify: `src/Modules/CRM/Application/ProvisionPipelineHandler.cs:36-51`
- Modify: `src/Modules/CRM/Application/CreatePipelineDraftHandler.cs:56-64` (the stage-building block inside `HandleAsync`)
- Test: `tests/CRM.Tests/Application/ProvisionPipelineHandlerTests.cs`, `tests/CRM.Tests/Application/CreatePipelineDraftHandlerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
    // ProvisionPipelineHandlerTests.cs
    [Fact]
    public async Task HandleAsync_gives_the_new_pipeline_a_won_and_a_lost_stage()
    {
        var tenant = TestData.NextTenant();
        var handler = new ProvisionPipelineHandler(Context);

        var result = await handler.HandleAsync(new ProvisionPipelineCommand(tenant, "Sales", ["Open"], RetiredStageNames: []));

        var stages = await Context.PipelineStages.Where(s => s.TenantId == tenant && s.PipelineDefinitionVersionId == result.PipelineDefinitionVersionId).ToListAsync();
        Assert.Contains(stages, s => s.Kind == PipelineStageKind.Won);
        Assert.Contains(stages, s => s.Kind == PipelineStageKind.Lost);
    }
```

```csharp
    // CreatePipelineDraftHandlerTests.cs — extend whichever existing test seeds a full draft+publish
    // round trip with an assertion that the published version's stages include both kinds.
    [Fact]
    public async Task Publishing_a_new_draft_always_includes_won_and_lost_stages()
    {
        // build via this file's existing CreatePipelineDraftCommand-building helper, then assert:
        var stages = await Context.PipelineStages.Where(s => s.TenantId == tenantId && s.PipelineDefinitionVersionId == versionId).ToListAsync();
        Assert.Single(stages, s => s.Kind == PipelineStageKind.Won);
        Assert.Single(stages, s => s.Kind == PipelineStageKind.Lost);
    }
```

Run: `dotnet test tests/CRM.Tests --filter "ProvisionPipelineHandlerTests|CreatePipelineDraftHandlerTests"`
Expected: FAIL — neither handler creates Won/Lost stages yet.

- [ ] **Step 2: Fix `ProvisionPipelineHandler`**

In `src/Modules/CRM/Application/ProvisionPipelineHandler.cs`, right before `version.Publish();`:

```csharp
        version.Publish();
```
→
```csharp
        version.AddWonStage("Won", sortOrder);
        context.PipelineStages.Add(version.Stages.Last());
        sortOrder += 10;
        version.AddLostStage("Lost", sortOrder);
        context.PipelineStages.Add(version.Stages.Last());

        version.Publish();
```

(`version.Stages.Last()` is safe here because `AddWonStage`/`AddLostStage` always append to the same `_stages` list this handler is already iterating — same pattern the existing `foreach` loops above it use implicitly by tracking the returned stage directly; if this feels fragile, capture the return value instead: `var won = version.AddWonStage("Won", sortOrder); context.PipelineStages.Add(won);` — prefer that form, shown correctly here:)

```csharp
        var wonStage = version.AddWonStage("Won", sortOrder);
        context.PipelineStages.Add(wonStage);
        sortOrder += 10;
        var lostStage = version.AddLostStage("Lost", sortOrder);
        context.PipelineStages.Add(lostStage);

        version.Publish();
```

- [ ] **Step 3: Fix `CreatePipelineDraftHandler`**

In `src/Modules/CRM/Application/CreatePipelineDraftHandler.cs`, after the existing stage-building block (right after the line `version.MarkEntry(stages[...]);` and before `context.PipelineStages.AddRange(stages);`), add:

```csharp
        var systemSortOrder = stages.Max(s => s.SortOrder) + 10;
        var wonStage = version.AddWonStage("Won", systemSortOrder);
        var lostStage = version.AddLostStage("Lost", systemSortOrder + 10);
```

Change `context.PipelineStages.AddRange(stages);` to also include the two new stages:

```csharp
        context.PipelineStages.AddRange(stages);
        context.PipelineStages.Add(wonStage);
        context.PipelineStages.Add(lostStage);
```

Also allocate their configuration ids the same way every other stage in this handler already does (find the existing `foreach (var (stage, input) in stages.Zip(...))` loop that calls `context.Entry(stage).Property(x => x.Id).CurrentValue = await context.AllocateConfigurationIdAsync<PipelineStage>(cancellationToken);` and add two more calls for `wonStage`/`lostStage` right after that loop):

```csharp
        context.Entry(wonStage).Property(x => x.Id).CurrentValue = await context.AllocateConfigurationIdAsync<PipelineStage>(cancellationToken);
        context.Entry(lostStage).Property(x => x.Id).CurrentValue = await context.AllocateConfigurationIdAsync<PipelineStage>(cancellationToken);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/CRM.Tests --filter "ProvisionPipelineHandlerTests|CreatePipelineDraftHandlerTests"`
Expected: PASS.

- [ ] **Step 5: Run the full CRM.Tests suite**

Run: `dotnet test tests/CRM.Tests`
Expected: PASS — this is a good place to check whether any existing test asserts an exact `PipelineStages.Count()` for a provisioned/drafted pipeline (it would now be 2 higher); update those counts rather than treat them as unrelated failures.

- [ ] **Step 6: Commit**

```bash
git add src/Modules/CRM/Application/ProvisionPipelineHandler.cs src/Modules/CRM/Application/CreatePipelineDraftHandler.cs tests/CRM.Tests/Application/ProvisionPipelineHandlerTests.cs tests/CRM.Tests/Application/CreatePipelineDraftHandlerTests.cs
git commit -m "feat(crm): every provisioned or drafted pipeline gets a Won and a Lost stage"
```

### Task 11: `PublishPipelineVersionHandler.Validate` enforces the Won/Lost shape

**Files:**
- Modify: `src/Modules/CRM/Application/CreatePipelineDraftHandler.cs:219-227` (`PublishPipelineVersionHandler.Validate`)
- Test: `tests/CRM.Tests/Application/CreatePipelineDraftHandlerTests.cs` (the `PublishPipelineVersionHandler.Validate` unit tests, if any exist as a separate `internal` test target — check; otherwise add via the public `PublishPipelineVersionHandler.HandleAsync` path)

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public void Validate_rejects_a_version_missing_a_lost_stage()
    {
        var version = /* build via this test file's existing PipelineDefinitionVersion test-construction helper */;
        var stages = new List<PipelineStage> { /* one Open stage, one Won stage, deliberately no Lost stage */ };

        var errors = PublishPipelineVersionHandler.Validate(version, stages, transitions: []);

        Assert.Contains(errors, e => e.Contains("Lost", StringComparison.OrdinalIgnoreCase));
    }
```

Run: `dotnet test tests/CRM.Tests --filter Validate_rejects_a_version_missing_a_lost_stage`
Expected: FAIL — `Validate` doesn't check for Won/Lost shape yet.

- [ ] **Step 2: Extend `Validate`**

Replace the body of `internal static List<string> Validate(...)` in `src/Modules/CRM/Application/CreatePipelineDraftHandler.cs`:

```csharp
    internal static List<string> Validate(PipelineDefinitionVersion version, IReadOnlyList<PipelineStage> stages, IReadOnlyList<PipelineStageTransition> transitions)
    {
        var errors = new List<string>();
        if (stages.Count == 0) errors.Add("At least one stage is required.");
        if (stages.Count(x => x.IsEntry && x.IsActive) != 1) errors.Add("Exactly one active entry stage is required.");
        var ids = stages.Where(x => x.IsActive).Select(x => x.Id).ToHashSet();
        if (version.EnforceAllowedTransitions && transitions.Any(x => !ids.Contains(x.FromStageId) || !ids.Contains(x.ToStageId))) errors.Add("Every allowed transition must connect active stages in this version.");

        var wonStages = stages.Where(x => x.Kind == PipelineStageKind.Won).ToList();
        var lostStages = stages.Where(x => x.Kind == PipelineStageKind.Lost).ToList();
        if (wonStages.Count != 1) errors.Add("Exactly one Won-kind stage is required.");
        if (lostStages.Count != 1) errors.Add("Exactly one Lost-kind stage is required.");
        if (wonStages.Concat(lostStages).Any(x => !x.IsActive)) errors.Add("The Won and Lost stages must be active.");
        if (wonStages.Concat(lostStages).Any(x => x.IsEntry)) errors.Add("The Won and Lost stages cannot be the entry stage.");
        var lastOpenSortOrder = stages.Where(x => x.Kind == PipelineStageKind.Open).Select(x => (int?)x.SortOrder).Max() ?? -1;
        if (wonStages.Concat(lostStages).Any(x => x.SortOrder <= lastOpenSortOrder)) errors.Add("The Won and Lost stages must sort after every Open-kind stage.");
        if (version.EnforceAllowedTransitions && transitions.Any(x => wonStages.Concat(lostStages).Any(system => system.Id == x.ToStageId)))
            errors.Add("No configured transition may target a Won or Lost stage — Win/Lose are dedicated commands, not pipeline-stage moves.");

        return errors;
    }
```

- [ ] **Step 3: Run tests to verify they pass**

Run: `dotnet test tests/CRM.Tests --filter Validate_rejects_a_version_missing_a_lost_stage`
Expected: PASS.

- [ ] **Step 4: Run the full CRM.Tests suite**

Run: `dotnet test tests/CRM.Tests`
Expected: PASS — since Task 10 already guarantees every drafted/provisioned pipeline has both stages, this should not break any test that goes through the normal handlers; it would only catch a test that hand-builds an invalid version directly.

- [ ] **Step 5: Commit**

```bash
git add src/Modules/CRM/Application/CreatePipelineDraftHandler.cs tests/CRM.Tests/Application/CreatePipelineDraftHandlerTests.cs
git commit -m "feat(crm): PublishPipelineVersion enforces exactly one Won and one Lost stage, correctly shaped"
```

### Task 12: `Opportunity.ClosedFromStageId`

**Files:**
- Modify: `src/Modules/CRM/Domain/Opportunity.cs`
- Modify: `src/Modules/CRM/Persistence/Configurations/OpportunityConfiguration.cs`
- Test: `tests/CRM.Tests/Domain/OpportunityStateMachineTests.cs`

- [ ] **Step 1: Add the property**

In `src/Modules/CRM/Domain/Opportunity.cs`, add next to `PipelineStageId`:

```csharp
    public long? ClosedFromStageId { get; private set; }
```

- [ ] **Step 2: Add the EF mapping and FK**

In `src/Modules/CRM/Persistence/Configurations/OpportunityConfiguration.cs`, add after the existing `PipelineStage` `HasOne` block:

```csharp
        builder.HasOne<PipelineStage>()
            .WithMany()
            .HasForeignKey(o => o.ClosedFromStageId)
            .HasPrincipalKey(s => s.Id)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
```

- [ ] **Step 3: Generate and apply the migration**

Run: `dotnet ef migrations add AddOpportunityClosedFromStage --project src/Modules/CRM --startup-project src/Host`
Run: `dotnet ef database update --project src/Modules/CRM --startup-project src/Host`
Expected: a nullable `closed_from_stage_id bigint` column with an FK to `pipeline_stages(id)`.

- [ ] **Step 4: Run the full CRM.Tests suite**

Run: `dotnet test tests/CRM.Tests`
Expected: PASS (this task adds no new tests on its own — Task 13 exercises the field through `Win`/`Lose`).

- [ ] **Step 5: Commit**

```bash
git add src/Modules/CRM/Domain/Opportunity.cs src/Modules/CRM/Persistence/Configurations/OpportunityConfiguration.cs src/Modules/CRM/Persistence/Migrations/
git commit -m "feat(crm): add Opportunity.ClosedFromStageId"
```

### Task 13: `Win`/`Lose` resolve and set the tenant's Won/Lost stage

**Files:**
- Modify: `src/Modules/CRM/Domain/Opportunity.cs:124-157` (`Win`, `Lose`)
- Modify: `src/Modules/CRM/Application/WinOpportunityHandler.cs:73-77`
- Modify: `src/Modules/CRM/Application/LoseOpportunityHandler.cs:60-73`
- Test: `tests/CRM.Tests/Domain/OpportunityStateMachineTests.cs`, `tests/CRM.Tests/Application/WinOpportunityHandlerTests.cs`, `tests/CRM.Tests/Application/LoseOpportunityHandlerTests.cs`

The new stage parameters are **optional and default to a no-op** (leave `PipelineStageId` exactly as it was), so every existing `.Win(...)`/`.Lose(...)` call in the current test suite keeps compiling and passing unchanged — only the handlers (which now always resolve a real value) exercise the new behavior. This is a deliberate choice: the aggregate shouldn't force dozens of unrelated existing tests (line handling, money rounding, row-version bumps) to start caring about pipeline plumbing they were never testing.

- [ ] **Step 1: Write the failing domain tests**

Add to `tests/CRM.Tests/Domain/OpportunityStateMachineTests.cs`:

```csharp
    [Fact]
    public void Win_sets_the_won_stage_and_records_where_it_closed_from()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: 1, pipelineStageId: 7);
        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 100m);

        opportunity.Win(wonStageId: 99);

        Assert.Equal(99, opportunity.PipelineStageId);
        Assert.Equal(7, opportunity.ClosedFromStageId);
    }

    [Fact]
    public void Win_without_a_stage_argument_leaves_the_pipeline_stage_untouched()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: 1, pipelineStageId: 7);
        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 100m);

        opportunity.Win();

        Assert.Equal(7, opportunity.PipelineStageId);
    }

    [Fact]
    public void Lose_from_open_sets_the_lost_stage_and_records_where_it_closed_from()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: 1, pipelineStageId: 7);

        opportunity.Lose("müşteri vazgeçti", lostStageId: 100);

        Assert.Equal(100, opportunity.PipelineStageId);
        Assert.Equal(7, opportunity.ClosedFromStageId);
    }

    [Fact]
    public void Lose_from_draft_leaves_both_stage_fields_null()
    {
        var opportunity = NewDraftOpportunity();

        opportunity.Lose("hiç açılmadı");

        Assert.Null(opportunity.PipelineStageId);
        Assert.Null(opportunity.ClosedFromStageId);
    }
```

Run: `dotnet test tests/CRM.Tests --filter OpportunityStateMachineTests`
Expected: FAIL — `Win`/`Lose` don't accept a stage argument yet.

- [ ] **Step 2: Change the aggregate methods**

Replace in `src/Modules/CRM/Domain/Opportunity.cs`:

```csharp
    public void Win(bool requireActiveRequiredLine = true)
    {
        EnsureNotArchived();
        if (Status != OpportunityStatus.Open)
            throw new InvalidOperationException($"Cannot win an opportunity in status {Status}. It must be open first.");

        var billableLines = _lines.Where(line => !line.IsOptional && !line.IsCanceled).ToList();
        if (requireActiveRequiredLine && billableLines.Count == 0)
            throw new InvalidOperationException("Cannot win an opportunity without at least one active required line.");

        // line_total is NULL on rows persisted before it was computed; derive it rather than count it as zero.
        var computedTotal = billableLines.Sum(line => line.LineTotal ?? line.Quantity * line.UnitPrice);

        Status = OpportunityStatus.Won;
        // Single declared rounding point (17 §3.4): 4dp computed total rounds to the
        // currency's 2dp minor unit exactly once, here.
        TotalAmount = decimal.Round(computedTotal, 2, MidpointRounding.AwayFromZero);
        WonDate = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Lose(string lostReason)
    {
        EnsureNotArchived();
        if (Status is OpportunityStatus.Won or OpportunityStatus.Lost)
            throw new InvalidOperationException($"Cannot lose an opportunity in status {Status}.");
        if (string.IsNullOrWhiteSpace(lostReason))
            throw new ArgumentException("Lost reason is required.", nameof(lostReason));

        Status = OpportunityStatus.Lost;
        LostReason = lostReason;
        LostDate = DateTimeOffset.UtcNow;
        Touch();
    }
```
→
```csharp
    public void Win(long? wonStageId = null, bool requireActiveRequiredLine = true)
    {
        EnsureNotArchived();
        if (Status != OpportunityStatus.Open)
            throw new InvalidOperationException($"Cannot win an opportunity in status {Status}. It must be open first.");

        var billableLines = _lines.Where(line => !line.IsOptional && !line.IsCanceled).ToList();
        if (requireActiveRequiredLine && billableLines.Count == 0)
            throw new InvalidOperationException("Cannot win an opportunity without at least one active required line.");

        // line_total is NULL on rows persisted before it was computed; derive it rather than count it as zero.
        var computedTotal = billableLines.Sum(line => line.LineTotal ?? line.Quantity * line.UnitPrice);

        // Doc 2026-09-27 §5 item 3: record where it closed from before overwriting the
        // live stage. wonStageId defaults to null, which leaves PipelineStageId untouched
        // (today's behavior) — only WinOpportunityHandler passes a real resolved value.
        ClosedFromStageId = PipelineStageId;
        PipelineStageId = wonStageId ?? PipelineStageId;

        Status = OpportunityStatus.Won;
        // Single declared rounding point (17 §3.4): 4dp computed total rounds to the
        // currency's 2dp minor unit exactly once, here.
        TotalAmount = decimal.Round(computedTotal, 2, MidpointRounding.AwayFromZero);
        WonDate = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Lose(string lostReason, long? lostStageId = null)
    {
        EnsureNotArchived();
        if (Status is OpportunityStatus.Won or OpportunityStatus.Lost)
            throw new InvalidOperationException($"Cannot lose an opportunity in status {Status}.");
        if (string.IsNullOrWhiteSpace(lostReason))
            throw new ArgumentException("Lost reason is required.", nameof(lostReason));

        // Doc 2026-09-27 §4.1: a Draft->Lost opportunity never entered a pipeline, so both
        // stage fields correctly stay null here — lostStageId is null in that path because
        // LoseOpportunityHandler only resolves one when Status is already Open.
        ClosedFromStageId = PipelineStageId;
        PipelineStageId = lostStageId ?? PipelineStageId;

        Status = OpportunityStatus.Lost;
        LostReason = lostReason;
        LostDate = DateTimeOffset.UtcNow;
        Touch();
    }
```

- [ ] **Step 3: Run the domain tests**

Run: `dotnet test tests/CRM.Tests --filter OpportunityStateMachineTests`
Expected: PASS.

- [ ] **Step 4: Wire the handlers to resolve and pass a real stage id — write the failing handler tests first**

```csharp
    // WinOpportunityHandlerTests.cs
    [Fact]
    public async Task HandleAsync_moves_the_opportunity_onto_the_pipelines_won_stage()
    {
        // seed a tenant with a published pipeline (via ProvisionPipelineHandler, which now
        // always creates a Won stage per Task 10), open an opportunity against it, add a line
        var wonStage = await Context.PipelineStages.SingleAsync(s => s.TenantId == tenant && s.PipelineDefinitionVersionId == versionId && s.Kind == PipelineStageKind.Won);
        var handler = new WinOpportunityHandler(Context, StubAuthorizer.AlwaysAllow);

        await handler.HandleAsync(new WinOpportunityCommand(tenant, TestData.Seller, opportunity.Id, opportunity.RowVersion, Guid.NewGuid().ToString(), Guid.NewGuid()));

        var reloaded = await Context.Opportunities.SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(wonStage.Id, reloaded.PipelineStageId);
    }
```

```csharp
    // LoseOpportunityHandlerTests.cs — same shape, asserting against the Lost-kind stage.
```

Run: `dotnet test tests/CRM.Tests --filter "WinOpportunityHandlerTests|LoseOpportunityHandlerTests"`
Expected: FAIL — the handlers still call `opportunity.Win(requireWonLine)`/`opportunity.Lose(resolvedReason ?? command.LostReason)` with no stage.

- [ ] **Step 5: Fix `WinOpportunityHandler`**

Replace:

```csharp
        var requireWonLine = await context.CrmSettings.AsNoTracking()
            .Where(settings => settings.TenantId == command.TenantId)
            .Select(settings => (bool?)settings.RequireWonLine)
            .SingleOrDefaultAsync(cancellationToken) ?? true;
        opportunity.Win(requireWonLine);
```
→
```csharp
        var requireWonLine = await context.CrmSettings.AsNoTracking()
            .Where(settings => settings.TenantId == command.TenantId)
            .Select(settings => (bool?)settings.RequireWonLine)
            .SingleOrDefaultAsync(cancellationToken) ?? true;
        var wonStageId = opportunity.PipelineDefinitionVersionId is { } versionId
            ? await context.PipelineStages.AsNoTracking()
                .Where(s => s.TenantId == command.TenantId && s.PipelineDefinitionVersionId == versionId && s.Kind == PipelineStageKind.Won)
                .Select(s => (long?)s.Id)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        opportunity.Win(wonStageId, requireWonLine);
```

- [ ] **Step 6: Fix `LoseOpportunityHandler`**

Replace `opportunity.Lose(resolvedReason ?? command.LostReason);` with:

```csharp
        var lostStageId = opportunity.Status == OpportunityStatus.Open && opportunity.PipelineDefinitionVersionId is { } versionId
            ? await context.PipelineStages.AsNoTracking()
                .Where(s => s.TenantId == command.TenantId && s.PipelineDefinitionVersionId == versionId && s.Kind == PipelineStageKind.Lost)
                .Select(s => (long?)s.Id)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        opportunity.Lose(resolvedReason ?? command.LostReason, lostStageId);
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test tests/CRM.Tests --filter "WinOpportunityHandlerTests|LoseOpportunityHandlerTests"`
Expected: PASS.

- [ ] **Step 8: Run the full CRM.Tests suite**

Run: `dotnet test tests/CRM.Tests`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/Modules/CRM/Domain/Opportunity.cs src/Modules/CRM/Application/WinOpportunityHandler.cs src/Modules/CRM/Application/LoseOpportunityHandler.cs tests/CRM.Tests/
git commit -m "feat(crm): Win/Lose move the opportunity onto the pipeline's Won/Lost stage"
```

**Escalate for review here** — this task changes the state-machine invariants of `Win`/`Lose`, the two commands with the strictest existing rules (line validation, rounding, lost-reason requirement). Get it reviewed before Task 14.

### Task 14: Board drag onto Won/Lost must never become `ChangePipelineStage`

**Files:**
- Modify: `src/Modules/CRM/Application/ChangePipelineStageHandler.cs:67-73`
- Test: `tests/CRM.Tests/Application/ChangePipelineStageHandlerTests.cs`

Implements doc §4.2 — the authorization-boundary requirement. This is the test the doc's own review specifically demanded.

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public async Task HandleAsync_rejects_targeting_the_wons_stage_even_with_authorization_granted()
    {
        // seed a tenant, open an opportunity against a published pipeline with its Task-10 Won stage
        var wonStage = await Context.PipelineStages.SingleAsync(s => s.TenantId == tenant && s.PipelineDefinitionVersionId == versionId && s.Kind == PipelineStageKind.Won);
        var handler = new ChangePipelineStageHandler(Context, StubAuthorizer.AlwaysAllow); // authorization is not the thing under test — it must fail even when allowed

        await Assert.ThrowsAsync<InvalidPipelineTransitionException>(() =>
            handler.HandleAsync(new ChangePipelineStageCommand(tenant, TestData.Seller, opportunity.Id, opportunity.RowVersion, wonStage.Id, Guid.NewGuid().ToString(), Guid.NewGuid())));

        var reloaded = await Context.Opportunities.SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(OpportunityStatus.Open, reloaded.Status); // board drag never silently wins it
    }
```

Run: `dotnet test tests/CRM.Tests --filter HandleAsync_rejects_targeting_the_wons_stage_even_with_authorization_granted`
Expected: FAIL — today `ChangePipelineStageHandler` happily moves the opportunity onto any active stage in its version, Won/Lost included, and never calls `Win`/`Lose`.

- [ ] **Step 2: Add the rejection**

In `src/Modules/CRM/Application/ChangePipelineStageHandler.cs`, right after the existing `if (!targetStage.IsActive) throw ...` check and before the `EnforceAllowedTransitions` block:

```csharp
        if (!targetStage.IsActive)
            throw new InvalidPipelineTransitionException(opportunity.Id, command.TargetStageId, "stage is retired and not a valid target for new transitions");
        if (targetStage.Kind != PipelineStageKind.Open)
            throw new InvalidPipelineTransitionException(opportunity.Id, command.TargetStageId, "Won and Lost stages can only be reached through the Win/Lose commands, never ChangePipelineStage");
```

(This check is unconditional — it runs regardless of the tenant's `EnforceAllowedTransitions` setting, deliberately, per doc §4.2: that flag must never be able to turn off this specific rule.)

- [ ] **Step 3: Run tests to verify they pass**

Run: `dotnet test tests/CRM.Tests --filter HandleAsync_rejects_targeting_the_wons_stage_even_with_authorization_granted`
Expected: PASS.

- [ ] **Step 4: Run the full CRM.Tests suite**

Run: `dotnet test tests/CRM.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Modules/CRM/Application/ChangePipelineStageHandler.cs tests/CRM.Tests/Application/ChangePipelineStageHandlerTests.cs
git commit -m "fix(crm): ChangePipelineStage can never move an opportunity onto a Won/Lost stage"
```

### Task 15: `MoveOpportunityToPipeline` command (cross-pipeline move, doc §3.3/§5 item 5)

**Files:**
- Create: `src/Modules/CRM/Application/MoveOpportunityToPipelineCommand.cs`
- Create: `src/Modules/CRM/Application/MoveOpportunityToPipelineResult.cs`
- Create: `src/Modules/CRM/Application/MoveOpportunityToPipelineHandler.cs`
- Modify: `src/Modules/CRM/Domain/Opportunity.cs` (`MoveToPipeline`)
- Modify: `src/Host/Program.cs`, `src/Host/Endpoints/OpportunityEndpoints.cs`
- Test: `tests/CRM.Tests/Application/MoveOpportunityToPipelineHandlerTests.cs`

- [ ] **Step 1: Write the failing domain test**

```csharp
    [Fact]
    public void MoveToPipeline_reassigns_both_the_version_and_the_stage()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: 1, pipelineStageId: 7);

        opportunity.MoveToPipeline(pipelineDefinitionVersionId: 2, pipelineStageId: 42);

        Assert.Equal(2, opportunity.PipelineDefinitionVersionId);
        Assert.Equal(42, opportunity.PipelineStageId);
    }

    [Fact]
    public void MoveToPipeline_is_rejected_when_not_open()
    {
        var opportunity = NewDraftOpportunity();

        Assert.Throws<InvalidOperationException>(() => opportunity.MoveToPipeline(2, 42));
    }
```

Run: `dotnet test tests/CRM.Tests --filter OpportunityStateMachineTests`
Expected: FAIL — `MoveToPipeline` doesn't exist.

- [ ] **Step 2: Add the aggregate method**

Add to `src/Modules/CRM/Domain/Opportunity.cs`, after `ChangeStage`:

```csharp
    /// <summary>Cross-pipeline move (doc 2026-09-27 §3.3/§5 item 5) — unlike ChangeStage,
    /// this also reassigns PipelineDefinitionVersionId, since the target stage belongs to
    /// an entirely different PipelineDefinition. MoveOpportunityToPipelineHandler validates
    /// the target stage's Kind == Open and Published-version membership before calling
    /// this — same division of labor as ChangeStage/ChangePipelineStageHandler.</summary>
    public void MoveToPipeline(long pipelineDefinitionVersionId, long pipelineStageId)
    {
        EnsureNotArchived();
        if (Status != OpportunityStatus.Open)
            throw new InvalidOperationException($"Cannot move an opportunity in status {Status} to a different pipeline. It must be open.");

        PipelineDefinitionVersionId = pipelineDefinitionVersionId;
        PipelineStageId = pipelineStageId;
        Touch();
    }
```

- [ ] **Step 3: Run the domain test**

Run: `dotnet test tests/CRM.Tests --filter OpportunityStateMachineTests`
Expected: PASS.

- [ ] **Step 4: Create the command/result records**

```csharp
// MoveOpportunityToPipelineCommand.cs
using Contracts;

namespace CRM.Application;

public sealed record MoveOpportunityToPipelineCommand(
    TenantId TenantId, PrincipalRef Principal, long OpportunityId, long ExpectedVersion,
    long TargetPipelineDefinitionVersionId, long TargetStageId, string IdempotencyKey, Guid CorrelationId);
```

```csharp
// MoveOpportunityToPipelineResult.cs
namespace CRM.Application;

public sealed record MoveOpportunityToPipelineResult(long OpportunityId, long TargetPipelineDefinitionVersionId, long TargetStageId, bool Replayed);
```

- [ ] **Step 5: Write the failing handler tests**

```csharp
    [Fact]
    public async Task HandleAsync_moves_an_open_opportunity_to_a_different_pipeline()
    {
        // seed tenant with two separate PipelineDefinitions, each with a Published version
        // (via ProvisionPipelineHandler twice with different names); open an opportunity on the first.
        var targetOpenStage = await Context.PipelineStages.SingleAsync(s => s.TenantId == tenant && s.PipelineDefinitionVersionId == secondVersionId && s.Kind == PipelineStageKind.Open);
        var handler = new MoveOpportunityToPipelineHandler(Context, StubAuthorizer.AlwaysAllow);

        await handler.HandleAsync(new MoveOpportunityToPipelineCommand(tenant, TestData.Seller, opportunity.Id, opportunity.RowVersion,
            secondVersionId, targetOpenStage.Id, Guid.NewGuid().ToString(), Guid.NewGuid()));

        var reloaded = await Context.Opportunities.SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(secondVersionId, reloaded.PipelineDefinitionVersionId);
        Assert.Equal(targetOpenStage.Id, reloaded.PipelineStageId);
    }

    [Fact]
    public async Task HandleAsync_rejects_a_won_or_lost_kind_target_stage()
    {
        var lostStage = await Context.PipelineStages.SingleAsync(s => s.TenantId == tenant && s.PipelineDefinitionVersionId == secondVersionId && s.Kind == PipelineStageKind.Lost);
        var handler = new MoveOpportunityToPipelineHandler(Context, StubAuthorizer.AlwaysAllow);

        await Assert.ThrowsAsync<InvalidPipelineTransitionException>(() =>
            handler.HandleAsync(new MoveOpportunityToPipelineCommand(tenant, TestData.Seller, opportunity.Id, opportunity.RowVersion,
                secondVersionId, lostStage.Id, Guid.NewGuid().ToString(), Guid.NewGuid())));
    }
```

Run: `dotnet test tests/CRM.Tests --filter MoveOpportunityToPipelineHandlerTests`
Expected: FAIL — the handler doesn't exist yet.

- [ ] **Step 6: Implement the handler**

Follow `ChangePipelineStageHandler.cs`'s exact template (idempotency, concurrency, unique-violation replay), with a new action key and the cross-pipeline-specific validation:

```csharp
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Contracts;
using CRM.Domain;
using CRM.Evidence;
using CRM.Idempotency;
using CRM.Outbox;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CRM.Application;

/// <summary>Doc 2026-09-27 §3.3/§5 item 5 — moves an open Opportunity to a different
/// PipelineDefinition entirely (not just a stage change within the same one, which stays
/// ChangePipelineStageHandler's job). The target stage is always chosen explicitly by the
/// caller; there is deliberately no auto-default.</summary>
public sealed class MoveOpportunityToPipelineHandler(CrmDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "MoveOpportunityToPipeline";
    private const string ActionKeyValue = "crm.opportunity.move_pipeline";
    private const string EventType = "enterprise.crmsales.opportunity.moved_pipeline.v1";
    private const string EventSource = "/enterprise/crm-sales";
    private const int SucceededStatus = 200;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    public async Task<MoveOpportunityToPipelineResult> HandleAsync(MoveOpportunityToPipelineCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        var opportunity = await context.Opportunities.SingleOrDefaultAsync(o => o.Id == command.OpportunityId, cancellationToken)
            ?? throw new OpportunityNotFoundException(command.OpportunityId);

        var actor = new ActorContext(command.TenantId, command.Principal, command.CorrelationId);
        var resource = new ResourceDescriptor(nameof(Opportunity), opportunity.Id, opportunity.AssignedPrincipal);
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey(ActionKeyValue), resource), cancellationToken);
        if (!decision.IsAllowed)
            throw new OpportunityAuthorizationDeniedException(ActionKeyValue, decision.ReasonCode, decision.DenialStage, opportunity.Id);

        var requestHash = HashRequest(command);
        var existing = await context.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                r => r.TenantId == command.TenantId && r.PrincipalIssuer == command.Principal.Issuer
                    && r.PrincipalSubject == command.Principal.Subject && r.Operation == Operation
                    && r.IdempotencyKey == command.IdempotencyKey,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);
            await transaction.CommitAsync(cancellationToken);
            return new MoveOpportunityToPipelineResult(command.OpportunityId, command.TargetPipelineDefinitionVersionId, command.TargetStageId, Replayed: true);
        }

        if (opportunity.RowVersion != command.ExpectedVersion)
            throw new OpportunityConcurrencyConflictException(opportunity.Id, command.ExpectedVersion);

        var targetVersion = await context.PipelineDefinitionVersions.AsNoTracking()
            .SingleOrDefaultAsync(v => v.TenantId == command.TenantId && v.Id == command.TargetPipelineDefinitionVersionId, cancellationToken)
            ?? throw new InvalidPipelineTransitionException(opportunity.Id, command.TargetStageId, "target pipeline version does not exist for this tenant");
        if (targetVersion.Status != PipelineVersionStatus.Published)
            throw new InvalidPipelineTransitionException(opportunity.Id, command.TargetStageId, "target pipeline version is not published");

        var targetStage = await context.PipelineStages.AsNoTracking()
            .SingleOrDefaultAsync(s => s.TenantId == command.TenantId && s.Id == command.TargetStageId, cancellationToken)
            ?? throw new InvalidPipelineTransitionException(opportunity.Id, command.TargetStageId, "stage does not exist for this tenant");
        if (targetStage.PipelineDefinitionVersionId != command.TargetPipelineDefinitionVersionId)
            throw new InvalidPipelineTransitionException(opportunity.Id, command.TargetStageId, "stage does not belong to the target pipeline version");
        if (!targetStage.IsActive)
            throw new InvalidPipelineTransitionException(opportunity.Id, command.TargetStageId, "stage is retired and not a valid target");
        if (targetStage.Kind != PipelineStageKind.Open)
            throw new InvalidPipelineTransitionException(opportunity.Id, command.TargetStageId, "Won and Lost stages can only be reached through the Win/Lose commands");

        opportunity.MoveToPipeline(command.TargetPipelineDefinitionVersionId, command.TargetStageId);

        var payload = new { opportunity.Id, command.TargetPipelineDefinitionVersionId, command.TargetStageId };
        var payloadJson = JsonSerializer.Serialize(payload);

        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
            EventType, EventSource, $"opportunities/{opportunity.Id}", command.CorrelationId, null, payloadJson));
        context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
            command.Principal, "Opportunity.MoveToPipeline", payloadJson, command.CorrelationId));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId, command.Principal, Operation, command.IdempotencyKey, requestHash, SucceededStatus, payloadJson, IdempotencyRetention));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new OpportunityConcurrencyConflictException(opportunity.Id, command.ExpectedVersion);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            var winner = await context.IdempotencyRecords.AsNoTracking().SingleAsync(
                record => record.TenantId == command.TenantId
                    && record.PrincipalIssuer == command.Principal.Issuer
                    && record.PrincipalSubject == command.Principal.Subject
                    && record.Operation == Operation
                    && record.IdempotencyKey == command.IdempotencyKey,
                cancellationToken);
            if (winner.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);
            return new MoveOpportunityToPipelineResult(command.OpportunityId, command.TargetPipelineDefinitionVersionId, command.TargetStageId, Replayed: true);
        }

        await transaction.CommitAsync(cancellationToken);
        return new MoveOpportunityToPipelineResult(opportunity.Id, command.TargetPipelineDefinitionVersionId, command.TargetStageId, Replayed: false);
    }

    private const string UniqueViolationSqlState = "23505";
    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState };

    private static string HashRequest(MoveOpportunityToPipelineCommand command)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.Principal}|{command.OpportunityId}|{command.ExpectedVersion}|{command.TargetPipelineDefinitionVersionId}|{command.TargetStageId}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test tests/CRM.Tests --filter MoveOpportunityToPipelineHandlerTests`
Expected: PASS.

- [ ] **Step 8: Register the handler and add the endpoint**

In `src/Host/Program.cs`, next to the other `AddScoped<...Handler>()` registrations for CRM:

```csharp
builder.Services.AddScoped<MoveOpportunityToPipelineHandler>();
```

In `src/Host/Endpoints/OpportunityEndpoints.cs`, follow the exact shape of the existing `ChangePipelineStage` endpoint (same file, same pattern — `MapPut`/`MapPost`, `[FromHeader(Name = "Idempotency-Key")]`, build the command from `actor`) to add:

```csharp
            long id, MoveOpportunityToPipelineRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            MoveOpportunityToPipelineHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = /* same actor-resolution call the ChangePipelineStage endpoint above it uses */;
            var command = new MoveOpportunityToPipelineCommand(
                actor.TenantId, actor.Principal, id, request.ExpectedVersion, request.TargetPipelineDefinitionVersionId, request.TargetStageId, idempotencyKey, actor.CorrelationId);
            var result = await handler.HandleAsync(command, cancellationToken);
            return Results.Ok(result);
        });
```

Add the request record next to `ChangePipelineStageRequest`:

```csharp
public sealed record MoveOpportunityToPipelineRequest(long ExpectedVersion, long TargetPipelineDefinitionVersionId, long TargetStageId);
```

- [ ] **Step 9: Run the full CRM.Tests and Host.Tests suites**

Run: `dotnet test tests/CRM.Tests tests/Host.Tests`
Expected: PASS.

- [ ] **Step 10: Commit**

```bash
git add src/Modules/CRM/Application/MoveOpportunityToPipeline*.cs src/Modules/CRM/Domain/Opportunity.cs src/Host/Program.cs src/Host/Endpoints/OpportunityEndpoints.cs tests/CRM.Tests/
git commit -m "feat(crm): add MoveOpportunityToPipeline for cross-pipeline moves"
```

### Task 16: `OpportunityStageHistory`

**Files:**
- Create: `src/Modules/CRM/Domain/OpportunityStageHistoryEntry.cs`
- Create: `src/Modules/CRM/Persistence/Configurations/OpportunityStageHistoryEntryConfiguration.cs`
- Modify: `src/Modules/CRM/Persistence/CrmDbContext.cs` (add the `DbSet`)
- Modify: `OpenOpportunityHandler.cs`, `ChangePipelineStageHandler.cs`, `WinOpportunityHandler.cs`, `LoseOpportunityHandler.cs`, `MoveOpportunityToPipelineHandler.cs` — each opens/closes a history row
- Test: `tests/CRM.Tests/Domain/OpportunityStageHistoryEntryTests.cs`, extensions to each handler's test file

This is additive reporting infrastructure — no existing behavior depends on it, so it's safe to add last within Phase C.

- [ ] **Step 1: Write the failing domain test**

```csharp
using Contracts;
using CRM.Domain;
using Xunit;

namespace CRM.Tests.Domain;

public sealed class OpportunityStageHistoryEntryTests
{
    [Fact]
    public void Open_creates_an_entry_with_no_exit()
    {
        var tenant = TestData.NextTenant();
        var entry = OpportunityStageHistoryEntry.Open(tenant, opportunityId: 1, pipelineStageId: 7);

        Assert.Equal(7, entry.PipelineStageId);
        Assert.Null(entry.ExitedAt);
    }

    [Fact]
    public void Close_sets_exited_at_once()
    {
        var tenant = TestData.NextTenant();
        var entry = OpportunityStageHistoryEntry.Open(tenant, opportunityId: 1, pipelineStageId: 7);

        entry.Close();

        Assert.NotNull(entry.ExitedAt);
    }

    [Fact]
    public void Close_twice_throws()
    {
        var tenant = TestData.NextTenant();
        var entry = OpportunityStageHistoryEntry.Open(tenant, opportunityId: 1, pipelineStageId: 7);
        entry.Close();

        Assert.Throws<InvalidOperationException>(() => entry.Close());
    }
}
```

Run: `dotnet test tests/CRM.Tests --filter OpportunityStageHistoryEntryTests`
Expected: FAIL — the type doesn't exist.

- [ ] **Step 2: Create the domain entity**

```csharp
using Contracts;

namespace CRM.Domain;

/// <summary>One row per stage an Opportunity occupied (doc 2026-09-27 §5 item 4) — feeds
/// funnel/time-in-stage reporting. ClosedFromStageId alone (on Opportunity itself) is not
/// enough for full funnel analysis across every stage a deal passed through, only the one
/// right before closing.</summary>
public sealed class OpportunityStageHistoryEntry
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long OpportunityId { get; private set; }
    public long PipelineStageId { get; private set; }
    public DateTimeOffset EnteredAt { get; private set; }
    public DateTimeOffset? ExitedAt { get; private set; }

    private OpportunityStageHistoryEntry() { }

    public static OpportunityStageHistoryEntry Open(TenantId tenantId, long opportunityId, long pipelineStageId) => new()
    {
        TenantId = tenantId,
        OpportunityId = opportunityId,
        PipelineStageId = pipelineStageId,
        EnteredAt = DateTimeOffset.UtcNow
    };

    public void Close()
    {
        if (ExitedAt is not null) throw new InvalidOperationException("This stage-history entry is already closed.");
        ExitedAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 3: Run the domain test**

Run: `dotnet test tests/CRM.Tests --filter OpportunityStageHistoryEntryTests`
Expected: PASS.

- [ ] **Step 4: EF configuration and migration**

```csharp
using Contracts;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class OpportunityStageHistoryEntryConfiguration : IEntityTypeConfiguration<OpportunityStageHistoryEntry>
{
    public void Configure(EntityTypeBuilder<OpportunityStageHistoryEntry> builder)
    {
        builder.ToTable("opportunity_stage_history");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.HasOne<Opportunity>()
            .WithMany()
            .HasForeignKey(e => e.OpportunityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<PipelineStage>()
            .WithMany()
            .HasForeignKey(e => e.PipelineStageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TenantId, e.OpportunityId, e.EnteredAt });
    }
}
```

Add to `src/Modules/CRM/Persistence/CrmDbContext.cs`, next to the other `DbSet<...>` properties:

```csharp
    public DbSet<OpportunityStageHistoryEntry> OpportunityStageHistory => Set<OpportunityStageHistoryEntry>();
```

Run: `dotnet ef migrations add AddOpportunityStageHistory --project src/Modules/CRM --startup-project src/Host`
Run: `dotnet ef database update --project src/Modules/CRM --startup-project src/Host`

- [ ] **Step 5: Wire it into every transition handler — write the failing tests first**

For each of `OpenOpportunityHandler`, `ChangePipelineStageHandler`, `WinOpportunityHandler`, `LoseOpportunityHandler`, `MoveOpportunityToPipelineHandler`, add one test asserting a history row is written/closed, e.g.:

```csharp
    [Fact]
    public async Task HandleAsync_opens_a_stage_history_entry()
    {
        // ... existing open-opportunity seeding/handling ...
        var entry = await Context.OpportunityStageHistory.SingleAsync(h => h.OpportunityId == opportunity.Id);
        Assert.Equal(pipelineStageId, entry.PipelineStageId);
        Assert.Null(entry.ExitedAt);
    }
```

Run each handler's test filter to confirm the new assertion fails first.

- [ ] **Step 6: Implement — same pattern in every handler**

In `OpenOpportunityHandler.HandleAsync`, right after `opportunity.Open(...)`, only when a stage was actually assigned:

```csharp
        if (pipelineStageId is { } openedStageId)
            context.OpportunityStageHistory.Add(OpportunityStageHistoryEntry.Open(command.TenantId, opportunity.Id, openedStageId));
```

In `ChangePipelineStageHandler.HandleAsync`, right after `opportunity.ChangeStage(command.TargetStageId);`:

```csharp
        if (fromStageId is { } previousStageId)
        {
            var openEntry = await context.OpportunityStageHistory.SingleOrDefaultAsync(
                h => h.TenantId == command.TenantId && h.OpportunityId == opportunity.Id && h.PipelineStageId == previousStageId && h.ExitedAt == null, cancellationToken);
            openEntry?.Close();
        }
        context.OpportunityStageHistory.Add(OpportunityStageHistoryEntry.Open(command.TenantId, opportunity.Id, command.TargetStageId));
```

In `WinOpportunityHandler.HandleAsync`, right after `opportunity.Win(wonStageId, requireWonLine);`, when `wonStageId` was resolved:

```csharp
        if (wonStageId is { } resolvedWonStageId)
        {
            var openEntry = await context.OpportunityStageHistory.SingleOrDefaultAsync(
                h => h.TenantId == command.TenantId && h.OpportunityId == opportunity.Id && h.ExitedAt == null, cancellationToken);
            openEntry?.Close();
            context.OpportunityStageHistory.Add(OpportunityStageHistoryEntry.Open(command.TenantId, opportunity.Id, resolvedWonStageId));
        }
```

`LoseOpportunityHandler` and `MoveOpportunityToPipelineHandler` follow the identical shape (close the currently-open entry if one exists, open a new one for the destination stage) — apply the same two-line pattern used above at the corresponding point in each, right after the aggregate mutation call.

- [ ] **Step 7: Run each handler's test suite**

Run: `dotnet test tests/CRM.Tests --filter "OpenOpportunityHandlerTests|ChangePipelineStageHandlerTests|WinOpportunityHandlerTests|LoseOpportunityHandlerTests|MoveOpportunityToPipelineHandlerTests"`
Expected: PASS.

- [ ] **Step 8: Run the full CRM.Tests suite**

Run: `dotnet test tests/CRM.Tests`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/Modules/CRM/Domain/OpportunityStageHistoryEntry.cs src/Modules/CRM/Persistence/ tests/CRM.Tests/
git commit -m "feat(crm): record stage-entry history for funnel/time-in-stage reporting"
```

**Phase C is now complete.**

---

## Phase D — One-time backfill (doc §5 item 6 / §6.2)

Runs only after Phase B and Phase C are both merged — the doc's required ordering, and `BackfillCrmPipelinesCommand` (Task 9) already exists as the shared operator-command shell.

### Task 17: Backfill sub-step 2 — Won/Lost stages onto existing Published/Superseded versions, and reassign already-closed opportunities

**Files:**
- Modify: `src/Host/Bootstrap/BackfillCrmPipelinesCommand.cs`
- Test: `tests/Host.Tests/`

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public async Task RunAsync_adds_won_and_lost_stages_to_a_pre_existing_published_version_and_reassigns_closed_opportunities()
    {
        // seed a tenant with a Published PipelineDefinitionVersion built the OLD way
        // (directly via version.AddStage + Publish, bypassing Task 10/11's now-current
        // handlers) so it has no Won/Lost stage — simulating data from before this feature.
        // Seed one Won opportunity whose PipelineStageId still points at the version's only Open stage.
        var openStageId = /* the pre-existing Open stage's id */;

        var exitCode = await BackfillCrmPipelinesCommand.RunAsync(Services, Configuration, ["backfill-crm-pipelines"], Output, Error, CancellationToken.None);

        Assert.Equal(BackfillCrmPipelinesCommand.Success, exitCode);
        var crmContext = /* resolve CrmDbContext from a fresh scope */;
        var wonStage = await crmContext.PipelineStages.SingleAsync(s => s.PipelineDefinitionVersionId == versionId && s.Kind == PipelineStageKind.Won);
        var reloadedOpportunity = await crmContext.Opportunities.SingleAsync(o => o.Id == wonOpportunityId);
        Assert.Equal(wonStage.Id, reloadedOpportunity.PipelineStageId);
        Assert.Equal(openStageId, reloadedOpportunity.ClosedFromStageId);
    }
```

Run: `dotnet test tests/Host.Tests --filter RunAsync_adds_won_and_lost_stages_to_a_pre_existing_published_version_and_reassigns_closed_opportunities`
Expected: FAIL.

- [ ] **Step 2: Extend `BackfillCrmPipelinesCommand`**

Add a second private method and call it from `RunAsync` after the provisioning pass:

```csharp
    public static async Task<int> RunAsync(
        IServiceProvider services, IConfiguration configuration, string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("Bootstrap:Enabled", false))
        {
            await error.WriteLineAsync("Refused: set Bootstrap:Enabled=true (environment variable Bootstrap__Enabled=true) to run this command.");
            return NotPermitted;
        }

        await using var scope = services.CreateAsyncScope();
        var provisioned = await ProvisionMissingPipelinesAsync(scope.ServiceProvider, cancellationToken);
        await output.WriteLineAsync($"Provisioned a default pipeline for {provisioned} tenant(s) that had none.");
        var (stagesAdded, opportunitiesReassigned) = await BackfillWonLostStagesAsync(scope.ServiceProvider, cancellationToken);
        await output.WriteLineAsync($"Added {stagesAdded} Won/Lost system stage(s) to existing pipeline versions; reassigned {opportunitiesReassigned} already-closed opportunity/opportunities onto them.");
        return Success;
    }

    /// <summary>Doc 2026-09-27 §5 item 6.a/b — every PipelineDefinitionVersion that predates
    /// this feature has no Won/Lost stage yet. Uses PipelineDefinitionVersion.BackfillSystemStage,
    /// the one deliberate exception to "only a draft version can be edited" (Task 3). Idempotent:
    /// skips a version that already has both kinds.</summary>
    private static async Task<(int StagesAdded, int OpportunitiesReassigned)> BackfillWonLostStagesAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var crmContext = scopedServices.GetRequiredService<CRM.Persistence.CrmDbContext>();
        var operatorPrincipal = new Contracts.PrincipalRef(
            scopedServices.GetRequiredService<Access.Application.Authentication.SessionOptions>().PlatformIssuer, "operator:" + BackfillCrmPipelinesCommand.Name);

        var candidateVersions = await crmContext.PipelineDefinitionVersions
            .Where(v => v.Status == CRM.Domain.PipelineVersionStatus.Published || v.Status == CRM.Domain.PipelineVersionStatus.Superseded)
            .ToListAsync(cancellationToken);

        var stagesAdded = 0;
        foreach (var version in candidateVersions)
        {
            await crmContext.SetTenantContextAsync(version.TenantId, cancellationToken);
            await crmContext.Entry(version).Collection(v => v.Stages).LoadAsync(cancellationToken);
            var maxSortOrder = version.Stages.Count == 0 ? 0 : version.Stages.Max(s => s.SortOrder);

            if (!version.Stages.Any(s => s.Kind == CRM.Domain.PipelineStageKind.Won))
            {
                var won = version.BackfillSystemStage(CRM.Domain.PipelineStageKind.Won, "Won", maxSortOrder + 10);
                crmContext.PipelineStages.Add(won);
                stagesAdded++;
            }
            if (!version.Stages.Any(s => s.Kind == CRM.Domain.PipelineStageKind.Lost))
            {
                var lost = version.BackfillSystemStage(CRM.Domain.PipelineStageKind.Lost, "Lost", maxSortOrder + 20);
                crmContext.PipelineStages.Add(lost);
                stagesAdded++;
            }
            await crmContext.SaveChangesAsync(cancellationToken);
        }

        var opportunitiesReassigned = 0;
        foreach (var version in candidateVersions)
        {
            await crmContext.SetTenantContextAsync(version.TenantId, cancellationToken);
            var wonStageId = await crmContext.PipelineStages.AsNoTracking()
                .Where(s => s.PipelineDefinitionVersionId == version.Id && s.Kind == CRM.Domain.PipelineStageKind.Won)
                .Select(s => s.Id).SingleAsync(cancellationToken);
            var lostStageId = await crmContext.PipelineStages.AsNoTracking()
                .Where(s => s.PipelineDefinitionVersionId == version.Id && s.Kind == CRM.Domain.PipelineStageKind.Lost)
                .Select(s => s.Id).SingleAsync(cancellationToken);

            var closedOpportunities = await crmContext.Opportunities
                .Where(o => o.PipelineDefinitionVersionId == version.Id
                    && (o.Status == CRM.Domain.OpportunityStatus.Won || o.Status == CRM.Domain.OpportunityStatus.Lost)
                    && o.PipelineStageId != wonStageId && o.PipelineStageId != lostStageId)
                .ToListAsync(cancellationToken);

            foreach (var opportunity in closedOpportunities)
            {
                var targetStageId = opportunity.Status == CRM.Domain.OpportunityStatus.Won ? wonStageId : lostStageId;
                var closedFromStageId = opportunity.PipelineStageId;
                opportunity.BackfillClosedStage(closedFromStageId, targetStageId);
                crmContext.EvidenceRecords.Add(CRM.Evidence.EvidenceRecord.Create(
                    version.TenantId, nameof(CRM.Domain.Opportunity), opportunity.Id, opportunity.RowVersion, operatorPrincipal,
                    "Opportunity.BackfillClosedStage", System.Text.Json.JsonSerializer.Serialize(new { closedFromStageId, targetStageId }), Guid.NewGuid()));
                opportunitiesReassigned++;
            }
            await crmContext.SaveChangesAsync(cancellationToken);
        }

        return (stagesAdded, opportunitiesReassigned);
    }
```

This introduces one more small, narrow aggregate method — add it now:

```csharp
    // In src/Modules/CRM/Domain/Opportunity.cs, next to MoveToPipeline:

    /// <summary>One-time backfill exception (doc 2026-09-27 §5 item 6.b) — reassigns an
    /// ALREADY-closed opportunity's stage fields onto its version's new Won/Lost system
    /// stage. Never call this from normal application code; Win()/Lose() do this at the
    /// moment of closing for every opportunity closed from here on.</summary>
    public void BackfillClosedStage(long? closedFromStageId, long targetStageId)
    {
        if (Status is not (OpportunityStatus.Won or OpportunityStatus.Lost))
            throw new InvalidOperationException($"Cannot backfill the closed stage of an opportunity in status {Status}.");

        ClosedFromStageId = closedFromStageId;
        PipelineStageId = targetStageId;
        Touch();
    }
```

Add its own small unit test in `OpportunityStateMachineTests.cs`:

```csharp
    [Fact]
    public void BackfillClosedStage_is_rejected_when_not_closed()
    {
        var opportunity = NewDraftOpportunity();

        Assert.Throws<InvalidOperationException>(() => opportunity.BackfillClosedStage(null, 99));
    }
```

- [ ] **Step 3: Run tests to verify they pass**

Run: `dotnet test tests/Host.Tests --filter RunAsync_adds_won_and_lost_stages_to_a_pre_existing_published_version_and_reassigns_closed_opportunities`
Run: `dotnet test tests/CRM.Tests --filter BackfillClosedStage_is_rejected_when_not_closed`
Expected: PASS.

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test tests/CRM.Tests tests/Host.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Host/Bootstrap/BackfillCrmPipelinesCommand.cs src/Modules/CRM/Domain/Opportunity.cs tests/
git commit -m "feat(crm): backfill Won/Lost stages onto existing pipeline versions and reassign already-closed opportunities"
```

### Task 18: Backfill sub-step 3 — stage-less `Open` opportunities (doc §5 item 6.c / §6.2)

**Files:**
- Modify: `src/Host/Bootstrap/BackfillCrmPipelinesCommand.cs`
- Test: `tests/Host.Tests/`

**Run this task only after Task 9 (Phase B's pipeline-less-tenant provisioning pass) and Task 17 have both run against the target database once** — doc §6.2's required order. Under that order this sub-step is expected to touch close to zero real rows; it still needs to exist for correctness.

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public async Task RunAsync_assigns_a_stage_to_an_open_stageless_opportunity_preferring_its_own_version()
    {
        // seed an Open opportunity with PipelineDefinitionVersionId set but PipelineStageId null
        // (simulating a pre-fix row) against a version that has an active IsEntry stage.

        var exitCode = await BackfillCrmPipelinesCommand.RunAsync(Services, Configuration, ["backfill-crm-pipelines"], Output, Error, CancellationToken.None);

        Assert.Equal(BackfillCrmPipelinesCommand.Success, exitCode);
        var reloaded = /* reload the opportunity */;
        Assert.Equal(expectedEntryStageId, reloaded.PipelineStageId);
    }

    [Fact]
    public async Task RunAsync_falls_back_to_the_tenants_default_pipeline_when_the_opportunitys_own_version_is_null()
    {
        // seed an Open opportunity with BOTH PipelineDefinitionVersionId and PipelineStageId null,
        // and a CrmSettings row pointing at a DefaultPipelineDefinitionId with a Published version.

        await BackfillCrmPipelinesCommand.RunAsync(Services, Configuration, ["backfill-crm-pipelines"], Output, Error, CancellationToken.None);

        var reloaded = /* reload */;
        Assert.Equal(tenantDefaultEntryStageId, reloaded.PipelineStageId);
        Assert.Equal(tenantDefaultVersionId, reloaded.PipelineDefinitionVersionId);
    }
```

Run: `dotnet test tests/Host.Tests --filter "RunAsync_assigns_a_stage_to_an_open_stageless_opportunity|RunAsync_falls_back_to_the_tenants_default_pipeline"`
Expected: FAIL.

- [ ] **Step 2: Add the sub-step**

Extend `RunAsync` in `src/Host/Bootstrap/BackfillCrmPipelinesCommand.cs`:

```csharp
        var (stagesAdded, opportunitiesReassigned) = await BackfillWonLostStagesAsync(scope.ServiceProvider, cancellationToken);
        await output.WriteLineAsync($"Added {stagesAdded} Won/Lost system stage(s) to existing pipeline versions; reassigned {opportunitiesReassigned} already-closed opportunity/opportunities onto them.");
        var (stagelessFixed, stagelessUnresolved) = await BackfillStagelessOpenOpportunitiesAsync(scope.ServiceProvider, cancellationToken);
        await output.WriteLineAsync($"Assigned a stage to {stagelessFixed} stage-less open opportunity/opportunities; {stagelessUnresolved} could not be resolved automatically and need manual review.");
        return Success;
```

Add the method:

```csharp
    /// <summary>Doc 2026-09-27 §5 item 6.c/§6.2 — deterministic fallback, never a manual
    /// per-row review at migration time: prefer the opportunity's own pipeline version's
    /// entry stage; only fall back to the tenant's CrmSettings default pipeline when the
    /// opportunity's own version is also null. Run only after
    /// BackfillWonLostStagesAsync and Task 9's pipeline provisioning pass — under that
    /// order this is expected to resolve every row; anything it can't resolve is counted,
    /// not guessed at.</summary>
    private static async Task<(int Fixed, int Unresolved)> BackfillStagelessOpenOpportunitiesAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var crmContext = scopedServices.GetRequiredService<CRM.Persistence.CrmDbContext>();

        var tenantIds = await crmContext.Opportunities
            .Where(o => o.Status == CRM.Domain.OpportunityStatus.Open && o.PipelineStageId == null)
            .Select(o => o.TenantId).Distinct().ToListAsync(cancellationToken);

        var fixedCount = 0;
        var unresolvedCount = 0;
        foreach (var tenantId in tenantIds)
        {
            await crmContext.SetTenantContextAsync(tenantId, cancellationToken);
            var stageless = await crmContext.Opportunities
                .Where(o => o.TenantId == tenantId && o.Status == CRM.Domain.OpportunityStatus.Open && o.PipelineStageId == null)
                .ToListAsync(cancellationToken);

            var defaultPipelineId = await crmContext.CrmSettings.AsNoTracking()
                .Where(s => s.TenantId == tenantId).Select(s => s.DefaultPipelineDefinitionId).SingleOrDefaultAsync(cancellationToken);

            foreach (var opportunity in stageless)
            {
                var resolveVersionId = opportunity.PipelineDefinitionVersionId;
                if (resolveVersionId is null && defaultPipelineId is { } fallbackPipelineId)
                {
                    resolveVersionId = await crmContext.PipelineDefinitionVersions.AsNoTracking()
                        .Where(v => v.TenantId == tenantId && v.PipelineDefinitionId == fallbackPipelineId && v.Status == CRM.Domain.PipelineVersionStatus.Published)
                        .OrderByDescending(v => v.VersionNumber).Select(v => (long?)v.Id).FirstOrDefaultAsync(cancellationToken);
                }
                if (resolveVersionId is null) { unresolvedCount++; continue; }

                var entryStageId = await crmContext.PipelineStages.AsNoTracking()
                    .Where(s => s.TenantId == tenantId && s.PipelineDefinitionVersionId == resolveVersionId && s.IsEntry && s.IsActive)
                    .Select(s => (long?)s.Id).SingleOrDefaultAsync(cancellationToken);
                if (entryStageId is null) { unresolvedCount++; continue; }

                opportunity.BackfillStagelessOpen(resolveVersionId.Value, entryStageId.Value);
                fixedCount++;
            }
            await crmContext.SaveChangesAsync(cancellationToken);
        }

        return (fixedCount, unresolvedCount);
    }
```

Add the last narrow aggregate method:

```csharp
    // In src/Modules/CRM/Domain/Opportunity.cs, next to BackfillClosedStage:

    /// <summary>One-time backfill exception (doc 2026-09-27 §5 item 6.c) — assigns a
    /// pipeline/stage to an already-Open opportunity that predates the "stage mandatory
    /// once Open" invariant. Never call this from normal application code.</summary>
    public void BackfillStagelessOpen(long pipelineDefinitionVersionId, long pipelineStageId)
    {
        if (Status != OpportunityStatus.Open)
            throw new InvalidOperationException($"Cannot backfill a pipeline stage onto an opportunity in status {Status}.");
        if (PipelineStageId is not null)
            throw new InvalidOperationException("This opportunity already has a pipeline stage.");

        PipelineDefinitionVersionId = pipelineDefinitionVersionId;
        PipelineStageId = pipelineStageId;
        Touch();
    }
```

With its own test:

```csharp
    [Fact]
    public void BackfillStagelessOpen_is_rejected_when_a_stage_is_already_set()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), 1, 7);

        Assert.Throws<InvalidOperationException>(() => opportunity.BackfillStagelessOpen(2, 99));
    }
```

- [ ] **Step 3: Run tests to verify they pass**

Run: `dotnet test tests/Host.Tests --filter "RunAsync_assigns_a_stage_to_an_open_stageless_opportunity|RunAsync_falls_back_to_the_tenants_default_pipeline"`
Run: `dotnet test tests/CRM.Tests --filter BackfillStagelessOpen_is_rejected_when_a_stage_is_already_set`
Expected: PASS.

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test tests/CRM.Tests tests/Host.Tests`
Expected: PASS.

- [ ] **Step 5: Run it for real against the local dev database and read the output before trusting it**

Run: `dotnet run --project src/Host -- backfill-crm-pipelines`
Expected: the "unresolved" count is 0 (confirms doc §6.2's prediction). If it isn't 0, stop — investigate those specific rows manually before touching Task 19; do not proceed to the `CHECK` constraint with unresolved rows outstanding.

- [ ] **Step 6: Commit**

```bash
git add src/Host/Bootstrap/BackfillCrmPipelinesCommand.cs src/Modules/CRM/Domain/Opportunity.cs tests/
git commit -m "feat(crm): backfill stage-less open opportunities, preferring their own pipeline version"
```

**Phase D is now complete.** Do not proceed to Phase E until Task 18's Step 5 has been run against every environment that matters and reports zero unresolved rows.

---

## Phase E — The `CHECK` constraint and final hardening (doc §4.1, §4.2)

### Task 19: `ck_opportunities_stage_required_once_open`

**Files:**
- Modify: `src/Modules/CRM/Persistence/Configurations/OpportunityConfiguration.cs:70-87`
- Test: manual verification (a `CHECK` constraint isn't unit-testable in isolation the way a handler is — verify via a raw SQL attempt, per Step 3 below)

- [ ] **Step 1: Add the constraint**

In `src/Modules/CRM/Persistence/Configurations/OpportunityConfiguration.cs`, inside the existing `builder.ToTable(t => { ... })` block, add alongside the other `t.HasCheckConstraint(...)` calls:

```csharp
            t.HasCheckConstraint(
                "ck_opportunities_stage_required_once_open",
                "status <> 'open' OR pipeline_stage_id IS NOT NULL");
```

- [ ] **Step 2: Generate and apply the migration**

Run: `dotnet ef migrations add AddStageRequiredOnceOpenCheck --project src/Modules/CRM --startup-project src/Host`
Run: `dotnet ef database update --project src/Modules/CRM --startup-project src/Host`

**If this migration fails to apply** (Postgres refuses a `CHECK` addition when existing rows violate it), Task 18's backfill did not actually finish cleanly — stop, re-run `backfill-crm-pipelines`, and confirm zero unresolved rows before retrying this migration. Do not weaken the constraint to work around leftover bad data.

- [ ] **Step 3: Manually verify the constraint is live**

In `psql`:

```sql
-- This must fail with a check-constraint violation:
UPDATE opportunities SET pipeline_stage_id = NULL WHERE status = 'open' LIMIT 1;
```

Expected: `ERROR: new row for relation "opportunities" violates check constraint "ck_opportunities_stage_required_once_open"`.

- [ ] **Step 4: Run the full CRM.Tests and Host.Tests suites one final time**

Run: `dotnet test tests/CRM.Tests tests/Host.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Modules/CRM/Persistence/Configurations/OpportunityConfiguration.cs src/Modules/CRM/Persistence/Migrations/
git commit -m "feat(crm): enforce stage-required-once-open with a real CHECK constraint"
```

**This is the last task in the plan.** At this point every decision in `docs/architecture-analysis/2026-09-27-crm-opportunity-pipeline-won-lost-stage-integration.md` — both the first-round §3 decisions and the second-round §6 amendments — is implemented, tested, and enforced at the database level.

---

## Explicitly out of scope for this plan

Carried over unchanged from the design doc's own §7 (renumbered from its §6 in an earlier draft) and restated here so no implementer accidentally builds them as a "natural extension":

- Any general per-field/per-stage tenant-customization locking mechanism. The Won/Lost stages' "label editable, nothing else" rule in this plan is a narrow, hardcoded special case for exactly two stages — not a generalized capability.
- Denormalizing `PipelineStage.Kind` onto `opportunities` for query performance.
- More than one Won-kind or Lost-kind stage per pipeline version.
- A stable, version-independent stage key for cross-version funnel reporting (v1's "Teklif" ≈ v2's "Teklif"). `PipelineStage.Name` stays unique only within a version.
- A UI/command specifically for renaming the Won/Lost system stages' labels. The domain model in this plan supports it trivially (add a `Rename` method to `PipelineStage` and a thin command around it, following the exact shape of every other rename in this codebase — e.g. `PipelineDefinition.Rename`) but no specific request for that command exists yet; build it when someone actually asks to change a label, not preemptively here.
