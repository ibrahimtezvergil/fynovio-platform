# MasterData / Party Foundation — Design Reference (Phase 0.5)

> **Status:** design reference, approved, **implemented**. This is the detailed
> record behind `docs/plans/2026-09-16-crm-target-model-phase0-delta-plan.md` §11 —
> that file's §5.C question ("Contact — build now or defer?") opened this larger
> reconciliation, which settled Party ownership, external identity, merge semantics,
> and the CRM/Sales boundary for commercial transactions. Every decision below was
> reached through several rounds of explicit review with the platform owner; none is
> silently inferred.
>
> **Progress:** this design is not a checkbox task list (see §10's acceptance criteria
> for what "done" means). Section-level status: §§1–9 (design) are settled and frozen;
> §11 (deferred items) stays deferred by definition. **The execution plan ran to
> completion:** `docs/plans/2026-09-16-masterdata-phase0.5-execution-plan.md` (11 tasks,
> all 11 committed, §10's acceptance criteria met — `tests/MasterData.Tests` 30/30,
> `tests/CRM.Tests` 41/41 with no regression). Status also mirrored in
> `docs/plans/2026-09-16-crm-target-model-phase0-delta-plan.md` §6's phase table.

## 1. Why Party moves out of CRM

Party is CRM-owned today (`crm.parties`, real composite FK from `Opportunity`). Two
things changed this:

1. **Sales was re-separated from CRM** the same day it had briefly been merged (a named
   pilot exception the owner corrected after reviewing the target-model PDF — see
   [[feedback-open-decisions]]). Sales will need to resolve a customer identity to
   render commercial documents.
