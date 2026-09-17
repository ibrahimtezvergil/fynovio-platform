# PHASE 2 — CRM Opportunity Commands & API — Planning Document

**Status:** PLANNING ONLY — no production code, migration, or endpoint has been implemented as part of this document.
**Prepared against:** `CLAUDE_PHASE_2_EXECUTION_PROMPT.md`, `PHASE_2_OPPORTUNITY_COMMANDS_API_BINDING_SPEC.md` (v2.0), `Enterprise_CRM_Target_Model_Binding_Implementation_Specification.pdf`, repository state as of commit `5d5331e` (2026-09-17, `main`).
**Precedence used:** repository = source of truth for what's built; binding docs = source of truth for intended behavior; where the Phase 2 spec and the execution prompt disagree on form (e.g. the final summary shape), the execution prompt's exact wording is used since the user invoked this document via that prompt.

---

## 0. How to read this document

Sections 1–2 are the two things that need an owner decision. Everything after is verified evidence supporting those two sections. If you only read two sections, read those.

---

## 1. Verified Phase 1.5 capability inventory (read this before anything else)

The Phase 2 binding spec repeatedly says Phase 2 "MUST consume" Phase 1.5 authorization, field-level security (FLS), policy obligations/`RequireApproval`, and territory/team/org scope — but every one of those clauses is conditional in the spec's own text (§4B: "if it exists"; §4C: "If Phase 1.5 supports obligations"; §13A: "according to the existing Phase 1.5 contract"). Phase 1.5 is complete and merged (`97743cb` "Merge Phase 1.5 (Enterprise Access Foundation) into main"; `AGENTS.md:103`), and its narrowness is a **named, adversarially-reviewed, frozen scope decision** — not a gap, not an oversight, and not a conflict between two binding documents. The table below is the discharge of those conditionals.

| Capability | Status | Evidence |
|---|---|---|
| Coarse action authorization (`crm.opportunity.*` → Allow/Deny) | **Available — consume it** | `Contracts.IAuthorizer.AuthorizeAsync`, impl `src/Modules/Access/Application/AccessAuthorizer.cs:16-46`. Default-deny; unregistered action → deny (`AccessAuthorizer.cs:21-22`); unrecognized principal → deny (24-26); RBAC via `RoleAssignment→RolePermissionSet→PermissionSetItem(ActionKey)` (29-35) |
| Tenant-wide grant (`relation == null`) | **Available — consume it** | `AccessAuthorizer.cs:37-38` |
| Record-level owner-relation grant (`OwnedBy`) | **Available — consume it, gated by an inherited open decision (§2.2)** | `AccessAuthorizer.cs:40-43`; `Contracts.ScopeTerm.OwnedBy(PrincipalRef)`; round-3 freeze #9 "Owner relation (`OwnedBy`) Phase 1.5'te implement edilir" |
| Query-side scope resolution (`None`/`All`/`OwnedBy`) with `Authorize(row)==Allow ⟺ row∈ResolveAccessScope(...)` equivalence | **Available — consume it** | `Contracts.IAccessScopeResolver`, `AccessScope`, `ScopeTerm`; `AuthorizeResolveAccessScopeEquivalenceTests` (`tests/Access.Tests/Application/`); freeze #11 |
| Action registry / registered-action gate | **Available — consume it** | `Contracts.IActionCatalog`, `AccessActionCatalogSeeder` run at Host startup (`src/Host/Program.cs:40-44`) |
| Field-Level Security (READ/WRITE) | **Frozen out — do not build it in Phase 2** | Ownership matrix lists "Field Security evaluation" as an *Access subdomain* but round-3 §8 freeze #14 and Scope Lock §9 keep it DESIGN/FREEZE; `identity-access-schema.md:184`: "Sharing, Field Security, Restriction/forbid policy tables — DESIGN/FREEZE... no schema exists yet" |
| Sharing / Org / Team / Territory scope | **Frozen out — do not build it in Phase 2** | Freeze #10: "Query scope: None/All/OwnedBy IMPLEMENT; Org/Territory/Team/Sharing DESIGN/FREEZE"; `Contracts.ScopeTerm` has only `OwnedBy` (no `InOrgNodes`/`InTerritories`/`SharedWith`, comment confirms "added... when their fact provider exists, never guessed at today") |
| Restriction/forbid layer (explicit deny grants) | **Frozen out** | Freeze #4: "Phase 1.5'te tenant-authored forbid yok" |
| Policy obligations / `RequireApproval` / masking / limits | **Frozen out — no runtime exists** | `Contracts.AuthorizationDecision` is `{Effect: Allow|Deny, ReasonCode, DecisionId, Revision}` only — comment: "`MatchedGrants`/`Obligations` are not here yet: restriction/field-security/step-up don't exist in Phase 1.5" (`src/Contracts/AuthorizationDecision.cs:4-6`). Ownership matrix assigns "Approval policy/path/version" to Workflow/Rules and "Human task lifecycle" to a Human Task/Approval runtime — **neither module exists in the repo** (confirmed: no such `src/Modules/*` directory) |
| Sensitive Data Projection Policy (per-channel classification for outbox/evidence/API) | **Frozen out — explicitly named as inherited, not fixed** | Closure matrix row "Sensitive Data Projection": `CompleteOpportunityHandler` writes the identical `payloadJson` to both `OutboxMessages` (lines 74-84) and `EvidenceRecords` (86-94) with zero classification; round-3 doc states this is "Phase 1.5'te düzeltilmiyor (DESIGN/FREEZE), ama execution planına girecek somut bir 'mevcut durum' kaydı" — i.e. explicitly punted to Phase 2 |
| Authorization reasoning over *proposed* mutation values (e.g. amount-threshold-triggers-approval, §4A) | **Vacuously satisfied — not violated, nothing to consume** | `Contracts.ResourceDescriptor` = `{ResourceType, Id?, OwnerPrincipal?}` (`src/Contracts/ResourceDescriptor.cs`) has no slot for a proposed value, and no Phase 1.5 grant depends on one. §4A's rule ("do not authorize only current state when the decision depends on proposed values") has nothing to violate today because Phase 1.5 defines no such rule. `ResourceDescriptor` is sufficient for every Phase 2 command as currently scoped; if a future amount-threshold rule is authored, it is an Architecture Delta that extends `ResourceDescriptor`, not a Phase 2 invention |

**Route for anything in the "Frozen out" rows, if Phase 2 turns out to need it:** round-3 Scope Lock §9 — `STOP → Architecture Delta note → owner approval → Phase 1.5 scope update`. Phase 2 must not silently implement any of them.

**What this means for Phase 2 concretely:** every Opportunity command can and must go through coarse `crm.opportunity.<verb>` action authorization plus, where the action is scoped to "your own records," the `OwnedBy` relation. Field-level write/read restriction, approval workflows, and territory/team scoping are **not available inputs to Phase 2's design** — the plan below does not build them, and the query/available-actions projections in §9 and §14 are plain booleans because there is nothing richer to collapse (§13A's "do not collapse richer policy outcomes into misleading booleans" has no richer outcome to collapse today).

---

## 2. OPEN DECISIONS — owner action required before implementation

These gate specific pieces of Phase 2. None are silently resolved here.

> **RESOLVED 2026-09-18 — owner approval received for all seven items below.** §2.1 (authentication): **JWT bearer** — a specific choice among the plan's own (a)/(b)/(c) options, made explicitly by the owner rather than recommended by this plan (this plan deliberately made no recommendation here). §2.2–§2.7: the owner approved this plan's own stated recommendation in each case, unchanged — Reassign() (2.2-a), `IsEntry` flag (2.3-i), `IsActive` flag (2.4-a), keep free-text `LostReason` (2.5-a), defer `ReopenOpportunity` (2.6), rename to `WinOpportunity`/`opportunity.won.v1` (2.7-a). Per binding spec §21 ("a recommendation is not approval"), these are recorded as resolved only now, not at drafting time. See §22 for the consequent execution-plan next step.

### 2.1 OPEN DECISION — Authentication mechanism (BLOCKER for the HTTP layer) — RESOLVED: JWT bearer

- **Issue:** No authentication pipeline exists anywhere in the repository. `src/Host/Program.cs` has no `AddAuthentication`/`UseAuthentication`/`AddAuthorization`/`AddJwtBearer` call, and `Host.csproj` references no auth package. `Contracts.ActorContext`'s own doc comment requires it be "built once from the authenticated session, never from a caller-supplied command body" (`src/Contracts/ActorContext.cs:4-5`), and `AccessAuthorizer` fails closed on `principal_not_recognized` if the principal can't be resolved (`AccessAuthorizer.cs:24-26`) — but nothing in the repo today produces a trusted principal from an inbound HTTP request. `src/Modules/TenantLifecycle` and `src/Modules/Organization` (both referenced by `Host.csproj`) are empty scaffold projects (`Class1.cs` only) — there is no "resolve tenant/organization context" implementation for §4A step 3 either.
- **Verified current state:** every existing test (`CompleteOpportunityHandlerTests`, `Access.Tests` handler tests) calls application handlers directly with hand-built `TenantId`/`PrincipalRef` values — none goes through HTTP or a session.
- **Options:** (a) JWT bearer (external IdP or platform-issued), (b) cookie/session auth via a first-party login flow, (c) API-key based service-to-service auth for the pilot only, deferring interactive login. Each has different implications for `PrincipalResolver`/`Account` linkage (`src/Modules/Access/Application/PrincipalResolver.cs`, `src/Modules/Access/Domain/Identity/Account.cs`, `ExternalIdentity.cs`).
- **Architectural impact:** every Phase 2 HTTP endpoint's `ActorContext` construction depends on this. The command/handler/domain layer can be designed and tested today (as the existing tests already do) without resolving it, but no endpoint can be exposed safely until it is.
- **Migration impact:** none directly, but the choice affects whether `Access.Domain.Identity.ExternalIdentity` needs new columns.
- **Recommendation:** (b)/(a) hybrid is typical for a B2B SaaS platform, but this is a genuine product/infra decision with no binding precedent in this repo — do not guess.
- **Why implementation must wait:** without it, Phase 2 can build and unit/integration-test commands and domain logic, but cannot ship real HTTP endpoints without either inventing an ad hoc auth shortcut (forbidden — "no always-allow authorization stub, ever," `feedback-open-decisions` memory) or leaving the API unauthenticated (forbidden by the entire authorization chapter of the binding spec).

