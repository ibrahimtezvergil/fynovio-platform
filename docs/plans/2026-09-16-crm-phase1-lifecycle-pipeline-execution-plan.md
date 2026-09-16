# CRM Phase 1 — Lifecycle/Pipeline Foundation — Execution Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use subagent-driven-development
> (recommended) or executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking. **Standard (see `CLAUDE.md` "Plan checkbox
> tracking"):** mark a step `[x]` only once its commit exists, and add a
> `→ Commit: \`<hash>\` "<message>"` line under it — never mark ahead of actual state.
>
> **Status: Task 0 confirmed (2026-09-16, commit `7214867`). Task 1 done (2026-09-16, on
> branch `crm-phase1-lifecycle-pipeline`, worktree
> `.worktrees/crm-phase1-lifecycle-pipeline`, not yet merged to `main`). Tasks 2–4 NOT
> YET RUN.** Read
> `docs/plans/2026-09-16-crm-target-model-phase0-delta-plan.md` §5 (all five items now
> RESOLVED), §11 (decision record) and §12 (dependency graph) before starting — this
> plan implements Phase 1 of that roadmap. Do not re-litigate any decision recorded
> there. Task 0's two design decisions and Task 3 Step 1's `party_type` default are all
> confirmed as written.

**Goal:** Rename `OpportunityStatus` to the target model's `Draft/Open/Won/Lost`; add
additive pipeline-definition/version/stage tables; cut `Opportunity` over from a raw
`PartyId`+FK to a `PartyRef` (per `docs/plans/2026-09-16-masterdata-party-foundation.md`
§9's migration sequence, steps 2–4 and 6–7 — step 1 and step 5 are already done, see
Task 3's intro). `OpportunityLine`'s monetary shape is **not** touched — §5.B is explicit
that it stays a legacy/pilot artifact until Phase 5.

**Explicitly OUT of scope for Phase 1** (per the delta plan's Phase 1/Phase 2 split and
§5.A/§5.D's resolutions):
- No new commands, no HTTP endpoints, no authorization gate — Phase 1 introduces **no
  public state-changing application surface**. That's Phase 2, which additionally
  requires Phase 1.5 (Access baseline) to be done first (§5.D).
- No structured `lost_reason` taxonomy / policy-controlled reason codes — Phase 1 keeps
  `LostReason` as free text (a mechanical rename of today's `CancelReason`); the
  decision matrix's "structured taxonomy" item is deferred, naturally paired with the
  authorization/policy work in Phase 2+, not bundled into a rename-only phase.
- No approval workflow of any kind (§5.A: permanently deferred, optional-capability-only).
- No `OpportunityLine` restructuring (§5.B: Phase 5's job).
- No pipeline-stage *enforcement* (e.g., requiring a stage to move `Draft → Open`) —
  Task 2 adds the tables and an **optional** nullable assignment on `Opportunity`; real
  assignment/transition commands (`ChangePipelineStage`) are Phase 2.

---

## Task 0: Pre-flight design decisions — read and confirm before Task 3

**No code in this task.** Two design choices were made while writing this plan that
were not explicitly settled by any prior owner decision. Flagging them here rather than
silently deciding, per the PDF's own Section 24 protocol ("if not defined, stop and
ask"). Both are **narrow, low-risk, and reversible** given no production tenants exist
([[project-scope-local-only]]) — but confirm before Task 3 runs, since data-shape
choices are more annoying to unwind than a rename.

- [x] **Decision 0.1 — `CRM.Domain.Party`'s `CreationSource` and `CustomFields` have no
  `MasterData.Domain.Party` equivalent.** `MasterData.Party`'s frozen shape (design doc
  §3) is `Id/TenantId/PartyType/Name/Surname/Phone/Email/MergedIntoPartyId` only — no
  `CreationSource` (Manual/AiVoiceCapture), no `CustomFields` (jsonb).
  **Recommendation:** drop both during the Task 3 backfill. `CreationSource` was a
  provenance tag with no consumer today (`CRM_CURRENT_STATE_ANALYSIS.md` never shows it
  read anywhere but persisted); `CustomFields` on Party duplicates what
  `TenantFieldDefinition`-driven custom fields already do at the `Opportunity` level —
  there's no evidence Party-level custom fields were ever used. If either turns out to
  be load-bearing, add it to `MasterData.Party` as a real, intentional extension later
  — don't route around the loss by keeping two Party tables.
- [x] **Decision 0.2 — pipeline-stage assignment is optional in Phase 1, not enforced.**
  `Opportunity.PipelineDefinitionVersionId`/`PipelineStageId` (Task 2) are nullable and
  Phase 1 adds no CHECK requiring them once `Open`. **Recommendation:** keep it that way
  through Phase 1 — enforcing "a stage is required once Open" belongs with the command
  that actually assigns one (`ChangePipelineStage`, Phase 2), the same pattern already
  used for `ExpiryDate` (required once `Open`, stamped by the domain method that
  transitions into it, not bolted on separately).

If either recommendation is rejected, stop before Task 3 (Decision 0.1) or before
finishing Task 2's CHECK constraints (Decision 0.2) and get the actual answer instead.

→ **Confirmed by Ibrahim (2026-09-16):** both recommendations approved as written, no
changes. Also confirmed at the same time: Task 3 Step 1's `party_type` default of
`'organization'` for the `crm.parties` → `masterdata.parties` backfill, approved as
written.

---

## Task 1: `OpportunityStatus` lifecycle rename — `Waiting/Offered/Completed/Canceled` → `Draft/Open/Won/Lost`

**Files:**
- Modify: `src/Modules/CRM/Domain/Opportunity.cs`
- Modify: `src/Modules/CRM/Persistence/Configurations/OpportunityConfiguration.cs`
- Modify: `tests/CRM.Tests/Domain/OpportunityStateMachineTests.cs`
- Modify: `tests/CRM.Tests/Integration/CompleteOpportunityHandlerTests.cs`
- Modify: `tests/CRM.Tests/Integration/OpportunityPersistenceTests.cs`
- Modify: `tests/CRM.Tests/Integration/OpportunityConcurrencyTests.cs` (method-name
  references only — no status literals, verify with the grep in Step 1)
- Create: `src/Modules/CRM/Persistence/Migrations/<timestamp>_RenameOpportunityLifecycle.cs` (generated)

Per the delta plan §7: this is expected, one-shot churn — do it as **one commit**, never
a broken-build intermediate state. Exact mapping, decided narrowly (see rationale after
the table):

| Old | New | Old method | New method | Old field | New field |
|---|---|---|---|---|---|
| `Waiting` | `Draft` | `Create(...)` (unchanged) | — | — | — |
| `Offered` | `Open` | `Offer(expiryDate)` | `Open(expiryDate)` | `OfferDate` | `OpenedDate` |
| `Completed` | `Won` | `Complete()` | `Win()` | `SaleDate` | `WonDate` |
| `Canceled` | `Lost` | `Cancel(reason)` | `Lose(reason)` | `CancelReason`, `CancelDate` | `LostReason`, `LostDate` |

**Rationale for what does NOT change:** `TotalAmount`, `Currency`, `EstimatedAmount`,
`ExpiryDate`, and the expiry-required-once-`Open` gate's *mechanism* all stay exactly as
today, just under the renamed status. §5.B is explicit that money-shaped fields are not
to be touched outside Phase 5; `ExpiryDate`'s long-term home (CRM lifecycle vs. a future
Sales quote concept) is an open question the decision matrix flagged but did not ask
Phase 1 to resolve — preserving it under the new name is the narrow, reversible choice.
`LostReason` stays free text (see this plan's header — structured taxonomy is
out of scope).

- [x] **Step 1: Confirm the exact blast radius**

```bash
grep -rn "OpportunityStatus\.\(Waiting\|Offered\|Completed\|Canceled\)\|\"waiting\"\|\"offered\"\|\"completed\"\|\"canceled\"" \
  src/Modules/CRM tests/CRM.Tests --include="*.cs"
```

Confirm it still matches only the five files listed above (it did when this plan was
written — re-verify, since the codebase may have moved since). If it matches more,
update this task's file list before proceeding.

→ **Deviation found:** this grep pattern only matches enum/string literals, not
method-call sites (`.Offer(`, `.Complete(`). It missed three files that call the old
methods directly: `tests/CRM.Tests/Domain/OpportunityRowVersionTests.cs`,
`tests/CRM.Tests/Domain/OpportunityMoneyTests.cs`, and
`src/Modules/CRM/Application/CompleteOpportunityHandler.cs`. Caught by the controller's
independent `dotnet test` run (outside the sandbox restriction that blocked the
implementer) and fixed in follow-up commits — see Step 9.

- [x] **Step 2: Rename in `Opportunity.cs`**

Apply the table above. `OpportunityStatus` enum values, `Open(DateTimeOffset
expiryDate)` (renamed from `Offer`, body unchanged except the status it sets),
`Win()` (renamed from `Complete`, body unchanged), `Lose(string lostReason)` (renamed
from `Cancel`, parameter and field renamed, body otherwise unchanged). Field renames:
`OfferDate`→`OpenedDate`, `SaleDate`→`WonDate`, `CancelReason`→`LostReason`,
`CancelDate`→`LostDate`. `CancelLine`/line-level cancellation is **not** renamed — a
line being canceled is unrelated to the opportunity's own Won/Lost vocabulary. Update
every doc comment that names the old status values (e.g. the `Complete()` XML doc's
"cross-row invariant" note, the class-level comment).

- [x] **Step 3: Rename in `OpportunityConfiguration.cs`**

`ToDb`/`FromDb` switch expressions get the new string values (`"draft"`, `"open"`,
`"won"`, `"lost"`). CHECK constraint renames (name **and** expression):
- `ck_opportunities_status` → same name, values become `'draft','open','won','lost'`.
- `ck_opportunities_expiry_required_once_offered` →
  `ck_opportunities_expiry_required_once_open`, expression
  `status NOT IN ('open','won') OR expiry_date IS NOT NULL`.
- `ck_opportunities_sale_date_required_once_completed` →
  `ck_opportunities_won_date_required_once_won`, expression
  `status <> 'won' OR won_date IS NOT NULL`.
- `ck_opportunities_cancel_fields_required_once_canceled` →
  `ck_opportunities_lost_fields_required_once_lost`, expression
  `status <> 'lost' OR (lost_date IS NOT NULL AND lost_reason IS NOT NULL)`.

- [x] **Step 4: Update the five test files (mechanical rename, same behavior)**

Read each file, replace old enum/method/field names with new ones per the table. No
new test cases needed — this step proves the *existing* invariants still hold under
the new names. Don't add pipeline-related assertions here (that's Task 2's job).

→ Actually touched 8 test files, not 5 — see Step 1's deviation note. Also fixed 2
stale doc comments a spec-compliance reviewer flagged (`OpportunityLine.cs`'s
`Opportunity.Complete()` reference, `CompleteOpportunityHandlerTests.cs`'s "Still
waiting" comment) in a follow-up commit — see Step 9.

- [x] **Step 5: Compile and run — expect the old suite to still pass, renamed**

```bash
dotnet build src/Modules/CRM/CRM.csproj
```

- [x] **Step 6: Generate the migration**

```bash
dotnet ef migrations add RenameOpportunityLifecycle \
  --project src/Modules/CRM/CRM.csproj \
  --startup-project src/Modules/CRM/CRM.csproj \
  --output-dir Persistence/Migrations
```

- [x] **Step 7: Verify the generated migration**

Since no production tenants exist, a straightforward `ALTER TABLE ... DROP
CONSTRAINT` + `ADD CONSTRAINT` (new names/expressions) plus a data `UPDATE` remapping
the four string values is fine — EF will likely generate `DropCheckConstraint` +
`AddCheckConstraint` calls plus (if it detects the column rename heuristically or not)
possibly a drop/re-add of the status values as data. **If EF's diff doesn't include a
data-remapping `UPDATE` for existing rows** (it may not, since this is a value rename
within a string column, not a schema change EF tracks), add one by hand in the
generated migration's `Up`/`Down` — this is a data-backfill `Sql()` call layered on an
EF-generated migration, the same pattern used in `masterdata-party-foundation.md` §9
step 2, not a second RLS-style full hand-written exception:

```csharp
migrationBuilder.Sql("""
    UPDATE crm.opportunities SET status = CASE status
        WHEN 'waiting' THEN 'draft'
        WHEN 'offered' THEN 'open'
        WHEN 'completed' THEN 'won'
        WHEN 'canceled' THEN 'lost'
        ELSE status
    END;
    """);
```
Place this **before** the new CHECK constraint is added in `Up` (so old values don't
violate it mid-migration), and add the reverse mapping to `Down`.

- [x] **Step 8: Apply and test**

```bash
dotnet ef database update \
  --project src/Modules/CRM/CRM.csproj \
  --startup-project src/Modules/CRM/CRM.csproj
dotnet test tests/CRM.Tests/CRM.Tests.csproj
```

Expected: all 41 tests still pass, renamed.

→ No standalone dev Postgres to run `database update` against; verified via
Testcontainers-driven `dotnet test`, which migrates fresh each run. 41/41 pass,
confirmed independently by the controller outside the sandbox restriction that blocked
the implementer's own runs (`System.Net.Sockets.SocketException (13): Permission
denied` on MSBuild's named-pipe node — environment issue, not a code issue).

- [x] **Step 9: Commit**

```bash
git add src/Modules/CRM/Domain/Opportunity.cs src/Modules/CRM/Persistence tests/CRM.Tests
git commit -m "Rename OpportunityStatus to Draft/Open/Won/Lost per the target model

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

→ Commit: `e523b12` "Rename OpportunityStatus to Draft/Open/Won/Lost per the target model"
(follow-up fix: `2718b25` "Fix remaining Offer/Complete/Cancel call sites missed by
Task 1's grep"; follow-up fix: `12d96ba` "Fix last leftover NewWaitingOpportunity() call
site"; follow-up fix: `5ffe212` "Fix two stale Complete()/waiting doc-comment
references"). Spec-compliance and code-quality subagent reviews both passed after the
doc-comment fix. Branch: `crm-phase1-lifecycle-pipeline` (worktree
`.worktrees/crm-phase1-lifecycle-pipeline`), not yet merged to `main`.

---

## Task 2: Pipeline definition/version/stage tables (additive)

**Files:**
- Create: `src/Modules/CRM/Domain/PipelineDefinition.cs`
- Create: `src/Modules/CRM/Domain/PipelineDefinitionVersion.cs`
- Create: `src/Modules/CRM/Domain/PipelineStage.cs`
- Create: `src/Modules/CRM/Persistence/Configurations/PipelineDefinitionConfiguration.cs`
- Create: `src/Modules/CRM/Persistence/Configurations/PipelineDefinitionVersionConfiguration.cs`
- Create: `src/Modules/CRM/Persistence/Configurations/PipelineStageConfiguration.cs`
- Modify: `src/Modules/CRM/Domain/Opportunity.cs` (two new nullable fields, no new
  domain method yet — see Task 0 Decision 0.2)
- Modify: `src/Modules/CRM/Persistence/Configurations/OpportunityConfiguration.cs`
- Create: `tests/CRM.Tests/Domain/PipelineDefinitionVersionTests.cs`
- Create: `src/Modules/CRM/Persistence/Migrations/<timestamp>_AddPipelineTables.cs` (generated)

Versioned by design (PDF §5's explicit rule): changing a definition's stages must not
remap opportunities already pointing at an older version. `Opportunity` therefore
references a **version**, not the definition directly.

- [ ] **Step 1: `PipelineDefinition`**

`src/Modules/CRM/Domain/PipelineDefinition.cs`:

```csharp
using Contracts;

namespace CRM.Domain;

/// <summary>Tenant/sector-configurable pipeline, versioned (PDF §5) so an edit to an
/// in-use definition never silently remaps opportunities already on an older version —
/// see PipelineDefinitionVersion. Purely additive in Phase 1: no command assigns one to
/// an Opportunity yet (Phase 2's ChangePipelineStage does).</summary>
public sealed class PipelineDefinition
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<PipelineDefinitionVersion> _versions = [];
    public IReadOnlyCollection<PipelineDefinitionVersion> Versions => _versions;

    private PipelineDefinition() { }

    public static PipelineDefinition Create(TenantId tenantId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        var now = DateTimeOffset.UtcNow;
        return new PipelineDefinition
        {
            TenantId = tenantId,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public PipelineDefinitionVersion AddVersion(int versionNumber)
    {
        if (_versions.Any(v => v.VersionNumber == versionNumber))
            throw new InvalidOperationException($"Version {versionNumber} already exists on this definition.");

        var version = PipelineDefinitionVersion.Create(TenantId, versionNumber);
        _versions.Add(version);
        UpdatedAt = DateTimeOffset.UtcNow;
        return version;
    }
}
```

- [ ] **Step 2: `PipelineDefinitionVersion`**

`src/Modules/CRM/Domain/PipelineDefinitionVersion.cs`:

```csharp
using Contracts;

namespace CRM.Domain;

public sealed class PipelineDefinitionVersion
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long PipelineDefinitionId { get; private set; }
    public int VersionNumber { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<PipelineStage> _stages = [];
    public IReadOnlyCollection<PipelineStage> Stages => _stages;

    private PipelineDefinitionVersion() { }

    internal static PipelineDefinitionVersion Create(TenantId tenantId, int versionNumber)
    {
        if (versionNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(versionNumber), "Version number must be positive.");

        return new PipelineDefinitionVersion
        {
            TenantId = tenantId,
            VersionNumber = versionNumber,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public PipelineStage AddStage(string name, int sortOrder)
    {
        if (_stages.Any(s => s.SortOrder == sortOrder))
            throw new InvalidOperationException($"Sort order {sortOrder} is already used on this version.");

        var stage = PipelineStage.Create(TenantId, name, sortOrder);
        _stages.Add(stage);
        return stage;
    }
}
```

- [ ] **Step 3: `PipelineStage`**

`src/Modules/CRM/Domain/PipelineStage.cs`:

```csharp
using Contracts;

namespace CRM.Domain;

public sealed class PipelineStage
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long PipelineDefinitionVersionId { get; private set; }
    public string Name { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private PipelineStage() { }

    internal static PipelineStage Create(TenantId tenantId, string name, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (sortOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order cannot be negative.");

        return new PipelineStage
        {
            TenantId = tenantId,
            Name = name,
            SortOrder = sortOrder,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
```

- [ ] **Step 4: Domain tests (versioning invariant)**

`tests/CRM.Tests/Domain/PipelineDefinitionVersionTests.cs` — cover: adding a duplicate
version number rejected; adding a duplicate sort-order stage within one version
rejected; two versions of the same definition can each freely reuse the same stage
names/sort-orders (proving versions are genuinely independent, the PDF §5 guarantee).
Write 3–4 focused tests in the style of `PartyRelationshipTests.cs`
(`docs/plans/2026-09-16-masterdata-phase0.5-execution-plan.md` Task 2 Step 3) — small,
one behavior per test, tenant via `TestData.NextTenant()`.

- [ ] **Step 5: Configurations**

`PipelineDefinitionConfiguration`: `ToTable("pipeline_definitions")`, PK `Id`,
alternate key `(TenantId, Id)`, `HasIndex(TenantId, Name)` unique (a tenant's pipeline
names don't collide).

`PipelineDefinitionVersionConfiguration`: `ToTable("pipeline_definition_versions")`, PK
`Id`, tenant-safe composite FK to `PipelineDefinition` via
`(TenantId, PipelineDefinitionId)` → `(TenantId, Id)`, `DeleteBehavior.Restrict`,
`HasIndex(TenantId, PipelineDefinitionId, VersionNumber)` unique.

`PipelineStageConfiguration`: `ToTable("pipeline_stages")`, PK `Id`, tenant-safe
composite FK to `PipelineDefinitionVersion` via
`(TenantId, PipelineDefinitionVersionId)` → `(TenantId, Id)`, `DeleteBehavior.Restrict`,
`HasIndex(TenantId, PipelineDefinitionVersionId, SortOrder)` unique,
`HasIndex(TenantId, PipelineDefinitionVersionId, Name)` unique.

Follow `PartyRelationshipConfiguration.cs`'s exact composite-FK pattern
(`docs/plans/2026-09-16-masterdata-phase0.5-execution-plan.md` Task 5 Step 6) — same
shape, new tables.

- [ ] **Step 6: `Opportunity` gains two nullable fields — no enforcement yet**

Add `public long? PipelineDefinitionVersionId { get; private set; }` and
`public long? PipelineStageId { get; private set; }` to `Opportunity`. **No setter, no
domain method, no CHECK constraint requiring them** — per Task 0 Decision 0.2, real
assignment is Phase 2's `ChangePipelineStage` command. In `OpportunityConfiguration.cs`,
add the two tenant-safe composite FKs (`IsRequired(false)`), matching `Party`'s
optional self-FK pattern from `PartyConfiguration.cs`
(masterdata-phase0.5-execution-plan.md Task 5 Step 5).

- [ ] **Step 7: Build, migrate, test**

```bash
dotnet build src/Modules/CRM/CRM.csproj
dotnet ef migrations add AddPipelineTables \
  --project src/Modules/CRM/CRM.csproj \
  --startup-project src/Modules/CRM/CRM.csproj \
  --output-dir Persistence/Migrations
dotnet ef database update \
  --project src/Modules/CRM/CRM.csproj \
  --startup-project src/Modules/CRM/CRM.csproj
dotnet test tests/CRM.Tests/CRM.Tests.csproj
```

Expected: 41 existing + new `PipelineDefinitionVersionTests` all pass; no existing test
touches pipeline fields, so none should need changes (purely additive).

- [ ] **Step 8: Commit**

```bash
git add src/Modules/CRM/Domain src/Modules/CRM/Persistence tests/CRM.Tests
git commit -m "Add pipeline_definitions/pipeline_definition_versions/pipeline_stages (additive)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 3: `Opportunity.PartyId` → `PartyRef` — MasterData cutover

Implements `docs/plans/2026-09-16-masterdata-party-foundation.md` §9, steps 2–4 and 6–7.
**Step 1 (MasterData schema ready) and step 5 (application cutover — Host registers
`MasterDataDbContext`, `IPartyDirectory`/`IPartyIdentityResolver` wired) are already
done**, from Phase 0.5. This task does the data move and the `Opportunity` cutover.

**Files:**
- Create: `src/Modules/CRM/Persistence/Migrations/<timestamp>_BackfillMasterDataParties.cs` (generated, hand-written `Sql()` data copy)
- Create: `tests/CRM.Tests/Integration/PartyBackfillVerificationTests.cs`
- Modify: `src/Modules/CRM/Domain/Opportunity.cs` (`PartyId` → `PartyRef`)
- Modify: `src/Modules/CRM/Persistence/Configurations/OpportunityConfiguration.cs`
- Modify: `tests/CRM.Tests/TestData.cs` (Party-seeding helper, now against `MasterDataDbContext`)
- Modify: `tests/CRM.Tests/Integration/OpportunityPersistenceTests.cs`
- Modify: `tests/CRM.Tests/Integration/OpportunityConcurrencyTests.cs`
- Modify: `tests/CRM.Tests/Integration/CompleteOpportunityHandlerTests.cs`
- Modify: `tests/CRM.Tests/Integration/TenantIsolationTests.cs` (if it seeds a Party — verify)
- Modify: `tests/CRM.Tests/Integration/PostgresFixture.cs` (needs to run **both**
  `CrmDbContext` and `MasterDataDbContext` migrations now)
- Create: `src/Modules/CRM/Persistence/Migrations/<timestamp>_DropCrmParties.cs` (generated, final step, own sub-task — see Step 7)

**Confirm Task 0 Decision 0.1 before Step 1.**

- [ ] **Step 1: Backfill migration — copy `crm.parties` into `masterdata.parties`**

Generate an empty CRM migration, then hand-write the data copy into its `Up` (same
"EF-generated schema, hand-written `Sql()` data move" pattern as
masterdata-party-foundation.md §9 step 2 — not a second RLS-style full exception, since
no schema shape changes here, only a cross-schema data copy the EF diff can't express):

```bash
dotnet ef migrations add BackfillMasterDataParties \
  --project src/Modules/CRM/CRM.csproj \
  --startup-project src/Modules/CRM/CRM.csproj \
  --output-dir Persistence/Migrations
```

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("""
        INSERT INTO masterdata.parties (id, tenant_id, party_type, name, surname, phone, email, merged_into_party_id, created_at, updated_at)
        SELECT id, tenant_id, 'organization', name, surname, phone, email, merged_into_party_id, created_at, updated_at
        FROM crm.parties;
        """);
    migrationBuilder.Sql("""
        SELECT setval(pg_get_serial_sequence('masterdata.parties', 'id'),
            GREATEST((SELECT COALESCE(MAX(id), 0) FROM masterdata.parties), 1));
        """);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("DELETE FROM masterdata.parties WHERE id IN (SELECT id FROM crm.parties);");
}
```

**Sub-question, RESOLVED (see Task 0's confirmation note above):** the `'organization'`
literal above was flagged rather than silently buried, since `crm.parties` has no
`party_type` today and `MasterData.Party` requires one. **Confirmed by Ibrahim
(2026-09-16), as written** — every CRM party seeded so far is a company/organization
contact record in practice, per `CRM_CURRENT_STATE_ANALYSIS.md`'s domain description, so
defaulting to `Organization` is correct. No per-row rule needed.

- [ ] **Step 2: Data/invariant verification test**

`tests/CRM.Tests/Integration/PartyBackfillVerificationTests.cs` — Testcontainers test
seeding a handful of `crm.parties` rows pre-migration (or verifying post-migration
state directly, since Testcontainers runs every migration fresh each test run): assert
row-count match between `crm.parties` and `masterdata.parties`, and that every
`(tenant_id, id)` pair in `crm.parties` exists in `masterdata.parties`. This is
`masterdata-party-foundation.md` §9 step 3.

- [ ] **Step 3: `Opportunity.PartyId` → `PartyRef`**

Replace `public long PartyId { get; private set; }` with a `PartyRef`-shaped pair:
`public long PartyRefPartyId { get; private set; }` (the `TenantId` half is
`Opportunity.TenantId` itself — see the CHECK below, not a separate stored column,
since it must always equal the opportunity's own tenant and a second column inviting
drift is worse than deriving it). Add a computed, not-mapped property:
`public PartyRef PartyRef => new(TenantId, PartyRefPartyId);`. Update `Create(...)`'s
signature from `long partyId` to `PartyRef partyRef` (validate
`partyRef.TenantId == tenantId` in the constructor guard — the same invariant the
dropped FK used to guarantee, now a domain-level check backed by the CHECK below).

In `OpportunityConfiguration.cs`: drop the `HasOne<Party>().WithMany()...` FK block
entirely (no cross-schema FK is possible — `MasterData` is a separate module/schema,
per `AGENTS.md`). Add:
```csharp
builder.Property(o => o.PartyRefPartyId).IsRequired();
builder.Ignore(o => o.PartyRef);
builder.ToTable(t => t.HasCheckConstraint(
    "ck_opportunities_party_ref_party_id_positive", "party_ref_party_id > 0"));
```
This is the `Contracts`-level `PartyRef` pattern the party-foundation design doc §2/§11
describes: tenant-safety recovered by construction (the ref always carries the
opportunity's own `TenantId`), not by a cross-schema FK Postgres can't express anyway.

- [ ] **Step 4: `crm.parties`' FK-dropping migration**

```bash
dotnet ef migrations add DropOpportunityPartyForeignKey \
  --project src/Modules/CRM/CRM.csproj \
  --startup-project src/Modules/CRM/CRM.csproj \
  --output-dir Persistence/Migrations
```
Verify the generated migration drops the old FK and renames/repurposes the column to
`party_ref_party_id` (EF may generate a drop+recreate rather than a rename — if so,
this is a data-preserving `ALTER TABLE ... RENAME COLUMN` you should add by hand rather
than let EF drop the column and lose the data, same discipline as Step 1's hand-written
`Sql()`).

- [ ] **Step 5: Update `PostgresFixture` to run both DbContexts' migrations**

`tests/CRM.Tests/Integration/PostgresFixture.cs`'s `InitializeAsync` currently runs
only `CrmDbContext.Database.MigrateAsync()`. It now needs `MasterDataDbContext`'s
migrations too (same container, same database, two schemas) — add a second
`MigrateAsync()` call against a `MasterDataDbContext` built from the same connection
string. Mirror `RuntimeConnectionStringAsync`'s grant SQL to include the `masterdata`
schema grants too (currently CRM-only).

- [ ] **Step 6: Update `TestData.cs` and every test that seeds a Party**

`tests/CRM.Tests/TestData.cs` gets a helper seeding through `MasterData.Domain.Party`
against a `MasterDataDbContext` (same connection string as the CRM context under test —
same physical database, two schemas), returning a `PartyRef`. Every call site in
`OpportunityPersistenceTests`, `OpportunityConcurrencyTests`,
`CompleteOpportunityHandlerTests` (and `TenantIsolationTests` if it seeds one — verify)
changes from `Party.Create(tenant, "Acme", PartyCreationSource.Manual)` +
`Opportunity.Create(tenant, party.Id, ...)` to the new helper + `Opportunity.Create(
tenant, partyRef, ...)`.

- [ ] **Step 7: Build, test, then the final drop (only after everything above is green)**

```bash
dotnet build
dotnet test
```
Expected: full suite green (CRM's renamed/updated tests + pipeline tests + backfill
verification + MasterData's own 30). Only once this is fully green:
```bash
dotnet ef migrations add DropCrmParties \
  --project src/Modules/CRM/CRM.csproj \
  --startup-project src/Modules/CRM/CRM.csproj \
  --output-dir Persistence/Migrations
```
Verify it drops `crm.parties`' RLS policy, then the table, with a real `Down` that
recreates the table shape (data is *not* restorable by `Down` — that's expected and
fine per `masterdata-party-foundation.md` §9's "reversible at every step 1–6; nothing
dropped until 7"). Apply and run the full suite one more time.

- [ ] **Step 8: Commit**

Consider splitting Steps 1–6 into one commit and the drop (Step 7's second half) into a
separate commit — the drop is the one irreversible-in-practice step in this task and
deserves its own clearly-labeled commit rather than being buried in a larger diff:
```bash
git add src/Modules/CRM tests/CRM.Tests
git commit -m "Cut Opportunity over from Party FK to PartyRef, backfill masterdata.parties

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```
then, after Step 7's verification:
```bash
git add src/Modules/CRM/Persistence/Migrations
git commit -m "Drop crm.parties now that Opportunity resolves Party through MasterData

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 4: Docs, CI, memory sync

**Files:**
- Modify: `README.md`
- Modify: `AGENTS.md`
- Modify: `docs/schema/crm-sales-schema.md` (lifecycle values, Party section — note the
  FK is gone, `PartyRef` is now the link; pipeline tables)
- Modify: `docs/plans/2026-09-16-crm-target-model-phase0-delta-plan.md` (§6 Phase 1 row
  → ✅ Done; §11 gets a short Phase 1 completion note)
- Modify: `graphify-out/` (`graphify update .`, no manual edits)

- [ ] **Step 1: `dotnet format --verify-no-changes` + full build/test**

```bash
dotnet format --verify-no-changes
dotnet build -c Release
dotnet test
```

- [ ] **Step 2: Update `docs/schema/crm-sales-schema.md`**

Lifecycle enum values, the dropped `Party` FK (now `PartyRef`, no cross-schema FK),
the new pipeline tables — same doc-sync discipline used for Phase 0.5
(`docs/plans/2026-09-16-masterdata-phase0.5-execution-plan.md` Task 10).

- [ ] **Step 3: Update `README.md` and `AGENTS.md`'s Status section**

Mirror how Phase 0.5's Task 10 updated them — new test count, new tables, `Opportunity`
now referencing `MasterData` through `PartyRef`/`IPartyDirectory` instead of an
in-schema FK.

- [ ] **Step 4: Update the delta plan's phase table and decision record**

`docs/plans/2026-09-16-crm-target-model-phase0-delta-plan.md` §6: Phase 1 row Status →
`✅ Done`. §11: append a short "Phase 1 complete" line mirroring Phase 0.5's.

- [ ] **Step 5: Graph + final commit**

```bash
graphify update .
git add README.md AGENTS.md docs/schema docs/plans graphify-out
git commit -m "Sync docs with the completed Lifecycle/Pipeline foundation (Phase 1)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Acceptance criteria

- `OpportunityStatus` is `Draft/Open/Won/Lost` everywhere (domain, config, CHECK names/
  values, all tests) — no trace of `Waiting/Offered/Completed/Canceled` left except in
  historical plan documents.
- `pipeline_definitions`/`pipeline_definition_versions`/`pipeline_stages` exist,
  additive, versioned, no enforcement on `Opportunity` yet (Phase 2's job).
- `Opportunity` resolves its Party via `PartyRef`, not a same-schema FK. `crm.parties`
  is dropped. `masterdata.parties` holds every row `crm.parties` used to.
- `tests/CRM.Tests` and `tests/MasterData.Tests` both fully green; `PostgresFixture`
  migrates both schemas.
- `dotnet format --verify-no-changes` clean; CI green.
- **Untouched, as scoped:** `OpportunityLine`'s monetary shape (§5.B, Phase 5's job);
  no new commands/endpoints/authorization (Phase 2's job, gated on Phase 1.5 per §5.D).