2. **Opportunity is not the mandatory parent of a sale.** The platform supports both:
   - CRM-driven: `Party → Opportunity → Quote → Order`
   - Direct transactional: `Party → Order/Sale` (retail POS, ERP-imported history,
     anything that shouldn't require a synthetic Opportunity)

   This means Sales, Order, Finance, Service, and Integrations all need to resolve or
   create Party **independently of CRM**. If Party stayed CRM-owned, every one of those
   modules would either depend synchronously on CRM (wrong — CRM shouldn't be
   load-bearing infrastructure for Finance) or invent its own parallel identity concept
   (identity fragmentation, the exact failure mode this whole reconciliation exists to
   prevent).

`src/Modules/MasterData/` already exists as a reserved placeholder in the solution
(currently just a `Class1.cs` template, not in the `.sln`) — it becomes Party's real
home.

## 2. PartyRef — a strongly-typed reference, not generic EntityRef

`Contracts.EntityRef` (`TenantId` + `BoundedContext` + `EntityType` + `Id`) is the
platform's existing polymorphic cross-module reference primitive, already used for
`OpportunityLine.ProductRef`. For Party, `BoundedContext`/`EntityType` would always be
the same two constant strings — carrying that genericity buys nothing and wastes two
columns per row.

```csharp
public readonly record struct PartyRef
{
    public TenantId TenantId { get; }
    public long PartyId { get; }
}
```

Lives in `Contracts` (not `MasterData`) — exactly like `EntityRef`/`TenantId`/
`PrincipalRef`. This matters structurally: `MasterData` owns the `Party` **entity**,
not the `PartyRef` **type**. If `PartyRef` lived in `MasterData.Application`, CRM would
need a project reference to `MasterData` to use it — which fails
`tests/CRM.Tests/Architecture/ModuleBoundaryTests.cs`'s existing
`Crm_does_not_depend_on_another_module` test (verified: `"MasterData"` is already in
its forbidden-dependency list).

**Persistence, e.g. on `opportunities`:**
```
tenant_id
party_ref_tenant_id
party_ref_id
CHECK (party_ref_tenant_id = tenant_id)
```

This is a **tenant-safety upgrade** over the existing `ProductRef` pattern, not just a
different shape: `OpportunityLine.ProductRef` reconstructs its `TenantId` from the
row's own `TenantId` rather than storing it separately, so same-tenant-ness is assumed,
never checked. `PartyRef`'s explicit column + `CHECK` recovers part of what's lost when
a real composite FK becomes a reference — the FK's cross-schema referential guarantee
is still gone (only `MasterData` can confirm the Party actually exists — see §4), but
the tenant-match guarantee is preserved at the DB level.

**Convention this establishes, to note in `AGENTS.md` later (not this document):** use
a strongly-typed `XRef` when the referenced aggregate is statically known
platform-wide (Party — there's only ever one); keep generic `EntityRef` for genuinely
polymorphic references (Product — may vary by catalog/module over time).

## 3. Party = Person | Organization; Contact is a role, not an entity

`party_type` (`Person` | `Organization`) returns — **a precondition of this model, not
a side effect of it.** It's what makes every downstream distinction possible (which
relationship types are valid, whether a Party can be a stakeholder).

The deciding case: a person (Ahmet Yılmaz) can be Procurement Manager at one
organization and Board Member at another. A flat `Contact` table would either duplicate
his identity per organization (breaking consent ownership — a KVKK withdrawal should
apply to the person, not fragment per relationship) or require a many-to-many join
anyway, at which point it's `PartyRelationship` under a different name.

**Resolution:** Contact is not an entity — it's *a Person Party related to an
Organization Party via a `works_for` `PartyRelationship`.* `job_title`, `work_email`,
`work_phone` live on the relationship row (context-specific), not on the Person
(personal channels + the single consent-subject identity stay on the Person).

**`PartyRelationship`:**
```
id, tenant_id, from_party_id, to_party_id, relationship_type, status, started_at?, ended_at?, metadata?
```
- `relationship_type`: closed vocabulary, starts with exactly two values —
  `works_for`, `branch_of`. No `parent_of`/`subsidiary_of`/`distributor_for` yet; those
  get added only against a validated request, same discipline as `OpportunityStatus`'s
  closed enum.
- **Temporal model: `status` (Active/Ended) + nullable `started_at`/`ended_at`**, not
  raw `effective_from`/`effective_to`. Different relationship types have genuinely
  different date-certainty: `works_for` usually has known start/end dates,
  `branch_of` usually doesn't ("we don't know exactly when, we just know it currently
  is"). `status` is always answerable; the dates are optional precision on top of it.
  Same single-row-invariant discipline as everywhere else in this codebase:
  `ended_at IS NOT NULL → status = Ended` is a `CHECK`.
- `metadata` (jsonb) is the escape hatch for type-specific richness later (e.g. a
  future `distributor_for`'s contract terms) — **explicitly not to be used as a
  substitute for promoting a relationship type to its own aggregate/capability if it
  grows real business rules of its own.** Noted as a design principle, not resolved
  further now.

## 4. IPartyDirectory vs. IPartyIdentityResolver — read/display is not command/identity

Two separate `Contracts`-level contracts, both implemented by `MasterData.Application`,
wired at `Host` (the only composition root):

```csharp
// Read/display — cheap, side-effect-free, no strong-consistency requirement
public interface IPartyDirectory
{
    Task<PartyDirectoryEntry?> GetParty(PartyRef partyRef, CancellationToken ct = default);
    Task<IReadOnlyDictionary<PartyRef, PartyDirectoryEntry>> GetParties(
        IReadOnlyCollection<PartyRef> partyRefs, CancellationToken ct = default);
}

// Command precondition / identity establishment — must reflect current committed state
public interface IPartyIdentityResolver
{
    Task<bool> ExistsAsync(PartyRef partyRef, CancellationToken ct = default);
    Task<PartyRef?> ResolveExternalIdentityAsync(
        TenantId tenantId, string provider, string sourceInstanceRef,
        string? externalType, string externalId, CancellationToken ct = default);
    Task<PartyRef> ResolveOrCreateAsync(/* ... */ CancellationToken ct = default);
}
```

**Why two, not one:** a future `CreateOpportunity` command must never depend on the
incidental behavior of a display-oriented contract for its own correctness (e.g. a
cached/stale-tolerant read used to validate that a Party exists before creating an
Opportunity against it). This is the same command/query separation `AGENTS.md` already
applies *within* a module's own Application layer, extended across a module boundary —
not a new principle.

**Merge-awareness:** both `ExistsAsync` and `ResolveOrCreateAsync` must resolve through
the merge chain (§5) and return the *canonical* `PartyRef` — a caller who passes a
since-merged Party id must never get a new Opportunity created against a tombstone.

**No cross-module ACID transaction, and this doesn't create one:** `AGENTS.md`'s outbox
rule governs *write-side* state propagation between modules; it's silent on synchronous
reads. A module's public surface already includes "queries" by its own wording.
`IPartyDirectory`/`IPartyIdentityResolver` are read paths — they don't require the two
modules' data to be transactionally joined, each still executes against its own
`DbContext`/connection. Concretely: a lookup mid-CRM-transaction opens a **second**
connection into `MasterDataDbContext`, sets its own RLS tenant context, and returns a
plain DTO — never a tracked entity. This is exactly why batch lookup (`GetParties`) is
load-bearing, not a nicety: an Opportunity list of 50 rows needs one extra round-trip
to MasterData, not fifty.

## 5. Party merge / canonical identity

Extends the existing `Party.MergeInto(canonicalPartyId)` (already tombstone-based, no
hard delete) rather than replacing it — the gap was on the *resolution* side, which
never existed because nothing today reads `MergedIntoPartyId`.

- **Party ID is permanent** — never reused, never changes. Every `PartyRef` embedded
  anywhere (an old `Opportunity`, a future `Quote`) stays resolvable forever.
- **No hard delete of a merged Party** — unchanged from today's behavior.
- **`IPartyDirectory`/`IPartyIdentityResolver` follow the chain to canonical** — new
  behavior: looking up a tombstoned Party returns the *survivor's* current data, never
  the tombstone's stale copy.
- **`PartyExternalIdentity` rows move to the survivor at merge time** — a same-schema,
  same-transaction `UPDATE`, no cross-module problem since `MasterData` owns both
  tables.
- **No merge chains — single hop only, structurally enforced.** `MergeInto` resolves
  its target to *that target's own* canonical first, so `MergedIntoPartyId` is never
  more than one hop from canonical. Merging #300 "into #200" when #200 is itself
  already a tombstone actually merges #300 into #200's canonical (#100) directly. If a
  canonical Party is later merged into something else, every tombstone that already
  pointed at it gets its `MergedIntoPartyId` rewritten in the same transaction — still
  entirely within `MasterData`'s own schema.
- **Cycle-safety is a consequence of the single-hop rule**, not a separate mechanism —
  a tombstone's target is always guaranteed non-tombstone, so a cycle cannot form.

## 6. PartyExternalIdentity — provider vs. source instance

The four-integration example (DIVA ERP, SAP, Legacy CRM, E-commerce — one Party, four
external ids) rules out a single `external_source`/`external_id` pair on `Party`
outright, and a further refinement is needed: a tenant can run **multiple instances**
of the same provider (`SAP Connection A` and `SAP Connection B`, each with its own
customer-1001), so "provider" and "which specific connection" are distinct concepts.

```
PartyExternalIdentity
  id, tenant_id, party_id, provider, source_instance_ref, external_type?, external_id, created_at
```

- `provider`: classification (`"sap"`, `"diva_erp"`, `"legacy_crm"`, `"ecommerce"`) —
  required, used for filtering/reporting, not part of the uniqueness key.
- `source_instance_ref`: tenant-scoped opaque string identifying the specific
  configured connection. Not backed by its own "Integration Connection" entity/table —
  that capability doesn't exist and isn't being built now (YAGNI).
- `external_type`: nullable — included in uniqueness only when a source's own ID
  namespace genuinely needs it (e.g. one source shares a numbering scheme across
  "customer" and "vendor" records).

**Concrete gotcha caught and fixed:** PostgreSQL unique indexes treat `NULL <> NULL`, so
a plain `UNIQUE(tenant_id, source_instance_ref, external_type, external_id)` would
**not** enforce anything for the common case where `external_type` is always `NULL` —
silently defeating the whole point. Correct form is an expression index:

```sql
CREATE UNIQUE INDEX ... ON masterdata.party_external_identities
  (tenant_id, source_instance_ref, COALESCE(external_type, ''), external_id);
```

FK: `(tenant_id, party_id) → masterdata.parties(tenant_id, id)` — intra-schema, no
cross-module issue.

## 7. Customer / Prospect semantics

Counter-example that overturned the earlier "Customer = ≥1 Won Opportunity" proposal: a
Party with 10 years of real ERP sales history and zero CRM Opportunities is obviously a
Customer — deriving the status purely from CRM's own Opportunity table is
domain-incomplete, since the evidence can live entirely outside CRM (Order, Finance,
an ERP import).

**Frozen:** Customer status is **asserted**, not derived from a single domain's live
data — any domain (CRM's `WinOpportunity`, an ERP import, Finance) can prove it.
Prospect can stay CRM-context-derived (an open, not-yet-won Opportunity) — the two are
not symmetric: a business doesn't have "prospect history" outside a sales funnel the
way it has customer/transaction history.

**Open, deliberately not resolved:** *where* the assertion lives — a `MasterData`
extension, a separate small "Shared Customer" capability, or (if Vendor/Partner/credit
terms/relationship-health ever materialize) a future `CommercialRelationship` domain.
Vendor/Partner are explicitly **not** modeled at all right now, not even as reserved
enum values — no Procurement or partner-management capability exists anywhere in the
code or the target-model PDF; including them would be pure speculation.

**Decision trigger, so this doesn't drift unowned:** resolved when the first real
command needs to *write* Customer status — most likely `WinOpportunity` (Phase 2) or a
Sales/Finance phase, not before.

## 8. Sales document snapshot invariant (forward note, not implemented)

Same principle already applied once in this codebase
(`OpportunityNeed.EstimatedValueSnapshot` — "value copied when attached so historical
reporting doesn't change when the catalog reference changes"), applied a second time
here:

- `PartyRef` = canonical identity link, always resolves to the **current** Party.
- `QuoteVersion`'s snapshot fields (customer/company name, address, tax/company
  identifiers, commercial values) = the ticari gerçek **at the moment the document was
  created**, copied once, never re-synced.

**Deliberately asymmetric read semantics on the same `PartyRef`:** an Opportunity list
view showing "current customer name" should be live, via `IPartyDirectory`. A
`QuoteVersion`'s formal document content must never be live — same reference type, two
different consumers, two different correctness requirements. Recorded for Phase 5
(Sales Quote split); no schema or code exists yet.

## 9. CRM → MasterData migration sequence (planned for Phase 1, not executed in Phase 0.5)

Phase 0.5 builds `MasterData` **fully isolated** — `crm.parties` is not touched. This
sequence is the plan for *when Phase 1 actually moves the data*, documented now per the
owner's request, not run yet. Practical risk today is low — no production tenants exist
([[project-scope-local-only]]) — so this is mainly about establishing the right
process/pattern, reusable for any future schema move, not mitigating real data loss.

1. **MasterData schema ready** — `masterdata.parties` created **empty**, in its final
   shape (`party_type` included), via a normal EF-generated migration.
   `crm.parties` untouched; both tables briefly coexist.
2. **Existing Party data copy/backfill** — ID-preserving:
   `INSERT INTO masterdata.parties (id, tenant_id, ..., party_type) SELECT id,
   tenant_id, ..., <backfill> FROM crm.parties;` then realign `masterdata.parties`'s
   identity sequence to the copied max id. This is a normal "EF-generated schema +
   hand-written data-backfill `Sql()` call" migration — doesn't need a new named
   exception the way RLS did, since the *schema* operations stay EF-generated; only the
   data copy is a manual `Sql()` addition on top.
3. **Data/invariant verification** — row-count match + every `(tenant_id, id)` pair
   present in both tables, as a Testcontainers integration test.
4. **Opportunity PartyRef migration** — `Opportunity.PartyId` (long, FK) →
   `Opportunity.PartyRef`; FK dropped, `party_ref_tenant_id`/`party_ref_id` + `CHECK`
   added; existing `party_id` values copied into `party_ref_id`, `party_ref_tenant_id`
   backfilled from the row's own `tenant_id`.
5. **Application cutover** — `Host` registers `MasterDataDbContext`;
   `IPartyDirectory`/`IPartyIdentityResolver` wired via DI.
6. **Tests** — `tests/MasterData.Tests` plus every CRM test that seeds a Party
   (`OpportunityPersistenceTests`, `OpportunityConcurrencyTests`,
   `CompleteOpportunityHandlerTests`, `TenantIsolationTests`) updated to the new
   `PartyRef`/`MasterDataDbContext` seeding path; full suite green.
7. **Old `crm.parties` cleanup/drop** — only after 3–6 are fully verified: a separate,
   reversible migration drops the table, its RLS policy, and any FK remnant.

Reversible at every step — 1–6 leave `crm.parties` intact; nothing is dropped until 7,
and every earlier migration has a real `Down`.

## 10. Phase 0.5 acceptance criteria

- `Contracts`: `PartyRef`, `IPartyDirectory`, `IPartyIdentityResolver` added.
- `MasterData.Domain`: `Party` (+`party_type`), `PartyRelationship` (`works_for`/
  `branch_of`, `status`+nullable dates), `PartyExternalIdentity` (provider +
  source_instance_ref + external_type? + external_id, `COALESCE`-based unique index),
  merge logic (single-hop invariant, external-identity reassignment).
- `MasterData.Application`: `CreateParty`, `MergeParty`, `ResolveOrCreateParty` —
  satisfying the **full** doc 20 binding core (same rigor CRM met, not a lighter
  version): own `outbox_messages`, own `idempotency_records`, evidence record on
  `MergeParty` (hard-to-undo, audit-worthy — same risk category as money/auth/
  cancellation).
- `MasterDataDbContext` + design-time factory + `InitialMasterDataSchema` migration +
  RLS migration (`ENABLE`+`FORCE`+policy on all three tables).
- `scripts/create-runtime-role.sql` gets a `masterdata` schema grant block.
- `tests/MasterData.Tests`: `Domain/` (merge single-hop/cycle-prevention, external
  identity reassignment), `Architecture/` (`MasterData` doesn't depend on CRM/Access/
  Sales — symmetric to the existing CRM `ModuleBoundaryTests`), `Integration/` (RLS
  isolation against `fynovio_app` on all three tables; `IPartyDirectory`/
  `IPartyIdentityResolver` behavior including merge resolution).
- CI green (format, build, vulnerability check, full suite including the new tests).
- **Explicitly out of scope for Phase 0.5's acceptance:** `crm.parties` data, the
  `Opportunity.PartyRef` migration, updates to CRM's own tests — all of §9, which is
  Phase 1's job. Phase 0.5 stands alone.

## 11. Explicitly deferred (unchanged from prior review, restated for this document's completeness)

Generic `PartyRole` table, Vendor/Partner, `OpportunityStakeholder`, Territory, fuzzy
identity resolution / merge workflow / golden record, a full automation engine, rich
`PartyRelationship.metadata` usage — none scheduled, none designed further than the
principle already recorded in §3 and §7.