### 2.2 OPEN DECISION — Physical "current owner" mapping for `Opportunity` (inherited from Phase 1.5, not new) — RESOLVED: option (a), `Reassign()`

- **Issue:** Business meaning is already frozen: Owner = "the principal currently responsible for this record," distinct from creator (round-3 closure matrix "CRM Owner semantics"). Physical mapping is not: `Opportunity.AssignedPrincipal` is set only in `Create()` (`src/Modules/CRM/Domain/Opportunity.cs:70-71`) with no `Reassign()` method anywhere in the aggregate (confirmed by grep across `src/Modules/CRM`) — so today it behaves as "assigned at creation," not "current owner." `AGENTS.md:103` states this explicitly as carried-forward Phase 2 work: "exactly which CRM field represents 'current owner' for the `OwnedBy` scope term... needs a `Reassign()` capability CRM doesn't have yet."
- **Verified current state:** `AssignedPrincipal` immutable post-create; `Contracts.ResourceDescriptor.OwnerPrincipal` is the only relationship fact Phase 1.5's PDP evaluates (`AccessAuthorizer.cs:40-43`) — so this decision gates **every** record-level (`OwnedBy`) authorization check on Opportunity, for every command, not just reassignment.
- **Options:** (a) add `Opportunity.Reassign(PrincipalRef newOwner)`, keep `AssignedPrincipal` as the single owner column; (b) add a distinct `OwnerPrincipalRef` column, decoupled from whoever was assigned at creation/working the deal.
- **Architectural impact:** (a) is a pure aggregate method addition, no new column, `ReassignOpportunity` command sets and reads the same field the record-level PDP fact provider (the PEP building `ResourceDescriptor`) reads. (b) requires a new nullable column + migration + a rule for what happens to `AssignedPrincipal` vs `OwnerPrincipalRef` divergence.
- **Migration impact:** (a) none (existing column, new setter). (b) one additive migration.
- **Recommendation:** (a) — no evidence in the target model or current schema calls for two separate principal-shaped fields on Opportunity, and doc `crm-sales-schema.md` already indexes `assigned_principal_issuer/subject` as the one identity-shaped field on the aggregate.
- **Why implementation must wait:** `ReassignOpportunity` cannot be designed, and `OwnedBy`-scoped authorization cannot be wired into *any* Opportunity command's PEP, until this is settled — this is not cosmetic.

### 2.3 OPEN DECISION — Entry/default pipeline stage *selection rule* (narrower than "does Phase 1 settle this") — RESOLVED: option (i), `IsEntry` flag

- **Issue:** The target model settles **when**: `OpenOpportunity` "moves Draft→Open and assigns initial Open stage" (target model PDF §14) — `CreateOpportunity` only selects a pipeline *version*, not a stage (same table: "pipeline version selected but Open stage not entered until activation"). It does **not** settle **which** stage becomes the initial one. `PipelineStage` (`src/Modules/CRM/Domain/PipelineStage.cs:5-32`) has no `IsEntry`/`IsDefault` flag — only `Id, TenantId, PipelineDefinitionVersionId, Name, SortOrder, CreatedAt`. §9A of the binding spec explicitly forbids assuming sort-order-first.
- **Verified current state:** `Opportunity.Open(DateTimeOffset expiryDate)` (`Opportunity.cs:93-104`) does not touch `PipelineStageId` at all today — it only sets `Status`, `ExpiryDate`, `OpenedDate`. No entry-stage concept exists anywhere in the schema or domain.
- **Options:** (i) explicit `PipelineStage.IsEntry` boolean, exactly one per `PipelineDefinitionVersion`, enforced by a partial unique index; (ii) sort-order-first (explicitly disallowed by §9A without a binding decision); (iii) caller-supplied stage on `OpenOpportunity`, validated against the assigned version's stage set.
- **Architectural impact:** (i) requires a migration adding `is_entry boolean` + a constraint ensuring exactly one `true` per version. (iii) requires no schema change but pushes the decision to the UI/caller, which conflicts with "the frontend must not reconstruct backend rules" (§13A) unless the valid set is still server-validated.
- **Migration impact:** (i) additive column + constraint on `pipeline_stages`.
- **Recommendation:** (i) — matches the target model's `pipeline_stages` target field list (§13 of the PDF: "active, capability/policy refs" alongside "code/stable key"), and keeps `OpenOpportunity` a zero-input state transition rather than requiring the caller to know pipeline internals.
- **Why implementation must wait:** `OpenOpportunity`'s exact signature and the pipeline migration in §11 both depend on this.

### 2.4 OPEN DECISION — Retired/inactive pipeline stage semantics — RESOLVED: option (a), `IsActive` flag

- **Issue:** `PipelineStage` has **no** active/inactive flag at all (`PipelineStage.cs:5-32` — confirmed by full read, not just a missing convention). The target model's suggested `pipeline_stages` shape explicitly wants one ("active" — PDF §13). Binding spec §9A requires this be decided before implementation, not defaulted.
- **Verified current state:** nothing retires a stage today; `Opportunity.PipelineStageId` has no domain setter at all yet (§2.3), so "what happens to Opportunities referencing a retired stage" has no current behavior to describe — it is a pure forward design question.
- **Options:** (a) `IsActive` boolean on `PipelineStage`; `ChangePipelineStage` rejects targeting an inactive stage but never touches historical references; a retired stage stays fully joinable for read/history. (b) soft-hide only at the query/UI layer, no domain concept (rejected — AGENTS.md's binding "no soft delete... use a domain status value instead" rule argues for (a)'s explicit flag over ad hoc hiding).
- **Architectural impact:** (a) is additive and low-risk since `Opportunity.PipelineStageId`/`PipelineDefinitionVersionId` FKs are `DeleteBehavior.Restrict` (`OpportunityConfiguration.cs:45,53`) — a stage can never be deleted out from under a historical reference regardless, only marked inactive for *new* transitions.
- **Migration impact:** one additive column + index; no data migration needed (default `true`).
- **Recommendation:** (a).
- **Why implementation must wait:** `ChangePipelineStage`'s precondition list depends on it.

### 2.5 OPEN DECISION — Structured `LostReason` timing — RESOLVED: option (a), keep free text

- **Issue:** Target model's Won/Lost table says Lost "requires structured loss reason... by tenant policy" (PDF §9, §14: "structured loss reason required by tenant policy"), and the Current-State→Target-Delta table (PDF §20) lists "Free-text cancel reason → introduce tenant/sector lost-reason taxonomy plus optional free-text note" as a target change — but the Required Implementation Sequence (PDF §21) does not assign this item to a specific phase number, and Phase 1's own plan explicitly deferred it "likely paired with Phase 2's authorization/policy work" (per prior session record). Today's `Opportunity.LostReason` is free text, DB-enforced only for non-null/non-whitespace (`Opportunity.cs:134-135`; CHECK `ck_opportunities_lost_fields_required_once_lost`, `OpportunityConfiguration.cs:72-74`).
- **Options:** (a) keep free text in Phase 2, defer taxonomy explicitly to a later phase; (b) introduce a tenant-configurable reason-code table now, keep `LostReason` as an optional supplementary note.
- **Architectural impact:** (b) requires a new tenant-scoped `lost_reasons` (or similar) table + migration + a `LoseOpportunity` command shape change (`reasonCode` + optional free text) before Phase 2 ships.
- **Migration impact:** (b) additive table; no destructive change to existing `lost_reason` column either way.
- **Recommendation:** (a) — minimizes Phase 2 scope creep into what is explicitly a "policy" capability with no frozen shape yet (unlike pipeline stages, no target column list is given for a reason-code table), consistent with the binding spec's non-goal discipline ("do not let Phase 2 become 'finish the entire CRM'").
- **Why implementation must wait:** `LoseOpportunity`'s exact request contract depends on it.

### 2.6 OPEN DECISION — `ReopenOpportunity` inclusion in Phase 2 — RESOLVED: deferred

- **Issue:** The target model freezes the *shape* if built ("Optional policy-controlled command; reason + evidence; creates explicit lifecycle event," PDF §14) but the Implementation Sequence table lists Phase 2 as covering "...Win/Lose/Reopen **(if enabled)**..." (PDF §21) — enablement is explicitly conditional. The Phase 2 binding spec independently lists "reopening Won/Lost opportunities" as an OPEN DECISION example (§21).
- **Verified current state:** no `Reopen` method exists on `Opportunity`; `Win()`/`Lose()` are one-way (`Status is Won or Lost` blocks further `Lose()`/`CancelLine()`/`Win()` calls — `Opportunity.cs:112,132,147`).
- **Recommendation:** defer — Won/Lost stay terminal in Phase 2, consistent with binding spec §10's framing of them as canonical terminal outcomes and with minimizing new domain-invariant surface in a phase already carrying five new commands.
- **Why implementation must wait:** if enabled, `ReopenOpportunity` needs its own authorization action, evidence and outbox event, and a precondition set (which prior states are reopenable, whether pipeline stage is retained or reset) that no binding document currently specifies.

### 2.7 OPEN DECISION — `WinOpportunity`/`CompleteOpportunity` naming and event-contract continuity — RESOLVED: option (a), rename

- **Issue:** The domain rename `Complete()→Win()` (Phase 1, `docs/schema/crm-sales-schema.md` Revision 5 item 17) did not propagate to the application layer: `CompleteOpportunityCommand`, `CompleteOpportunityHandler`, `CompleteOpportunityResult`, `CompletedPayload`, the `Operation` constant `"CompleteOpportunity"`, and the published CloudEvents `eventType` `"enterprise.crmsales.opportunity.completed.v1"` all still say "Complete" (`src/Modules/CRM/Application/CompleteOpportunityHandler.cs:21-23`). This command must be rewritten regardless for Phase 2 (authorization added, pipeline order fixed — see §7 below), which is the natural point to also rename it.
- **Options:** (a) rename throughout to `WinOpportunity*`/`opportunity.won.v1`, treating the old event type as never having had an external consumer (no dispatcher exists yet — §12); (b) keep the legacy names for wire/event compatibility, accept the code-level naming mismatch.
- **Recommendation:** (a) — no outbox dispatcher exists yet (§12), so `enterprise.crmsales.opportunity.completed.v1` has never been consumed by anything outside this database; there is no compatibility cost to renaming now, and carrying a stale name forward only compounds confusion once Phase 5 real event consumers appear.
- **Why implementation must wait:** this is a naming/contract decision, not a blocking architectural one, but it changes which files are edited vs. added in §16/§17 below, so it is called out rather than assumed.

---

## 3. Verified repository state — CRM module

### 3.1 `Opportunity` aggregate (`src/Modules/CRM/Domain/Opportunity.cs`)

Full read, lines 1-165. Lifecycle enum (5-11): `Draft, Open, Won, Lost` — matches the target model's canonical four states exactly, confirmed by the DB `CHECK` (`ck_opportunities_status`, `OpportunityConfiguration.cs:63`).

Fields (20-39): `Id`, `TenantId`, `PartyRefPartyId` (+ `PartyRef` computed property, line 45 — `PartyRef.TenantId` is *derived* from the row's own `TenantId`, not a stored column), `PipelineDefinitionVersionId`/`PipelineStageId` (both nullable, no public setter — "reserved for a later phase's `ChangePipelineStage`" per `crm-sales-schema.md` item 19), `AssignedPrincipalIssuer`/`Subject` (+ `AssignedPrincipal` computed property), `Status`, `LostReason` (nullable string, free text), `Currency`, `EstimatedAmount` (entered, 2dp), `TotalAmount` (nullable, computed, 4dp precision column but rounded to 2dp at `Win()`), `ExpiryDate`, `OpenedDate`, `WonDate`, `LostDate`, `CustomFields` (jsonb string), `RowVersion` (concurrency token, starts at 1), `CreatedAt`/`UpdatedAt`.

Public methods and exact preconditions:
- `Create(tenantId, partyRef, assignedPrincipal, currency, estimatedAmount)` (49-78): validates `partyRef.TenantId == tenantId`, 3-letter currency, non-negative 2dp `estimatedAmount`; sets `Status = Draft`; **does not** set any pipeline field.
- `AddLine(...)` (80-89): only while `Draft`.
- `Open(DateTimeOffset expiryDate)` (93-104): only while `Draft`; `expiryDate` must be future; sets `Open`, `ExpiryDate`, `OpenedDate`. **Does not touch `PipelineStageId`** — a gap against the target model's "OpenOpportunity... assigns initial Open stage" (§2.3 above).
- `Win()` (110-128): only while `Open`; requires ≥1 line that is both non-optional and non-canceled ("billable"); derives `TotalAmount` from those lines' `LineTotal` (or `Quantity*UnitPrice` if null), rounds to 2dp exactly once; sets `Won`, `WonDate`.
- `Lose(string lostReason)` (130-141): rejected only from `Won`/`Lost`; requires non-blank `lostReason`; sets `Lost`, `LostDate`.
- `CancelLine(OpportunityLine, string cancelReason)` (143-152): rejected once `Won`/`Lost`; delegates to `OpportunityLine.Cancel` (internal, only reachable through the aggregate root).
- **No** `Reassign()`, **no** `ChangePipelineStage()`, **no** `Update`/`Edit` method for `EstimatedAmount`, `Currency`, `ExpiryDate`, `CustomFields`, or `PartyRef` post-creation — confirmed by full-file read, not by absence-of-grep-hit alone.
- `Touch()` (157-161) is the single `RowVersion++` point, called from every mutator — the aggregate self-increments; no EF interceptor is used for CRM (the CRM interceptor was deleted, per `crm-sales-schema.md` Revision 4 item 13; `RowVersionInterceptor` is registered only for `AccessDbContext`, `Program.cs:28`).

### 3.2 `OpportunityLine` (`src/Modules/CRM/Domain/OpportunityLine.cs`)

`Quantity`, `UnitPrice` (entered, 2dp), `LineTotal` (computed at `Create`, 4dp, `Line 54`), `IsOptional`, `IsCanceled`, `CancelReason`, `SortOrder`, `ProductRef` (`EntityRef`, no FK — Master Data doesn't own product rows yet). This is confirmed-still-live **priced/monetary** data living inside CRM — the named pilot/legacy compatibility artifact per prior owner decision (§5.B, `feedback-open-decisions` memory: "commercial priced lines = SALES only... today's monetary `OpportunityLine` is a pilot/legacy compatibility artifact — no destructive rewrite before Phase 5"). The target model's Current-State→Target-Delta table independently confirms: "Opportunity carries quote/order-shaped fields → Gradually separate... do not big-bang rewrite without a plan" (PDF §20). **Phase 2 must not touch `OpportunityLine`'s shape or ownership.**

### 3.3 `CustomerNeed` / `OpportunityNeed` — confirmed persisted-but-not-wired

`CustomerNeed.cs` (5-30): `Name`, `AveragePrice` (2dp). `OpportunityNeed.cs` (7-22): pure join (`TenantId, OpportunityId, CustomerNeedId`) with a bare `Create` factory — **no** snapshot value, source, confidence, or confirmation-status fields (the target model's `OpportunityNeed` wants `EstimatedValueSnapshot`, `Source`, `Confidence`, `ConfirmationStatus`, `EvidenceRef` — PDF §6 — none of which exist). `Opportunity.Create(...)` takes `estimatedAmount` as a raw caller-supplied `decimal` (`Opportunity.cs:53`) — it is **not** derived from any selected `CustomerNeed` sum, despite the comment on `OpportunityNeed.cs:6` describing that as the intent ("used to compute estimated_amount from the sum of selected needs' average_price"). `crm-sales-schema.md`'s own "Still open on this schema" list confirms this independently: "`estimated_amount` is still supplied by the caller instead of being summed from the selected `customer_needs`" (line 234). No command anywhere in the repo adds, confirms, or rejects an `OpportunityNeed` (confirmed: no such Application-layer files exist beside the `CompleteOpportunity*` set). This exactly matches the binding spec §11's suspicion — **confirmed dead-in-behavior, not dead code.**

### 3.4 Pipeline model (`PipelineDefinition.cs`, `PipelineDefinitionVersion.cs`, `PipelineStage.cs`)

Versioned, tenant-scoped, purely additive as of Phase 1 (comment, `PipelineDefinition.cs:5-8`: "no command assigns one to an Opportunity yet"). `PipelineStage` (full read, `PipelineStage.cs:5-32`) has **only** `Id, TenantId, PipelineDefinitionVersionId, Name, SortOrder, CreatedAt` — no `IsActive`, no `IsEntry`/`IsDefault`, no stable `code` distinct from the display `Name`, no capability/policy reference. The target model's suggested `pipeline_stages` shape (PDF §13) wants `id, pipeline_version_id, code/stable key, display_name, sort_order, active, capability/policy refs` — **three of five target columns are missing today, a genuine migration gap**, not merely an undecided default. Uniqueness (per `crm-sales-schema.md` ERD, lines 39-40): `UNIQUE(tenant_id, pipeline_definition_version_id, name)` and `UNIQUE(..., sort_order)` — both DB-level, independently verified by negative tests (`PipelineConstraintTests`, per `crm-sales-schema.md` Revision 6 closing paragraph).

`Opportunity.PipelineDefinitionVersionId`/`PipelineStageId` are tenant-safe composite FKs with `DeleteBehavior.Restrict` (`OpportunityConfiguration.cs:41-53`) — a stage/version can never be deleted while referenced, which already satisfies the "historical references must not be silently destroyed" requirement (§9A) structurally, independent of how the retired-stage OPEN DECISION (§2.4) resolves.

### 3.5 Migrations (in order, `src/Modules/CRM/Persistence/Migrations/`)

`InitialCrmSchema` → `FixOpportunityAssignedPrincipalIndex` → `FixCancelExpiryCheck` → `EnableRowLevelSecurity` (hand-written SQL body, the one AGENTS.md-permitted exception) → `RenameOpportunityLifecycle` (Waiting/Offered/Completed/Canceled → Draft/Open/Won/Lost) → `AddPipelineTables` → `BackfillMasterDataParties` → `DropOpportunityPartyForeignKey` → `DropCrmParties` → `RemoveCrmPartyEntity` → `EnableRowLevelSecurityOnPipelineTables` (closes a real gap: `AddPipelineTables` ran after `EnableRowLevelSecurity`'s hardcoded table list, so the three pipeline tables shipped with **no RLS at all** until this fix — `crm-sales-schema.md` Revision 6 item 21). RLS is confirmed (grepped directly): `ENABLE`+`FORCE ROW LEVEL SECURITY` plus a `tenant_isolation` policy using `NULLIF(current_setting('app.tenant_id', true), '')::bigint` on every CRM table including the pipeline tables.

### 3.6 `CompleteOpportunityHandler` — full read, current pipeline order, and required rewrite

`src/Modules/CRM/Application/CompleteOpportunityHandler.cs:31-110`, exact sequence:

1. `BeginTransactionAsync` + `SetTenantContextAsync` (35-36).
2. **Idempotency lookup** on `(TenantId, PrincipalIssuer, PrincipalSubject, Operation="CompleteOpportunity", IdempotencyKey)` (38-48). If found: verify `RequestHash` matches (52-53, else `IdempotencyKeyReusedException`), commit the (no-op) transaction, deserialize and return the stored `CompletedPayload` (55-58).
3. **Only then** load the `Opportunity` (61-64) and call `Win()` (66).
4. Build `payloadJson` once; write it **identically** to `OutboxMessages.Add(...)` (74-84) and `EvidenceRecords.Add(...)` (86-94) — same JSON, no classification distinction between the two channels.
5. Write the `IdempotencyRecord` (96-104).
6. Single `SaveChangesAsync` + commit (106-107).

There is **no authorization call anywhere in this handler** — no `IAuthorizer`/`Access` reference in its `using`s or body. This is a **known, already-acknowledged rewrite target**, not new information: round-3 closure matrix states directly, "Authorization hiç yok... Handler bugünkü haliyle hedef sırayla uyumsuz, Phase 2'de yeniden yazılacak" (`2026-09-17-enterprise-access-foundation-review-round3-final.md:37,100-102`). The target pipeline (round-3 §4, same file lines 78-95) is: Authenticate → ActorContext → Tenant → (future) Entitlement → PEP builds `ResourceRef` → **Authorization** → **Idempotency lookup/replay** → Business policy → Approval → Domain invariants → Mutation → Evidence+Outbox → Commit. Today's handler runs idempotency *before* authorization/resource-load because authorization doesn't exist yet — Phase 2 must invert this order for every new/rewritten command.

`RequestHash` (112-117) covers only `Operation|TenantId|Principal|OpportunityId` — sufficient for a zero-input command like today's `Win`, but **every Phase 2 command with real business inputs (amount, stage, reason, expected version) must extend the hashed canonical string to cover those fields**, or the "same key + different request → reject" requirement (§7) cannot be detected. This is a required change to the existing pattern, not a new mechanism.

`IdempotencyRecord` has no `ExpectedVersion`/aggregate-version column — replay today doesn't reconsider current aggregate state at all (acceptable for a terminal no-further-mutation-inputs command, insufficient in general — see §13 idempotency×concurrency matrix).

### 3.7 Idempotency / Outbox / Evidence infrastructure

CRM owns its own physical copies (`src/Modules/CRM/Idempotency/IdempotencyRecord.cs`, `Outbox/OutboxMessage.cs`, `Evidence/EvidenceRecord.cs`) — **not shared** with Access or MasterData, which each have their own separate physical tables (`AGENTS.md:103`: "Access's own Outbox/Evidence/Idempotency records were added (module-local duplicates, per the 'modules reference Contracts only' rule)"). This is by design (`AGENTS.md` Architecture Rules: "A module project may reference `Contracts` only"). **Phase 2 extends CRM's existing tables/patterns — it must not create a second idempotency mechanism**, consistent with binding spec §7.

- `IdempotencyRecord` (`IdempotencyRecord.cs:10-57`): composite natural key `(TenantId, PrincipalIssuer, PrincipalSubject, Operation, IdempotencyKey)` (`IdempotencyRecordConfiguration.cs:15`), plus `RequestHash`, `ResponseStatus`, `ResponsePayload` (jsonb), `CreatedAt`/`ExpiresAt` (7-day retention constant in the handler, `CompleteOpportunityHandler.cs:25`; index on `ExpiresAt` for a future purge job that doesn't exist yet).
- `OutboxMessage` (`OutboxMessage.cs:8-68`): CloudEvents-shaped (`EventId` unique, `EventType`, `Source`, `Subject`, `CorrelationId`, `CausationId`, `Payload` jsonb, `OccurredAt`, nullable `ProcessedAt` + `MarkProcessed()`).
- `EvidenceRecord` (`EvidenceRecord.cs:9-54`): append-only (DB-enforced — `create-runtime-role.sql` revokes `UPDATE`/`DELETE` on `evidence_records` for `fynovio_app`, lines 19, 31, 52-53), `AggregateType/Id/Version`, `PrincipalIssuer/Subject`, `Action`, `Detail` (jsonb), `CorrelationId`, `OccurredAt`.

### 3.8 Outbox dispatcher — confirmed still missing

Repo-wide search for `MarkProcessed` usage found **only the three method definitions** (CRM, Access, MasterData `OutboxMessage.cs:61`) — **zero call sites**. `src/Worker/Worker.cs` (1-16) is the unmodified `dotnet new worker` template: logs `"Worker running at: {time}"` every second, no outbox read/dispatch logic whatsoever. This confirms the prior finding ("outbox records created but no dispatcher existed") is **still true** as of `main`. Not a Phase 2 hard blocker for exposing the command/API surface itself (nothing currently consumes CRM's outbox), but a real, still-open gap that Phase 2 should surface rather than silently accept as done — see §12.

### 3.9 Host / runtime configuration — RLS-bypass risk confirmed still present

`src/Host/Program.cs` (full read, 51 lines) registers all three `DbContext`s — `CrmDbContext`, `MasterDataDbContext`, `AccessDbContext` (11-28) — plus `IPartyDirectory`, `IPartyIdentityResolver`, `PrincipalResolver`, `IActionCatalog`, `IAuthorizer`, `IAccessScopeResolver` (30-36), and seeds the Access action registry at startup (40-44). This **contradicts** a stale prior-session memory note claiming only `CrmDbContext` was registered — verified current state supersedes it.

However, every module's connection-string default is the **superuser**: `CrmConnectionString.cs`, `AccessConnectionString.cs`, `MasterDataConnectionString.cs` all default to `"Host=localhost;Database=fynovio_platform;Username=postgres;Password=postgres"`, overridable only via `FYNOVIO_{MODULE}_CONNECTION_STRING` environment variables. `src/Host/appsettings.json` and `appsettings.Development.json` carry **no `ConnectionStrings` section at all** (verified by full `cat`). Per AGENTS.md's own binding Database Rule, "superusers and table owners bypass RLS regardless of policy" — so **as shipped, running the Host with no environment variables set connects every module as `postgres`, and every RLS policy this project has built is inert.** `scripts/create-runtime-role.sql` (verified, comprehensive) correctly grants `crm`/`masterdata`/`identity`/`access` schemas to `fynovio_app` and revokes write on `evidence_records`, but nothing wires the Host to prefer it. This is exactly the scenario the binding spec §5 names: *"If this problem still exists in Host/runtime configuration, include its correction in the Phase 2 prerequisite work."* **Confirmed still present — listed as a Phase 2 prerequisite in §11, not fixed by this document (config change is out of scope for a planning-only phase per the STOP condition).**

Also newly observed: `Host.csproj` references `src/Modules/TenantLifecycle` and `src/Modules/Organization`, both of which contain only the default `Class1.cs` scaffold — no domain code. These are placeholders, not implementations; §4A's "Resolve Tenant / Organization Context" step has no dedicated module behind it today (tenant context is set directly from the caller-supplied `TenantId` in existing commands, which is safe only because those commands aren't reachable over HTTP yet — see §2.1).

### 3.10 HTTP API surface — confirmed nonexistent

Repo-wide search found no `ControllerBase` subclass, no `[ApiController]`, no `MapPost`, and no `ProblemDetails` usage anywhere in `src/`. `Program.cs`'s only endpoints are `MapGet("/")` (hello world) and `MapGet("/health/db")` (46-49). **Phase 2 must establish API conventions from nothing** — there is no existing pattern to "follow," only the minimal-API style already present (`app.MapGet`, no controllers) as a weak precedent, and .NET 10's built-in `AddProblemDetails()` middleware as the natural default for the error model (§15) since nothing else is decided.

### 3.11 CRM.Tests coverage (`tests/CRM.Tests/`, file listing)

`Domain/`: `OpportunityMoneyTests`, `OpportunityPipelineFieldsTests` (the regression lock proving `PipelineDefinitionVersionId`/`PipelineStageId` have no public write path yet), `OpportunityRowVersionTests`, `OpportunityStateMachineTests`, `PipelineDefinitionVersionTests`. `Integration/`: `CompleteOpportunityHandlerTests`, `LegacyDataMigrationTests` (+ fixture), `OpportunityConcurrencyTests`, `OpportunityPersistenceTests`, `PartyBackfillVerificationTests`, `PipelineConstraintTests`, `PipelineDefinitionPersistenceTests`, `PipelineRlsTests`, `TenantIsolationTests`, plus `PostgresFixture`/`PostgresCollection`. `Architecture/ModuleBoundaryTests`. 79 tests total per `AGENTS.md:101` (167 across the full solution including `Access.Tests`' 58 and `MasterData.Tests`' 30).

---

## 4. What Phase 0.5 (MasterData) provides to Phase 2

`Opportunity.PartyRef` (strongly-typed `TenantId`+`PartyId`, `src/Contracts/PartyRef.cs`) replacing the dropped `crm.parties` table — confirmed cut over (`DropCrmParties`, `RemoveCrmPartyEntity` migrations; `crm-sales-schema.md` Revision 5 item 18). `IPartyDirectory`/`IPartyIdentityResolver` are registered in Host (`Program.cs:30-31`) for resolving/validating party references. Phase 2 commands that accept a `PartyRef` (only `CreateOpportunity` does) should validate it through these, not re-implement party lookup. No Phase 2 command needs to create or mutate a `Party` — Opportunity only *references* one.

## 5. What Phase 1 (Lifecycle/Pipeline) provides to Phase 2

The canonical `Draft/Open/Won/Lost` lifecycle, the CRM-owned versioned pipeline schema (`pipeline_definitions/_versions/_stages`), and RLS across all of it. Deliberately incomplete by design, explicitly deferred to Phase 2: pipeline-stage enforcement on Opportunity transitions, `ChangePipelineStage` itself, and any command writing `Opportunity.PipelineDefinitionVersionId`/`PipelineStageId` (`crm-sales-schema.md` Revision 5 item 19: "not yet used by any command, reserved for a later phase's `ChangePipelineStage`"). No behavioral rework of Phase 1 is required or proposed here — Phase 2 only adds to it.

## 6. What Phase 1.5 (Access) provides to Phase 2

Covered exhaustively in §1's capability table. Summary: coarse per-`ActionKey` Allow/Deny, tenant-wide and `OwnedBy`-scoped grants, a query-scope resolver with a proven equivalence contract, and an action registry — all consumable as-is, with the owner-field mapping (§2.2) as the one piece that must be resolved before `OwnedBy` can actually be wired into an Opportunity command's PEP.

---

## 6A. Current vs. target lifecycle mapping

| | Current (verified) | Target (binding spec §1; target-model PDF §4) | Gap |
|---|---|---|---|
| States | `Draft, Open, Won, Lost` (`Opportunity.cs:5-11`) | `Draft, Open, Won, Lost` | **None — already aligned**, including the DB `CHECK` |
| `Draft → Open` | `Open(expiryDate)` — sets `Status`, `ExpiryDate`, `OpenedDate` only | Same, plus "assigns initial Open stage" (target model) | Stage assignment missing — §2.3 |
| `Open → Won` | `Win()` — requires ≥1 billable line, computes `TotalAmount` | Matches | None |
| `Open → Lost` | `Lose(reason)` — free text | Matches; target model wants a structured reason "by tenant policy" | Taxonomy timing — §2.5 |
| `Draft → Lost` | `Lose(reason)` also allowed from `Draft` (guard is `Status is not (Won or Lost)`) | Target model's diagram shows an abandoned-Draft path as legitimate | Already consistent, not a gap — worth an explicit product confirmation only |
| `Won`/`Lost → Open` (Reopen) | Not implemented — both states are terminal | Optional, "(if enabled)" per target model's own phase table | Recommend deferring past Phase 2 (§2.6) |
| Pipeline stage on the aggregate | `PipelineStageId`/`PipelineDefinitionVersionId` exist as nullable FKs, never written by any command | Assigned on `Open`, changed via `ChangePipelineStage` | The whole of §2.3/§2.4/§7's `ChangePipelineStage` row |

No lifecycle state or transition needs to be renamed or restructured — the gap is entirely in *pipeline-stage wiring* and *optional structure* (reopen, structured loss reason), not in the four-state model itself.

---

## 7. Required commands (reconciled: current code × binding spec examples × target model §14)

| Command | Current domain support | Application layer today | Phase 2 scope |
|---|---|---|---|
| `CreateOpportunity` | `Opportunity.Create(...)` exists (`Opportunity.cs:49-78`) | **No handler exists** | New command + handler. Selects pipeline version (not stage, per §2.3); optional `PartyRef` validation via `IPartyDirectory` |
| `OpenOpportunity` | `Opportunity.Open(expiryDate)` exists (93-104), does **not** assign a stage | **No handler exists** | New command + handler; must additionally assign the entry stage once §2.3 is resolved — requires a domain-method change, not just a wrapper |
| `ChangePipelineStage` | **No domain method at all** | **No handler exists** | New domain method + command + handler; depends on §2.3 (entry stage) and §2.4 (retired-stage `IsActive`) migrations landing first |
| `WinOpportunity` (rename of `CompleteOpportunity`, §2.7) | `Opportunity.Win()` exists (110-128) | Exists but must be **rewritten**, not merely renamed — see §3.6 | Rewrite: add authorization, reorder pipeline (auth before idempotency), extend `RequestHash` coverage, accept `expectedVersion` |
| `LoseOpportunity` | `Opportunity.Lose(reason)` exists (130-141) | **No handler exists** | New command + handler; reason shape depends on §2.5 |
| `ReassignOpportunity` | **No domain method** | **No handler exists** | New domain method (`Reassign`, pending §2.2) + command + handler; this is the command that makes `OwnedBy` meaningful for future reassignment scenarios |
| `ReopenOpportunity` | **No domain method** | N/A | **Deferred** pending §2.6 |
| `Add/Confirm/RejectOpportunityNeed` | `OpportunityNeed.Create` exists but unused (§3.3) | **No handler exists** | **Out of Phase 2 scope** — target model's own Implementation Sequence assigns this to Phase 3 ("Customer Need capability"). Phase 2's only obligation is to not break these tables and to note the integration point (§10) |
| `CreateQuote`/`SendQuote`/`RecordQuoteInteraction` | N/A — Sales-owned | N/A | **Non-goal**, confirmed by binding spec §19 and target model §12 domain-ownership table |

For every in-scope command, the required precondition/authorization/evidence/idempotency/concurrency/error dimensions (binding spec §3) are specified in §8–§14 below rather than repeated five times here.

---

## 8. Opportunity update/mutability model and bounded-context field ownership

No generic `UpdateOpportunity`/PATCH. Per-field classification:

| Field | Classification | Bounded-context owner | Notes |
|---|---|---|---|
| `Status` (lifecycle) | Editable only via dedicated commands (`Open`/`Win`/`Lose`) | CRM | Never directly settable |
| `PipelineStageId`/`PipelineDefinitionVersionId` | Editable only via `ChangePipelineStage`/`OpenOpportunity` | CRM | Never directly settable |
| `AssignedPrincipal` | Editable only via `Reassign` (pending §2.2) | CRM references Identity/Access-owned `PrincipalRef`, no FK | Not a CRM-owned identity — CRM only stores the reference |
| `EstimatedAmount` | **Deferred — no dedicated command in Phase 2** | CRM | Target model eventually wants this split into `ForecastAmount` (commercial estimate) vs. a derived `NeedPotentialAmount` (PDF §13's suggested `opportunities` shape: `forecast_amount?, need_potential_amount?`) — that split is Phase 3's job ("Customer Need capability... potential calculation," PDF §21). Phase 2 does not rename or split this field; it stays immutable post-`Create` as today, since no command needs to change it yet and inventing one would pre-empt the Phase 3 split |
| `Currency`, `ExpiryDate` (post-`Open`), `CustomFields` | **Immutable in Phase 2 — deferred** | CRM | No binding requirement forces these to be editable now; binding spec §11A requires classifying, not requiring, every field's editability |
| `PartyRef` | **Immutable in Phase 2 — deferred** | MasterData-owned reference; CRM stores only the ref | Re-parenting an Opportunity to a different Party has no binding decision and is out of scope |
| `Lines` (`OpportunityLine`) | Editable only while `Draft` via `AddLine`/`CancelLine` (already exists) | CRM (pilot/legacy artifact, §3.2) | No change proposed |
| `LostReason` | Set only via `LoseOpportunity` | CRM | Shape pending §2.5 |
| `CustomerNeed`/`OpportunityNeed` links | **Out of Phase 2 scope** | CRM (owns the join), but the *capability* (need-selection UX, snapshotting) is Phase 3 | See §10 |
| `RowVersion` | Never directly settable; read-only concurrency token exposed to callers as `expectedVersion` | CRM | See §13 |

This satisfies binding spec §11A's requirement to classify every mutable field without inventing a generic PATCH: the only fields that become writable in Phase 2 are the ones with a dedicated command (`AssignedPrincipal` via `Reassign`, `PipelineStageId` via `ChangePipelineStage`, `Status`-adjacent fields via `Open`/`Win`/`Lose`). Everything else stays immutable, deferred, and explicitly named as deferred rather than silently unaddressed.

---

## 9. Required queries (Phase 2 minimum) and available-actions strategy

**Required now:**
- `GetOpportunity(tenantId, id)` — tenant-safe load, `crm.opportunity.read` (or reuse `.view`) coarse authorization, `OwnedBy` scope check if the caller's grant is owner-scoped rather than tenant-wide.
- `ListOpportunities(tenantId, filters, paging)` — filtered by status/stage/owner; the WHERE-clause scope filter comes from `IAccessScopeResolver.ResolveAsync(...)` translated into a SQL predicate (never fetch-then-post-filter, per `AccessScope`'s own doc comment).
- `GetPipelineStages(tenantId, pipelineDefinitionId, version?)` — needed to power `ChangePipelineStage` clients and (once §2.3 lands) the entry-stage the client can display; read-only, no FLS applicable (no sensitive fields on `PipelineStage`).

**Available-actions projection (§13A):** feasible now, but necessarily **plain-boolean-only** — `canEdit`/`canOpen`/`canChangeStage`/`allowedTargetStages`/`canWin`/`canLose`/`canReassign`, each computed by calling `IAuthorizer.AuthorizeAsync` for the corresponding `ActionKey` plus the relevant lifecycle/pipeline-state guard from the aggregate's own precondition logic (mirroring the guard clauses in `Opportunity.cs`, not reimplementing them — the projection calls into the same domain checks used by the command handlers wherever possible, e.g. "can `Win`" = `Status == Open && billable line count > 0 && Authorize(win) == Allow`). This is *not* a violation of "do not collapse richer policy outcomes into misleading booleans" (§13A) — Phase 1.5 has no richer outcome (no obligation/approval/masking) to collapse (§1). If Phase 1.5 ever gains one, this projection's shape must change to carry it, at that time.

**Deferred (reporting/analytics):** any aggregate/dashboard/forecast query — explicit non-goal (§19: "analytics platform," "forecasting engine").

---

## 9A. Proposed HTTP API surface

No existing convention to follow (§3.10) — minimal-API style (matching the one precedent in `Program.cs`, `app.MapGet`), not controllers. Two conventions are named explicitly here because §8/§3.10 of the binding spec require a stated choice, not a default:

- **Concurrency contract:** `expectedVersion` as an explicit field in the request body (not `If-Match`/ETag). Reason: the existing concurrency token is a domain-level `RowVersion` (`long`) already surfaced in every response DTO, not an HTTP-cache-shaped opaque tag, and every other project convention (idempotency key, correlation id) is already body/header-based rather than ETag-based — introducing ETag semantics for concurrency alone would be a second, inconsistent convention.
- **Idempotency:** caller-supplied key via an `Idempotency-Key` request header (not a body field), matching how `IdempotencyKey` is already a distinct parameter from the domain payload in `CompleteOpportunityCommand` (`CompleteOpportunityCommand.cs`).
- **404 vs 403:** a record that exists but is outside the caller's resolved access scope (§14 step 6) returns the same `404` as a record that doesn't exist at all, per §15's tenant-non-leak rule — never `403` for "exists, not yours."

| Method + route | Request contract | Response contract | Authorization (`ActionKey`) | Idempotency | Concurrency | Status codes |
|---|---|---|---|---|---|---|
| `POST /opportunities` | `partyRef, assignedPrincipal, currency, estimatedAmount` + `Idempotency-Key` header | `{id, status, rowVersion}` | `crm.opportunity.create` | Required. `ResourceDescriptor.Id = null` (CREATE-action shape, round-3 freeze #22) | N/A (no prior version) | 201, 400 (validation), 403, 409 (idempotency-key-reused) |
| `POST /opportunities/{id}/open` | `expiryDate, expectedVersion` + `Idempotency-Key` | `{id, status, openedDate, pipelineStageId, rowVersion}` | `crm.opportunity.open` | Required | `expectedVersion` | 200, 400, 403, 404, 409 (concurrency or illegal-transition) |
| `POST /opportunities/{id}/stage` | `targetStageId, expectedVersion` + `Idempotency-Key` | `{id, pipelineStageId, rowVersion}` | `crm.opportunity.change_stage` | Required | `expectedVersion` | 200, 400, 403, 404, 409 (invalid-pipeline-transition or concurrency) |
| `POST /opportunities/{id}/win` | `expectedVersion` + `Idempotency-Key` | `{id, totalAmount, currency, wonDate, rowVersion}` | `crm.opportunity.win` | Required (existing pattern, extended per §13) | `expectedVersion` | 200, 403, 404, 409 |
| `POST /opportunities/{id}/lose` | `lostReason, expectedVersion` + `Idempotency-Key` | `{id, lostDate, rowVersion}` | `crm.opportunity.lose` | Required | `expectedVersion` | 200, 400, 403, 404, 409 |
| `POST /opportunities/{id}/reassign` (pending §2.2) | `newAssignedPrincipal, expectedVersion` + `Idempotency-Key` | `{id, assignedPrincipal, rowVersion}` | `crm.opportunity.reassign` | Required | `expectedVersion` | 200, 400, 403, 404, 409 |
| `GET /opportunities/{id}` | — | Full DTO (no FLS-driven omission — not applicable, §1) | `crm.opportunity.read` | N/A | N/A | 200, 403, 404 |
| `GET /opportunities` | Query-string filters (`status`, `pipelineStageId`, `assignedPrincipal`), paging | Paged DTO list, scope-filtered per `IAccessScopeResolver` | `crm.opportunity.list` (or reuse `.read` — naming is an implementation detail, not an open decision) | N/A | N/A | 200, 403 |
| `GET /opportunities/{id}/actions` | — | `{canOpen, canChangeStage, allowedTargetStageIds, canWin, canLose, canReassign}` (plain booleans, §9) | Same as `.read` — this endpoint doesn't gate on its own action | N/A | N/A | 200, 403, 404 |
| `GET /pipelines/{pipelineDefinitionId}/stages?version=` | — | Stage list for the resolved (or specified) version | `crm.opportunity.read` (pipeline config is read through the CRM lens here, not a separate capability) | N/A | N/A | 200, 403, 404 |

`CreateOpportunity` is the one command with `ResourceDescriptor.Id = null` — per `ResourceDescriptor`'s own doc comment ("`Id` is null for CREATE actions," round-3 §11) — so its authorization check evaluates only the type-level `crm.opportunity.create` capability plus tenant context, never an `OwnedBy` fact (there is no owner yet).

---

## 10. CustomerNeed impact assessment

Preserve as-is; do not delete (`CustomerNeed`, `OpportunityNeed` stay in the schema and domain, §3.3). Do not implement AI inference, confidence scoring, or provenance tracking (non-goal §19: "AI voice processing," "AI need inference"). **Phase 2's only integration point:** `CreateOpportunity`'s request contract should be designed so that adding an optional "selected needs" input later (Phase 3) doesn't require breaking the command shape — concretely, keep `estimatedAmount` as an explicit, caller-supplied, top-level field in Phase 2 (as it is today) rather than trying to pre-emptively wire in `OpportunityNeed` summation, since that summation logic, the value-snapshot fields, and the source/confidence/confirmation model are all explicitly Phase 3 (`Customer Need capability`, target model §21). No schema change to `customer_needs`/`opportunity_needs` is proposed in Phase 2.

---

## 11. Persistence / migration impact (Phase 2)

New migrations required (exact shape depends on §2 resolutions, but the columns are name-stable regardless of which option is chosen where a choice exists):

1. `pipeline_stages`: add `is_active boolean NOT NULL DEFAULT true` (§2.4) and `is_entry boolean NOT NULL DEFAULT false` + a partial unique index enforcing exactly one entry stage per `pipeline_definition_version_id` (§2.3). Both additive, no data migration needed (existing rows default to inactive-entry-unset, which is safe since no command reads these columns until this phase ships).
2. If §2.2 resolves to option (b) (separate `OwnerPrincipalRef`): one additive nullable column pair (`owner_principal_issuer`, `owner_principal_subject`) on `opportunities`. If (a) (recommended): **no migration** — `Reassign()` reuses `assigned_principal_issuer/subject`.
3. If §2.5 resolves to a structured reason-code table (not recommended): a new tenant-scoped `lost_reasons`-shaped table + migration. Under the recommended option (keep free text), **no migration**.
4. `IdempotencyRecord`/`OutboxMessage`/`EvidenceRecord`: no schema change — Phase 2 commands reuse the existing CRM-local tables (§3.7); only the request-hash *input* and handler *call sites* change, not the table shape.
5. **Not a Phase 2 schema change, but a required prerequisite:** `Host` connection-string wiring (appsettings/env-var documentation) to default to `fynovio_app` rather than `postgres` (§3.9). This is configuration, not an EF migration, but it is listed here because it gates whether any of the above migrations' RLS protection is real once deployed.

Every new migration must follow the same hand-written-SQL-only-for-RLS convention (AGENTS.md's named exception) and get its own `EnableRowLevelSecurity`-style follow-up **only if** a genuinely new tenant-scoped table is introduced (options (b) under §2.2/§2.5) — the `pipeline_stages` column additions in item 1 don't need a new RLS migration since the table is already RLS-enabled.

---

## 12. Legacy compatibility / migration impact

No destructive changes proposed to `OpportunityLine`, `CustomerNeed`, or `OpportunityNeed` shapes (§3.2, §3.3, §10). The `CompleteOpportunity → WinOpportunity` rename (§2.7) has zero external-consumer compatibility cost today because **no outbox dispatcher exists** (§3.8) — nothing outside this database has ever consumed `enterprise.crmsales.opportunity.completed.v1`. If the owner instead wants the legacy event name preserved for some reason not visible in this repo, that is a one-line reversal of the §2.7 recommendation, not a structural change to this plan. The outbox-dispatcher gap itself is **not** a hard Phase 2 blocker (Phase 2's command/API surface doesn't require anything to consume the outbox), but it should not be silently left unaddressed either — recommend a follow-on, non-blocking task: a `Worker` `BackgroundService` polling `WHERE processed_at IS NULL ORDER BY id`, at-least-once delivery, calling `MarkProcessed()` after successful hand-off. Not designed further here since implementing it is out of this planning phase's scope.

---

## 12A. Event / outbox catalog (Phase 2 events)

Extending the exact template already proven by `CompleteOpportunityHandler` — `source: "/enterprise/crm-sales"`, `subject: "opportunities/{id}"`, `aggregate_type: "Opportunity"`, `aggregate_version` = the aggregate's `RowVersion` **after** the mutation (this is why `Touch()` increments before the outbox row is built inside the same method, `crm-sales-schema.md` Revision 4 item 13) — every event below is a domain event promoted to an integration event via the same outbox row (this codebase does not distinguish the two categories with separate mechanisms; the outbox row *is* the integration event, per AGENTS.md's "no direct message publication inside the business transaction").

| Event type (`v1`) | Emitted by | Payload | Correlation / causation |
|---|---|---|---|
| `enterprise.crmsales.opportunity.created.v1` | `CreateOpportunity` | `opportunityId, partyRef, assignedPrincipal, currency, estimatedAmount` | `correlationId` = caller-supplied; `causationId` = null (root of the chain) |
| `enterprise.crmsales.opportunity.opened.v1` | `OpenOpportunity` | `opportunityId, expiryDate, openedDate, pipelineDefinitionVersionId, pipelineStageId` (the assigned entry stage, §2.3) | `causationId` = null (a new caller-initiated command, not a reaction to another event) |
| `enterprise.crmsales.opportunity.stage_changed.v1` | `ChangePipelineStage` | `opportunityId, fromStageId, toStageId` | `causationId` = null |
| `enterprise.crmsales.opportunity.won.v1` (rename of `.completed.v1`, §2.7) | `WinOpportunity` | `opportunityId, totalAmount, currency` (unchanged payload shape from today's `CompletedPayload`) | `causationId` = null |
| `enterprise.crmsales.opportunity.lost.v1` | `LoseOpportunity` | `opportunityId, lostReason` | `causationId` = null |
| `enterprise.crmsales.opportunity.reassigned.v1` (pending §2.2) | `ReassignOpportunity` | `opportunityId, previousPrincipal, newPrincipal` | `causationId` = null |

**Deliberately not emitted in Phase 2:** no event for `GetOpportunity`/`ListOpportunities`/`GetOpportunityAvailableActions` (queries never emit events); no `OpportunityNeedSuggested/Confirmed/Rejected` events (Phase 3 — CustomerNeed commands are out of scope, §7); no `OpportunityReopened` event (§2.6, deferred). Every event above shares the schema `{event_id (unique), event_type, source, subject, correlation_id, causation_id, aggregate_type, aggregate_id, aggregate_version, payload, occurred_at}` already defined by `OutboxMessage.Create(...)` (`OutboxMessage.cs:27-59`) — no schema change needed to the outbox table itself, only new call sites.

---

## 13. Idempotency × concurrency behavior matrix

Building directly on the existing mechanism (§3.6, §3.7) — no new table, no new locking primitive, per binding spec §7's explicit prohibition.

| Scenario | Required behavior | How the existing infra achieves it | Gap to close |
|---|---|---|---|
| A) First request, `expectedVersion=N`, key=K | Executes; aggregate becomes `N+1`; response stored | `RowVersion` is `IsConcurrencyToken()` (`OpportunityConfiguration.cs:34`); handler loads at `N`, mutates, `SaveChanges` writes `WHERE row_version = N` | Command handlers must accept and pass through `expectedVersion` — today only `Win` exists and takes none |
| B) Exact retry: same `expectedVersion`, same key K, identical request | Return stored result; no re-execution, no version bump, no duplicate Evidence/Outbox | `IdempotencyRecord` lookup by `(Tenant, Principal, Operation, Key)` + `RequestHash` match (`CompleteOpportunityHandler.cs:40-58`) already does exactly this | `RequestHash`'s canonical string must include every business field (amount, stage, reason, `expectedVersion`) for non-trivial commands, not just `TenantId+Principal+OpportunityId` (§3.6) |
| C) Same key K, different request | Reject as idempotency-key-reuse conflict | `IdempotencyKeyReusedException` thrown today when `RequestHash` mismatches (`CompleteOpportunityHandler.cs:52-53`) | None — mechanism already correct once (B)'s hash-coverage gap is closed |
| D) New key, stale `expectedVersion` | Concurrency conflict; no mutation; no evidence/outbox | EF's optimistic concurrency check on `SaveChanges` throws `DbUpdateConcurrencyException` when the `WHERE row_version=N` matches zero rows — this is standard EF behavior, already exercised by `OpportunityConcurrencyTests` | Handler must catch `DbUpdateConcurrencyException` and translate to the API's concurrency-conflict error shape (§15) — not implemented in any handler today since only `Win` exists and it doesn't take `expectedVersion` |
| Concurrent duplicates: two requests, same key K, both in-flight | Defined, not ad hoc | Composite PK on `IdempotencyRecords` (`Tenant, Issuer, Subject, Operation, Key`) already guarantees only one insert commits | **Currently undefined for the loser**: the losing transaction's `SaveChanges` throws a raw unique-violation `DbUpdateException`, surfaced uncaught to the caller (`crm-sales-schema.md` "Still open" list, confirmed). **Required fix:** catch the unique-violation, re-query `IdempotencyRecords` for the now-committed winner's row, and replay it — this reuses (B)'s existing replay path rather than inventing a new mechanism, satisfying §7A's "do not invent ad-hoc in-memory locking" |

Transaction ordering (§7A "must remain atomic"): unchanged from today's proven pattern — domain state, idempotency record, evidence, and outbox all write in the **same** `SaveChangesAsync()` call inside the same transaction (`CompleteOpportunityHandler.cs:74-107`); Phase 2 handlers keep this shape, only moving the authorization/scope check earlier in the method (before the idempotency lookup, per round-3 §4's target order — see §3.6).

---

## 14. Authorization evaluation sequence mapped to actual Phase 1.5 components

For every Phase 2 mutation, in order (mapping binding spec §4A/§6's conceptual steps onto real types):

1. **Authenticate** → *unresolved, §2.1 BLOCKER*.
2. **Build `ActorContext`** (`TenantId`, `PrincipalRef`, `CorrelationId`) → depends on (1).
3. **Resolve tenant/organization context** → today, directly from the authenticated principal's tenant membership (no `TenantLifecycle`/`Organization` module exists to add anything beyond this — §3.9).
4. **Coarse capability/action authorization** → `IAuthorizer.AuthorizeAsync(new AuthorizationRequest(actor, new ActionKey("crm.opportunity.<verb>"), resource))`. New `ActionKey`s needed: `crm.opportunity.create`, `.open`, `.change_stage`, `.win`, `.lose`, `.reassign`, `.read`/`.list` — each must be registered in the action catalog (mirroring `AccessActionCatalogSeeder`'s existing pattern) or `AccessAuthorizer` denies with `action_not_registered` (`AccessAuthorizer.cs:21-22`).
5. **Tenant-safe aggregate load** → existing pattern (`_context.Opportunities.SingleOrDefaultAsync(...)`, RLS active on the connection) — unchanged.
6. **Record-level policy (`OwnedBy`)** → only meaningful once §2.2 resolves; until then, tenant-wide grants are the only usable grant shape for Opportunity actions.
7. **Field-level WRITE authorization** → **not applicable** — frozen out (§1). Every Phase 2 command's field set is instead constrained by dedicated-command shape (§8), which is the substitute Phase 2 relies on in FLS's absence.
8. **Policy obligations/approval** → **not applicable** — frozen out (§1). No command may be gated on an approval outcome that doesn't exist; `WinOpportunity` is a direct authorize-and-execute command, consistent with the prior owner decision that mandatory approval is permanently deferred (`feedback-open-decisions` memory, §5.A).
9. **Validate `expectedVersion`** → EF optimistic concurrency (§13).
10. **Domain command** → the aggregate method.
11. **Single transaction**: state + evidence + outbox + idempotency (§13's ordering, unchanged pattern).

Risk-relevant commands requiring Evidence (§16 of the binding spec, applying AGENTS.md's "Evidence required only for risk-catalogued commands" trigger rule): `WinOpportunity` (money-carrying, already evidenced today), `LoseOpportunity` (terminal, money-adjacent), `ReassignOpportunity` (authorization-relevant ownership change), `ChangePipelineStage` (lower risk — evidence optional, recommend including it anyway since the pattern is already "for free" per the existing one-`SaveChanges` shape). `CreateOpportunity`/`OpenOpportunity`/queries: evidence not required by the trigger rule, may still get outbox events for integration purposes (§17).

---

## 15. Consistent error model

No existing convention (§3.10) — proposed, using ASP.NET Core's built-in `ProblemDetails` (`AddProblemDetails()`), since nothing else is decided and it is the .NET 10 default:

| Condition | Status | `type`/`title` shape |
|---|---|---|
| Validation error (bad request body, invalid `PartyRef`, etc.) | 400 | `validation_error` |
| Not authenticated | 401 | generic — pending §2.1 |
| Coarse action denied | 403 | `forbidden` — no detail leaking which specific grant was checked |
| Record-level denied (tenant-safe load found nothing, or found something outside scope) | 404 | **same** 404 for "doesn't exist" and "exists but out of scope" — per binding spec §12 "do not leak tenant data through differences in error responses" |
| Illegal lifecycle transition (e.g. `Win` on a `Draft`) | 409 | `illegal_lifecycle_transition` |
| Invalid pipeline transition (target stage inactive/wrong version) | 409 | `invalid_pipeline_transition` |
| Optimistic concurrency conflict | 409 | `concurrency_conflict` |
| Idempotency-key reuse (different request, same key) | 409 | `idempotency_key_reused` |
| Idempotent exact replay | 200/201 (whatever the original succeeded with), `Replayed: true` marker in the body, no new side effects |

Approval-required/field-restriction rows from the binding spec's error taxonomy (§17) are intentionally **omitted** — nothing produces those outcomes today (§1).

---

## 16. Test plan

**Domain** (extend `tests/CRM.Tests/Domain/`): `ChangePipelineStage` valid/invalid transitions once the method exists; entry-stage assignment on `Open()` once §2.3 lands; retired-stage rejection once §2.4 lands; `Reassign()` guard clauses once §2.2 resolves; confirm `Win`/`Lose` remain unreachable from `Won`/`Lost` (already covered, regression-lock only).

**Application** (new `tests/CRM.Tests/Application/`, mirroring `Access.Tests`' handler-test pattern): per-command coarse-authorization allow/deny; `OwnedBy` scope allow/deny once §2.2 resolves; idempotency exact-replay, key-reuse-reject, and the concurrent-duplicate-replay fix (§13); concurrency-conflict translation; evidence/outbox written exactly once per successful command.

**Integration** (extend `tests/CRM.Tests/Integration/`, Testcontainers + `fynovio_app`, per AGENTS.md's binding testing rule): RLS still isolates the two new/changed tables (`pipeline_stages`'s new columns don't need a new RLS test, the table already has one); tenant-safe FK/uniqueness for any new column; a legacy-migration-upgrade test class analogous to `LegacyDataMigrationTests` if any of §2's OPEN DECISIONS require a data migration (only true for the non-recommended options).

**API** (new, once §2.1 resolves): happy path per endpoint; validation errors; 401/403/404 distinctions; concurrency conflict; idempotent retry; tenant isolation over HTTP (a cross-tenant token must never see another tenant's Opportunity, mirroring the domain-layer `TenantIsolationTests` pattern at the HTTP boundary).

Do not replace PostgreSQL integration tests with mocks — continue Testcontainers (AGENTS.md, binding).

---

## 17. Exact files expected to change

- `src/Modules/CRM/Domain/Opportunity.cs` — add `ChangePipelineStage(...)`, `Reassign(...)` (pending §2.2), extend `Open(...)` to assign the entry stage (pending §2.3).
- `src/Modules/CRM/Domain/PipelineStage.cs` + `Persistence/Configurations/PipelineStageConfiguration.cs` — add `IsActive`, `IsEntry` (§2.3, §2.4).
- `src/Modules/CRM/Application/CompleteOpportunityCommand.cs`, `CompleteOpportunityHandler.cs`, `CompleteOpportunityResult.cs`, `CompletedPayload.cs` — rewritten and renamed to `WinOpportunity*` (§2.7): add authorization, reorder pipeline, extend request-hash coverage, accept `expectedVersion`.
- `src/Host/Program.cs` — register new endpoints, `AddProblemDetails()`, authentication middleware (pending §2.1).
- `src/Host/appsettings.json`, `appsettings.Development.json` — add `ConnectionStrings` pointing at `fynovio_app`-based DSNs (§3.9 prerequisite).
- `docs/schema/crm-sales-schema.md` — new revision entry documenting the pipeline-stage columns and any §2-driven schema change.
- `AGENTS.md` — status section update once Phase 2 executes (not part of this planning document).

## 18. Exact files expected to be added

- `src/Modules/CRM/Application/CreateOpportunityCommand.cs` + `CreateOpportunityHandler.cs` (+ result type)
- `src/Modules/CRM/Application/OpenOpportunityCommand.cs` + `OpenOpportunityHandler.cs`
- `src/Modules/CRM/Application/ChangePipelineStageCommand.cs` + `ChangePipelineStageHandler.cs`
- `src/Modules/CRM/Application/LoseOpportunityCommand.cs` + `LoseOpportunityHandler.cs`
- `src/Modules/CRM/Application/ReassignOpportunityCommand.cs` + `ReassignOpportunityHandler.cs` (pending §2.2)
- `src/Modules/CRM/Application/GetOpportunityQuery.cs` + handler + response DTO
- `src/Modules/CRM/Application/ListOpportunitiesQuery.cs` + handler + response DTO
- `src/Modules/CRM/Application/GetOpportunityAvailableActionsQuery.cs` + handler + response DTO
- `src/Modules/CRM/Application/GetPipelineStagesQuery.cs` + handler + response DTO
- New exception types as needed (e.g. `OpportunityConcurrencyConflictException`, `IllegalLifecycleTransitionException`) alongside the existing `OpportunityNotFoundException`/`IdempotencyKeyReusedException`
- `src/Modules/CRM/Persistence/Migrations/<ts>_AddPipelineStageActiveAndEntryFlags.cs`
- Any additional migration from §2.2(b)/§2.5(b) if those non-recommended options are chosen
- `src/Host/Endpoints/OpportunityEndpoints.cs` (or equivalent minimal-API grouping) — new HTTP surface
- `tests/CRM.Tests/Application/*` — new handler test files, one per command
- `tests/CRM.Tests/Integration/OpportunityAuthorizationTests.cs` (or similar) — coarse/`OwnedBy` authorization integration coverage
- This document itself: `docs/architecture-analysis/PHASE_2_OPPORTUNITY_COMMANDS_API_PLAN.md`

---

## 19. Explicit non-goals (unchanged from binding spec §19, confirmed none contradicted by repository state)

Quote, QuoteVersion, QuoteLine, Sales Order, Invoice, refund, payment, full Activity Timeline, Calls/Meetings/Email sync, Feedback surveys, KVKK/Consent module, AI voice/need inference, RAG/LangGraph/recommendation agents, analytics/forecasting platforms, redesigning Phase 1.5's authorization/policy/approval engine, inventing new field-security semantics outside Phase 1.5, rebuilding the Phase 1 pipeline foundation. Additionally, per this plan's own findings: no `OpportunityLine`/`CustomerNeed` schema rewrite (§3.2, §3.3), no outbox dispatcher implementation (§12 — recommended as a follow-on, not built here), no `ReopenOpportunity` (§2.6, deferred), no structured `LostReason` taxonomy (§2.5, deferred).

---

## 20. Risks, blockers, and reference-completeness check

**Blockers as of drafting — both RESOLVED 2026-09-18 (see §2):**
- §2.1 Authentication — **RESOLVED: JWT bearer.**
- §2.2 Owner-field mapping — **RESOLVED: option (a), `Reassign()`.**
- §3.9 Host connection-string default — **not an owner decision, an execution task.** RLS is inert as shipped; fixing it is step 1 of §21's sequence, not something requiring further approval.

**Non-blocking but load-bearing risks:**
- §2.3/§2.4 pipeline-stage columns — must land before `ChangePipelineStage`/`OpenOpportunity`'s stage-assignment logic can be written; low risk (purely additive migration).
- §2.5 LostReason timing, §2.6 Reopen inclusion, §2.7 naming — product/scope decisions with low architectural risk either way, but must be explicit before the affected command's exact contract is written.
- §12 outbox dispatcher absence — doesn't block Phase 2's surface, but any claim that Phase 2 events are "delivered" would be false until this exists.
- §13 concurrent-duplicate-idempotency behavior — currently surfaces a raw `DbUpdateException`; must be fixed as part of every new command's handler, not deferred, since it's a correctness gap on the exact mechanism Phase 2 depends on.

**Reference-location correction:** an earlier pass of this document incorrectly stated that the numbered architecture-decision docs (04, 07, 08, 12, 14, 17, 19, 20 — "Canonical Concepts," "Target Reference Architecture," "Module Boundaries," "Fitness Functions," "Executive Summary," "CRM+Sales Pilot Domain," "Identity/Access & Dealer Network," "Pilot Enforcement Scope") were absent from this machine. They are **not** missing — they exist at `~/Projects/fynovio/enterprise ve B2B mimari araştırma/docs/architecture-analysis/`, a separate, non-git directory sitting alongside this repository (confirmed present, directory listing checked directly). Two of the most load-bearing were spot-verified directly against this plan's claims: doc 04 line 25's `IdempotencyKey` primitive definition — *"Tenant + caller + operation + key, bounded documented retention... Same key binds same normalized input hash"* — matches `IdempotencyRecord`'s actual composite key and `RequestHash` mechanism exactly (§3.7 above), and doc 17 §2 ("Pilot aggregate: state machine") confirms the CRM+Sales pilot-merger boundary cited in §3.2. No conclusion in this plan rests on an actually-unverifiable claim; no BLOCKER applies here.

---

## 21. Recommended implementation sequence

1. Resolve §2.1–§2.7 (owner decisions) — nothing below should start coding before at least §2.1, §2.2, §2.3, §2.4 land, since they change method signatures and migrations that everything else builds on.
2. Fix Host connection-string wiring to default to `fynovio_app` (§3.9) — infrastructure prerequisite, independent of the OPEN DECISIONS, should happen first regardless.
3. Pipeline-stage migration (`IsActive`, `IsEntry`) + domain method changes (`Open` assigns entry stage, new `ChangePipelineStage`) + their tests.
4. Rewrite `CompleteOpportunity` → `WinOpportunity` with authorization, reordered pipeline, extended request-hash, `expectedVersion` — this is the template every other command's handler copies, so getting its shape right first de-risks the rest.
5. `CreateOpportunity`, `OpenOpportunity`, `LoseOpportunity`, `ReassignOpportunity` handlers, each following the `WinOpportunity` template.
6. Concurrent-duplicate-idempotency fix (§13), applied to all handlers from step 4 onward, not bolted on later.
7. Queries: `GetOpportunity`, `ListOpportunities`, `GetPipelineStages`, `GetOpportunityAvailableActions`.
8. HTTP surface (`Host/Endpoints/OpportunityEndpoints.cs`, `ProblemDetails` middleware) — last, since it depends on §2.1 (authentication) which may land on a different timeline than 1–7.
9. Outbox dispatcher (§12) — recommended as a parallel, non-blocking track; does not gate any of 1–8.

---

### PHASE 2 READINESS

**Status update 2026-09-18: all seven OPEN DECISIONs (§2.1–§2.7) approved by the owner. Ready to implement: YES, gated only by writing a proper task-by-task execution plan first (§22) — not by any further product decision.**

- **Existing foundation:** Draft/Open/Won/Lost lifecycle and versioned tenant-configurable pipeline schema (Phase 1, RLS-complete); coarse action-based authorization with tenant-wide and owner-relation grants plus a proven query-scope equivalence contract (Phase 1.5); CRM-local idempotency/outbox/evidence tables and a working exact-replay/key-reuse pattern (`CompleteOpportunityHandler`); PartyRef integration with MasterData. Gaps to close in execution: no HTTP surface exists at all; no authentication pipeline exists; `PipelineStage` lacks active/entry columns; `Opportunity` has no `Reassign`/`ChangePipelineStage`; outbox has no dispatcher; Host defaults every DbContext to the `postgres` superuser.
- **Verified Phase 1.5 authorization/FLS/policy integration:** coarse `ActionKey`-based Allow/Deny and `OwnedBy`/tenant-wide scope are real and consumable now. Field-Level Security, Sharing/Org/Team/Territory scope, and policy obligations/`RequireApproval` are **frozen out by named Phase 1.5 decision** (round-3 Scope Lock), not missing by oversight — Phase 2 does not build them, and none of the binding spec's conditional "if it exists" clauses trigger.
- **Required commands:** `CreateOpportunity`, `OpenOpportunity`, `ChangePipelineStage`, `WinOpportunity` (rewrite of `CompleteOpportunity`), `LoseOpportunity`, `ReassignOpportunity`. `ReopenOpportunity` and Customer-Need commands are out of Phase 2 scope (§2.6).
- **Required queries:** `GetOpportunity`, `ListOpportunities`, `GetPipelineStages`, and a plain-boolean `GetOpportunityAvailableActions` projection (no richer policy outcomes exist to represent).
- **Initial pipeline-stage semantics:** RESOLVED — `OpenOpportunity` assigns the stage (not `Create`); the entry stage is the one with `PipelineStage.IsEntry = true` (§2.3), enforced by a partial unique index (exactly one per version).
- **Update/mutability model:** no generic PATCH; only fields with a dedicated command become writable (`AssignedPrincipal` via `Reassign`, `PipelineStageId` via `ChangePipelineStage`); `EstimatedAmount`/`Currency`/`ExpiryDate`/`CustomFields`/`PartyRef` stay immutable and explicitly deferred in Phase 2.
- **Idempotency/concurrency contract:** existing exact-replay and key-reuse-reject mechanism is correct and reusable; must extend `RequestHash` to cover full request payloads, wire `expectedVersion` through EF's existing optimistic-concurrency token, and fix the concurrent-duplicate case (currently a raw uncaught `DbUpdateException`) by catching the unique-violation and replaying the winner's committed result.
- **Migration impact:** additive only — `pipeline_stages.is_active`/`is_entry`. No new `Opportunity` owner column (§2.2 resolved to option (a), no schema change), no `lost_reasons` table (§2.5 resolved to keep free text). No destructive change to `OpportunityLine`/`CustomerNeed`/`OpportunityNeed`.
- **Critical risks/blockers:** none remaining at the *decision* level. One execution-level prerequisite carried into §21/§22's task list: Host connects every module as the `postgres` superuser by default — RLS provides zero protection until connection strings are corrected (step 1 of the execution sequence, not a further approval gate).
- **Open decisions:** none remaining — authentication (§2.1: JWT bearer), owner-field mapping (§2.2: `Reassign()`), entry-stage rule (§2.3: `IsEntry`), retired-stage semantics (§2.4: `IsActive`), LostReason timing (§2.5: free text), Reopen inclusion (§2.6: deferred), Win/Complete naming (§2.7: rename) — all resolved 2026-09-18.
- **Proposed implementation order:** fix Host connection-string defaults → pipeline-stage migration (`IsActive`+`IsEntry`) + `Open`/`ChangePipelineStage` domain changes → rewrite `WinOpportunity` as the handler template (adds authorization, JWT-derived `ActorContext`, reordered pipeline) → remaining commands (`Create`, `Open`, `Lose`, `Reassign`) → idempotency concurrent-duplicate fix → queries → JWT authentication + HTTP surface → outbox dispatcher (parallel, non-blocking).
- **Ready to implement: YES**, pending the task-by-task execution plan called for in §22 (this document is an architecture/scope plan, not yet a sequenced commit-by-commit plan in the `docs/plans/` convention used for Phase 0.5/1/1.5).

---

## 22. Next step — execution plan required before code

Per this repository's own established convention, every prior phase (0.5, 1, 1.5) had its own dedicated, numbered-task execution-plan document under `docs/plans/` — with per-task preconditions, exact file lists, and a commit-reference line filled in as each task lands (`docs/plans/*.md`, tracked per the plan-checkbox commit-ref standard). This architecture/scope document is deliberately **not** that: it is the "what and why," reconciled against the binding spec and the actual repository. Before any commit touches `src/`, the next artifact should be `docs/plans/crm-phase2/<date>-crm-phase2-opportunity-commands-api-execution-plan.md`, breaking §21's nine-step sequence into concrete, independently-committable tasks (mirroring how Phase 1.5's execution plan turned its own closure document into `docs/plans/enterprise-access-foundation/2026-09-17-enterprise-access-foundation-execution-plan.md`'s task list) — including the JWT-bearer wiring's own sub-decisions (issuer/audience configuration, token validation parameters, which existing `Access.Domain.Identity` type a validated JWT claim maps to) that are implementation detail, not further open product decisions.

---

Bu belge onay bekliyor. Yukarıdaki OPEN DECISION maddeleri (§2.1–§2.7) karara bağlanmadan implementasyona geçilmeyecek.
