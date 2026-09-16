# CRM Module — Current-State Analysis

Factual inventory of what is actually implemented in `src/Modules/CRM` and its supporting
infrastructure (`Contracts`, `Host`, `tests/CRM.Tests`) as of 2026-09-16, `main`, commit
`fd72a51`. This is a snapshot, not a design document — it describes code that exists, not
code that is planned. Cross-referenced against `AGENTS.md`, `docs/schema/crm-sales-schema.md`,
and the numbered architecture-decision docs (01–20) in the sibling research repository.

No code was modified to produce this document.

---

## 1. Executive Summary

The CRM module implements a narrow, vertically-complete slice: one aggregate
(`Opportunity`, with `OpportunityLine` as a child entity), one supporting entity
(`Party`), two lookup/join tables (`CustomerNeed`, `OpportunityNeed`), one
tenant-customization table (`TenantFieldDefinition`), and the enterprise-safety
infrastructure needed to run one command safely: outbox, evidence, idempotency, and
PostgreSQL Row-Level Security enforced against an unprivileged runtime role.

Exactly **one application command exists**: `CompleteOpportunity`. There is no API surface
(no HTTP endpoint calls it), no create/offer/cancel commands, no queries, and no
CRM-owned events other than the one `CompleteOpportunity` publishes. The module is a
domain + persistence + one-command slice with strong safety guarantees around that slice,
not a usable CRM yet.

The classic CRM funnel (Lead → Qualification → Opportunity → Pipeline Stage) does not
exist. There is no `Lead`, no `Account`/`Company` distinct from `Party`, no pipeline
stage separate from `OpportunityStatus`, no activities (calls, meetings, tasks, notes),
no competitors, no lost reasons, no ownership beyond a single assigned-principal field.
What exists is the tail end of a sales flow: a party already has an opportunity, and the
opportunity moves through a four-state lifecycle culminating in `Complete()`.

CRM and Sales are deliberately merged in this pilot (a named exception — see
[[feedback-open-decisions]] and doc 08 §2): `Opportunity`/`OpportunityLine` carry
quote-and-order-shaped fields (currency, line prices, totals) directly, with no separate
Sales module or schema. Refund is explicitly out of scope — not a status, not a
column, nothing.

Enterprise safety controls for the slice that exists are strong and DB-verified:
tenant-safe composite FKs, RLS tested against `fynovio_app` (an unprivileged role, not
the migration superuser), atomic state+outbox+evidence+idempotency writes in one
`SaveChanges()`, and single-row invariants as DB `CHECK` constraints. The main
enterprise-readiness gap is not code quality within the slice — it's how small the slice
is, and that `Host` still connects as the RLS-bypassing superuser by default.

---

## 2. CRM File / Module Inventory

| Component | File path | Type | Responsibility | Implemented? | Used? | Notes |
|---|---|---|---|---|---|---|
| `Opportunity` | `src/Modules/CRM/Domain/Opportunity.cs` | Aggregate | Sales opportunity lifecycle, state machine, money rounding | Yes | Yes | Aggregate root; `IHasRowVersion` |
| `OpportunityLine` | `src/Modules/CRM/Domain/OpportunityLine.cs` | Entity (child) | Line item under an opportunity | Yes | Yes | `internal` factory/mutators — only reachable via `Opportunity` |
| `Party` | `src/Modules/CRM/Domain/Party.cs` | Entity | Customer/contact record | Yes | Referenced by `Opportunity`, no command creates one yet | No `Create`-time CRM command exists; only the domain factory |
| `CustomerNeed` | `src/Modules/CRM/Domain/CustomerNeed.cs` | Entity | Named need with an average price | Yes | Referenced by `OpportunityNeed`, unused elsewhere | Not consumed by any command |
| `OpportunityNeed` | `src/Modules/CRM/Domain/OpportunityNeed.cs` | Join entity | Links an opportunity to selected needs | Yes | Persisted, never read by any command | `estimated_amount` is not derived from this table despite the doc comment saying that's the intent |
| `TenantFieldDefinition` | `src/Modules/CRM/Customization/TenantFieldDefinition.cs` | Entity | Metadata for tenant custom fields | Yes | Persisted, no command reads/writes it | Governs `custom_fields` jsonb shape only |
| `OutboxMessage` | `src/Modules/CRM/Outbox/OutboxMessage.cs` | Event (outbox) | CloudEvents-shaped outbound integration event | Yes | Written by `CompleteOpportunityHandler` | No dispatcher reads/publishes these yet |
| `EvidenceRecord` | `src/Modules/CRM/Evidence/EvidenceRecord.cs` | Audit/evidence | Append-only action record | Yes | Written by `CompleteOpportunityHandler` | No reader/projection exists |
| `IdempotencyRecord` | `src/Modules/CRM/Idempotency/IdempotencyRecord.cs` | Contract-ish (persistence) | Dedupes retried commands | Yes | Written/read by `CompleteOpportunityHandler` | No purge job for expired records |
| `CompleteOpportunityCommand` | `src/Modules/CRM/Application/CompleteOpportunityCommand.cs` | Command | Public input to the one command | Yes | Yes | Only public command in the module |
| `CompleteOpportunityHandler` | `src/Modules/CRM/Application/CompleteOpportunityHandler.cs` | Handler | Orchestrates state+outbox+evidence+idempotency atomically | Yes | Yes | Not wired to any HTTP endpoint |
| `CompleteOpportunityResult` | `src/Modules/CRM/Application/CompleteOpportunityResult.cs` | Contract | Handler return type | Yes | Yes | |
| `CompletedPayload` | `src/Modules/CRM/Application/CompletedPayload.cs` | Contract (internal) | Outbox/evidence/idempotency payload shape | Yes | Yes | `internal`, not a public contract |
| `OpportunityNotFoundException` | `src/Modules/CRM/Application/OpportunityNotFoundException.cs` | Exception | Not-found signal (also RLS-invisible-row signal) | Yes | Yes | |
| `IdempotencyKeyReusedException` | `src/Modules/CRM/Application/IdempotencyKeyReusedException.cs` | Exception | Same-key-different-payload signal | Yes | Yes | |
| `CrmDbContext` | `src/Modules/CRM/Persistence/CrmDbContext.cs` | Persistence | EF Core context, `crm` schema | Yes | Yes | |
| `CrmDbContextFactory` | `src/Modules/CRM/Persistence/CrmDbContextFactory.cs` | Persistence (design-time) | `dotnet ef` design-time factory | Yes | Yes (tooling only) | Not used at runtime |
| `CrmConnectionString` | `src/Modules/CRM/Persistence/CrmConnectionString.cs` | Persistence (config) | Shared local-dev connection string default | Yes | Yes | Reads `FYNOVIO_CRM_CONNECTION_STRING` |
| `CrmDbContextTenantExtensions` | `src/Modules/CRM/Persistence/CrmDbContextTenantExtensions.cs` | Persistence | Sets `app.tenant_id` for RLS, transaction-local | Yes | Yes | Throws if no explicit transaction is open |
| `*Configuration.cs` (9 files) | `src/Modules/CRM/Persistence/Configurations/` | Persistence (EF mapping) | Table/FK/CHECK/index mapping per entity | Yes | Yes | One file per entity, all with explicit CHECKs |
| Migrations (4) | `src/Modules/CRM/Persistence/Migrations/` | Persistence (generated) | Schema history | Yes | Yes | `InitialCrmSchema`, `FixOpportunityAssignedPrincipalIndex`, `FixCancelExpiryCheck`, `EnableRowLevelSecurity` (hand-written SQL body, the one permitted exception) |
| `ModuleBoundaryTests` | `tests/CRM.Tests/Architecture/ModuleBoundaryTests.cs` | Test (architecture) | NetArchTest module-boundary enforcement | Yes | Yes (CI) | 2 tests |
| `OpportunityStateMachineTests` | `tests/CRM.Tests/Domain/OpportunityStateMachineTests.cs` | Test (domain) | State transitions | Yes | Yes (CI) | 10 tests |
| `OpportunityMoneyTests` | `tests/CRM.Tests/Domain/OpportunityMoneyTests.cs` | Test (domain) | Rounding/total-derivation invariants | Yes | Yes (CI) | 6 tests |
| `OpportunityRowVersionTests` | `tests/CRM.Tests/Domain/OpportunityRowVersionTests.cs` | Test (domain) | Concurrency-token increment rules | Yes | Yes (CI) | 6 tests |
| `OpportunityPersistenceTests` | `tests/CRM.Tests/Integration/OpportunityPersistenceTests.cs` | Test (persistence) | EF mapping round-trips | Yes | Yes (CI) | 3 tests, Testcontainers |
| `OpportunityConcurrencyTests` | `tests/CRM.Tests/Integration/OpportunityConcurrencyTests.cs` | Test (concurrency) | Optimistic-concurrency conflict | Yes | Yes (CI) | 1 test |
| `TenantIsolationTests` | `tests/CRM.Tests/Integration/TenantIsolationTests.cs` | Test (tenant isolation) | RLS enforced against unprivileged role | Yes | Yes (CI) | 7 tests |
| `CompleteOpportunityHandlerTests` | `tests/CRM.Tests/Integration/CompleteOpportunityHandlerTests.cs` | Test (command/integration) | Handler atomicity, idempotency, RLS-under-runtime-role | Yes | Yes (CI) | 6 tests |
| `PostgresFixture` / `PostgresCollection` | `tests/CRM.Tests/Integration/` | Test infra | Testcontainers PostgreSQL 17 fixture, runtime-role connection string | Yes | Yes | |
| `TestData` | `tests/CRM.Tests/TestData.cs` | Test infra | Shared builders | Yes | Yes | |
| API endpoint for `CompleteOpportunity` | — | API | HTTP surface for the command | **No** | — | Not implemented; `Host/Program.cs` exposes only `/` and `/health/db` |
| Outbox dispatcher | — | Integration | Publishes `outbox_messages` rows externally | **No** | — | Rows are written, never read |
| Idempotency purge job | — | Persistence/background | Removes expired `idempotency_records` | **No** | — | `ExpiresAt` is indexed but nothing purges by it |
| Seed/demo data | — | — | — | **No** | — | None found in CRM |
| Frontend | `web/` | — | — | **No** | — | `web/.gitkeep` only, no frontend exists |

---

## 3. Current Domain Model

| Concept | Exists? | Class / table | Real domain concept or DTO? | Owns | Referenced by | Tenant-scoped? | Part of an aggregate? |
|---|---|---|---|---|---|---|---|
| Party / Customer | Yes | `Party` / `crm.parties` | Real entity | Itself, a self-referencing merge FK | `Opportunity.PartyId` (composite FK) | Yes | Standalone aggregate (own identity, own root) |
| Account / Company | **No** | — | — | — | — | — | `Party` does not distinguish person vs. organization; `party_type` was reviewed and explicitly deferred as YAGNI ([[feedback-open-decisions]]) |
| Contact | **No** | — | — | — | — | — | No separate contact concept; `Party` is the only actor |
| Lead | **No** | — | — | — | — | — | Not modeled at all |
| Opportunity | Yes | `Opportunity` / `crm.opportunities` | Real aggregate root | `OpportunityLine` (children), the `RowVersion`/state machine | `OpportunityNeed`, outbox/evidence/idempotency rows | Yes | Aggregate root |
| Pipeline | **No** | — | — | — | — | — | No pipeline concept exists |
| Pipeline Stage | **No** | — | — | — | — | — | `OpportunityStatus` (waiting/offered/completed/canceled) is a lifecycle status, not a configurable pipeline stage — see §5 |
| Activity / Task / Note / Call / Meeting | **No** | — | — | — | — | — | None of these exist |
| Customer Need | Yes | `CustomerNeed` / `crm.customer_needs` | Real entity, but orphaned | `AveragePrice` | `OpportunityNeed.CustomerNeedId` | Yes | Standalone; not part of `Opportunity`'s aggregate boundary |
| Competitor | **No** | — | — | — | — | — | Not modeled |
| Lost Reason | **No** | — | — | — | — | — | `Opportunity.CancelReason` exists but is a free-text field, not a structured lost-reason taxonomy |
| Opportunity Line | Yes | `OpportunityLine` / `crm.opportunity_lines` | Real child entity | Nothing (leaf) | `Opportunity._lines` | Yes | Child of `Opportunity`; mutated only through the aggregate root |
| Product Reference | Yes (as a reference, not an entity) | `OpportunityLine.ProductRef` (`EntityRef`) | Value object, not a domain entity | — | — | Yes (via `EntityRef.TenantId`) | Value object on `OpportunityLine`; no FK because Master Data doesn't own product rows yet |
| Assignment / Owner | Partial | `Opportunity.AssignedPrincipalIssuer/Subject` (`PrincipalRef`) | Real field, single-owner only | — | — | Indexed by tenant+issuer+subject | Field on `Opportunity`, not a separate concept |
| Tags | **No** | — | — | — | — | — | Not modeled |
| Custom Fields | Partial | `Party.CustomFields` / `Opportunity.CustomFields` (jsonb) + `TenantFieldDefinition` metadata | Real mechanism | — | — | Yes | Metadata table + jsonb column, Tier-1 only (doc 15 §7) |
| Source / Provenance | Partial | `Party.CreationSource` (`Manual`/`AiVoiceCapture`) | Real enum field | — | — | Yes (via `Party`) | Only on `Party`, not on `Opportunity` |

---

## 4. Current Business Flow

Reconstructed strictly from `Opportunity.cs` and the CHECK constraints in
`OpportunityConfiguration.cs` / `OpportunityLineConfiguration.cs` — nothing inferred
beyond what the code enforces.

**Entry point:** `Opportunity.Create(tenantId, partyId, assignedPrincipal, currency, estimatedAmount)`.
There is no command wrapping this — it is a bare domain factory, callable only from
inside the CRM module (no `CreateOpportunityHandler` exists).

**State transitions (enforced in `Opportunity`, mirrored by DB `CHECK`s):**

```
Waiting --AddLine()--> Waiting            (lines only addable while Waiting)
Waiting --Offer(expiryDate)--> Offered    (expiry must be future; stamps OfferDate)
Offered --Complete()--> Completed         (requires ≥1 active, non-optional line; stamps SaleDate, TotalAmount)
Waiting --Cancel(reason)--> Canceled
Offered --Cancel(reason)--> Canceled
(Completed, Canceled are terminal — no further transition)
```

`CancelLine(line, reason)` is a side-channel mutation available in `Waiting` or `Offered`
(rejected once `Completed` or `Canceled`); it marks a line canceled without changing the
opportunity's own status.

**Allowed commands (application layer):** only `CompleteOpportunity`. `Create`, `AddLine`,
`Offer`, `Cancel`, `CancelLine` are domain methods with **no application command**,
**no API endpoint**, and **no test that exercises them through a command handler** — they
are exercised only directly against the domain object in unit tests
(`OpportunityStateMachineTests`, `OpportunityMoneyTests`, `OpportunityRowVersionTests`).

**Required fields:** `TenantId`, `PartyId`, `AssignedPrincipal`, `Currency` (3-letter),
`EstimatedAmount` (≥0, ≤2dp). On `Offer`: `ExpiryDate` (future). On `Complete`: at least
one line that is `!IsOptional && !IsCanceled`.

**Validations:** currency length, non-negative amounts, 2dp entered-value precision on
`EstimatedAmount` and `UnitPrice`, positive `Quantity`, future `ExpiryDate`, non-empty
`CancelReason`, line-belongs-to-this-opportunity check in `CancelLine`.

**Close/win/loss behavior:** there is no "won/lost" distinction — only `Completed` (a
sale happened) and `Canceled` (with a free-text reason, which could represent a loss, a
customer withdrawal, or anything else — the schema does not distinguish).

**Ownership/assignment:** a single `PrincipalRef` (`AssignedPrincipalIssuer/Subject`) is
set once, at `Create`, and never reassigned — there is no `Reassign`/`ChangeOwner`
method.

**Product references:** `OpportunityLine.ProductRef` is an `EntityRef` value object with
no FK (Master Data doesn't own product rows yet, per doc 07 §3's no-cross-schema-FK rule).

**Activity handling:** none — no activity/task/note/call/meeting concept exists anywhere
in the module.

**Where the flow stops:** the flow is complete as a state machine (`Waiting → Offered →
Completed`/`Canceled`), but stops being "a CRM" well before that — there is no lead
capture, no qualification step, and no command surface for anything except completing an
already-offered opportunity.

---

## 5. Status / Stage Analysis

- **Lifecycle status:** `OpportunityStatus` (`waiting`/`offered`/`completed`/`canceled`)
  is the only status-like concept in the module. It is a closed enum backed by a DB
  `CHECK` (`ck_opportunities_status`).
- **Pipeline stage:** does not exist as a distinct concept. `OpportunityStatus` is doing
  double duty as both lifecycle status and (informally) pipeline position — there is no
  separate, tenant-configurable pipeline-stage table or column.
- **Business type:** not modeled — there is no `type` column distinguishing, e.g., new
  business vs. renewal vs. upsell.
- **Sale/refund/cancel states:** `Completed` represents "sale happened."
  `Canceled` represents "did not happen, for `CancelReason`." **Refund is not a state**
  — it was explicitly and deliberately excluded from the pilot's active domain
  ([[feedback-open-decisions]]), consistent with doc 17 §"legacy issues" flagging refund
  as a status-value anti-pattern in the predecessor system (crm-app).
- **Mixing check:** no evidence of the anti-patterns doc 17 warns about (refund as
  opportunity status, pipeline stage hardcoded as status) — because pipeline stage
  simply isn't implemented yet, there's nothing to mix it with. `OpportunityStatus`
  itself is clean: four values, each with DB-enforced required-field invariants
  (`ck_opportunities_expiry_required_once_offered`,
  `ck_opportunities_sale_date_required_once_completed`,
  `ck_opportunities_cancel_fields_required_once_canceled`).

---

## 6. CRM vs Sales Boundary

Per the named pilot exception, CRM and Sales are **one merged module/schema** right now
(doc 08 §2's pilot-assembly allowance) — there is no `src/Modules/Sales/` project (it was
deleted with the owner's approval; see [[feedback-open-decisions]]). Everything below
lives inside `CRM.*`.

| Concept | Current state | Classification |
|---|---|---|
| Quote | `Opportunity` in `Offered` status functions as a quote (has `ExpiryDate`, `OfferDate`) | Correctly inside CRM **for this pilot** (merged-module exception); would move to Sales if the module were later split |
| Quote lines | `OpportunityLine` | Same — merged-module exception |
| Pricing | `OpportunityLine.UnitPrice`, `LineTotal` | Same — merged-module exception |
| Discount | **Not implemented** | Not present anywhere |
| Order | `Opportunity` in `Completed` status functions as an order/sale record (`TotalAmount`, `SaleDate`) | Same — merged-module exception |
| Order lines | `OpportunityLine` (same rows as quote lines — no separate order-line entity) | Same — merged-module exception |
| Refund | **Not implemented, deliberately excluded** | Out of scope, archive-only in legacy migration lane per [[feedback-open-decisions]] |
| Invoice | **Not implemented** | Not present anywhere |

There is no cross-schema reference from CRM to a distinct Sales schema because no
distinct Sales schema exists in this codebase today. This is a documented, approved
exception, not an oversight — but it means the "CRM vs Sales boundary" question in
practice is currently moot: everything commercial is CRM.

---

## 7. Tenancy and Enterprise Safety

| Control | Status | Evidence |
|---|---|---|
| `TenantId` usage | **IMPLEMENTED** | Every entity carries `TenantId` (typed primitive from `Contracts`); no tenant-free property found |
| PostgreSQL RLS | **IMPLEMENTED** | `20260916081636_EnableRowLevelSecurity.cs`: `ENABLE`+`FORCE ROW LEVEL SECURITY` and a `tenant_isolation` policy (USING + WITH CHECK) on all 9 CRM tenant-scoped tables; tested against `fynovio_app`, an unprivileged role, in `TenantIsolationTests` (7 tests) and `CompleteOpportunityHandlerTests` (2 tests) |
| Composite tenant-safe FKs | **IMPLEMENTED** | `Opportunity→Party`, `OpportunityLine→Opportunity`, `OpportunityNeed→Opportunity`/`→CustomerNeed`, `Party→Party` (self-merge) all use `(TenantId, Id)` alternate keys + composite FKs |
| Cross-module FKs | **NOT APPLICABLE / N/A** | No cross-module FK exists or is needed — CRM only references its own schema (Access is a separate module with no FK relationship to CRM in code) |
| `EntityRef` usage | **IMPLEMENTED** | `OpportunityLine.ProductRef` uses `EntityRef` instead of an FK, exactly per doc 07 §3 (no cross-schema FK; Master Data doesn't own product rows yet) |
| Optimistic concurrency / row versioning | **IMPLEMENTED** | `Opportunity.RowVersion` (`bigint`, `IsConcurrencyToken()`), incremented by the aggregate itself in `Touch()`, not by an EF interceptor (the CRM interceptor was deliberately deleted — see `Contracts/IHasRowVersion.cs` doc comment); verified by `OpportunityConcurrencyTests` and `OpportunityRowVersionTests` |
| Soft delete | **NOT PRESENT — by design** | No `deleted_at` anywhere; `AGENTS.md` explicitly forbids soft delete in favor of domain status values (e.g. `Canceled`) |
| Audit / evidence hooks | **PARTIAL** | `EvidenceRecord` is written atomically for `CompleteOpportunity` only (the one risk-catalogued command that exists); no other command exists to need one yet, per the doc 20 "trigger-based" enforcement tier |
| Idempotency | **IMPLEMENTED (for the one command that exists)** | `IdempotencyRecord` (natural key: tenant+principal+operation+key), request-hash mismatch detection, response replay — exercised by 3 of the 6 `CompleteOpportunityHandlerTests` |
| Transactional outbox | **PARTIAL** | Outbox row committed atomically with domain state (`CompleteOpportunityHandlerTests.Completing_writes_state_outbox_and_evidence_together`) — but **no dispatcher reads/publishes these rows**, so nothing actually leaves the database yet (planned, per `docs/plans/2026-09-16-pilot-enforcement.md` item 2) |
| Correlation / causation IDs | **IMPLEMENTED (for the one command)** | `OutboxMessage.CorrelationId`/`CausationId`, `EvidenceRecord.CorrelationId` populated from `CompleteOpportunityCommand.CorrelationId`; no caller exists yet to generate one outside a test |

**Known gap not listed above:** `Host/Program.cs` registers `CrmDbContext` with a
connection string that defaults to the `postgres` superuser
(`CrmConnectionString.LocalDevDefault`). Superusers bypass RLS unconditionally
(PostgreSQL semantics — `FORCE ROW LEVEL SECURITY` does not change this for the table
owner). **The running application today gets no tenant isolation from the database**
unless an operator explicitly sets `ConnectionStrings__Crm` to the `fynovio_app` role, as
documented in `README.md` "Runtime role (Row-Level Security)." This is a deployment/config
gap, not a code gap — the RLS code and its test are correct — but it is the single
highest-severity gap in this section.

---

## 8. Persistence Model

All tables live in the `crm` PostgreSQL schema (`CrmDbContext.Schema = "crm"`).

### `parties`
- PK: `id`
- Tenant key: `tenant_id` (+ alternate key `(tenant_id, id)`)
- FK: self-referencing `(tenant_id, merged_into_party_id) → (tenant_id, id)`, nullable, `RESTRICT`
- Unique: none beyond PK/alternate key
- Index: `(tenant_id, email)`
- CHECK: `ck_parties_creation_source` (`manual`/`ai_voice_capture`)
- JSONB: `custom_fields`
- Concurrency field: none
- Timestamps: `created_at`, `updated_at`

### `customer_needs`
- PK: `id`
- Tenant key: `tenant_id` (+ alternate key `(tenant_id, id)`)
- FK: none
- CHECK: `ck_customer_needs_average_price_non_negative`
- JSONB: none
- Concurrency field: none
- Timestamps: `created_at` only (no `updated_at` — entity has no mutator besides `Create`)

### `opportunities`
- PK: `id`
- Tenant key: `tenant_id` (+ alternate key `(tenant_id, id)`)
- FK: `(tenant_id, party_id) → parties(tenant_id, id)`, `RESTRICT`
- Indexes: `(tenant_id, status)`, `(tenant_id, party_id)`, `(tenant_id, assigned_principal_issuer, assigned_principal_subject)` (named `ix_opportunities_tenant_assigned_principal` — EF's auto name would exceed Postgres's 63-byte identifier limit), `(tenant_id, created_at)`
- CHECKs: `ck_opportunities_status`, `ck_opportunities_estimated_amount_non_negative`, `ck_opportunities_expiry_required_once_offered`, `ck_opportunities_sale_date_required_once_completed`, `ck_opportunities_cancel_fields_required_once_canceled`
- JSONB: `custom_fields`
- Concurrency field: `row_version` (`bigint`, EF concurrency token) — **no DB `DEFAULT 1`**, relies entirely on the application always setting it (known gap, tracked in memory)
- Money columns: `estimated_amount numeric(19,2)`, `total_amount numeric(19,4)` (computed, rounds to 2dp once at `Complete()`)
- Timestamps: `created_at`, `updated_at`, plus lifecycle timestamps `offer_date`, `sale_date`, `cancel_date` (all nullable, each gated by a CHECK)

### `opportunity_lines`
- PK: `id`
- Tenant key: `tenant_id`
- FK: `(tenant_id, opportunity_id) → opportunities(tenant_id, id)`, `CASCADE`
- Index: `(tenant_id, opportunity_id)`
- CHECKs: `ck_opportunity_lines_quantity_positive`, `ck_opportunity_lines_unit_price_non_negative`
- JSONB: none
- Money columns: `unit_price numeric(19,2)`, `line_total numeric(19,4)` (nullable — rows persisted before it was computed can be `NULL`, handled explicitly in `Opportunity.Complete()`)
- Concurrency field: none (child entity, covered by the root's `row_version`)
- Timestamps: `created_at` only

### `opportunity_needs`
- PK: composite `(tenant_id, opportunity_id, customer_need_id)` — no surrogate id
- FKs: `(tenant_id, opportunity_id) → opportunities`, `CASCADE`; `(tenant_id, customer_need_id) → customer_needs`, `RESTRICT`
- No CHECK, no JSONB, no concurrency field, no timestamps
- **Orphan table candidate**: nothing reads it — see §11

### `tenant_field_definitions`
- PK: `id`
- Tenant key: `tenant_id`
- Unique: `(tenant_id, aggregate_type, field_name)`
- CHECKs: `ck_tenant_field_definitions_aggregate_type` (`Party`/`Opportunity`), `ck_tenant_field_definitions_field_type` (`text`/`number`/`boolean`/`date`)
- No JSONB, no concurrency field
- Timestamps: `created_at` only

### `outbox_messages`
- PK: `id`
- Tenant key: `tenant_id`
- Unique: `event_id`
- Partial index: `processed_at` `WHERE processed_at IS NULL` (dispatcher-scan index, unused — no dispatcher exists)
- Index: `(tenant_id, aggregate_type, aggregate_id)`
- JSONB: `payload`
- No CHECK, no concurrency field
- Timestamps: `occurred_at`, nullable `processed_at`

### `idempotency_records`
- PK: composite `(tenant_id, principal_issuer, principal_subject, operation, idempotency_key)` — natural key, no surrogate id
- Index: `expires_at` (for a purge job that does not exist yet)
- JSONB: `response_payload`
- No CHECK, no concurrency field
- Timestamps: `created_at`, `expires_at`

### `evidence_records`
- PK: `id`
- Tenant key: `tenant_id`
- Indexes: `(tenant_id, aggregate_type, aggregate_id)`, `correlation_id`
- JSONB: `detail`
- No CHECK
- Timestamps: `occurred_at`
- **Append-only by grant, not by schema**: `scripts/create-runtime-role.sql` revokes `UPDATE`/`DELETE` on this table from `fynovio_app` — the immutability is a privilege grant, not a database-level constraint (e.g. no trigger/rule blocking it at the superuser level, which is expected since the superuser must retain full access for operations)

### Cross-cutting findings
- **Orphan tables**: `opportunity_needs` and `customer_needs` are fully wired at the
  persistence layer (FKs, CHECKs) but have **no command that writes or reads them** —
  they exist to compute `estimated_amount` from selected needs (per the doc comment on
  `OpportunityNeed`), but that computation is not implemented; `Opportunity.EstimatedAmount`
  is currently just an entered value at `Create` time.
- **Suspicious nullable columns**: `opportunity_lines.line_total` is nullable purely for
  a legacy-migration reason ("rows persisted before it was computed") that doesn't apply
  to this codebase — every row here is created through `OpportunityLine.Create`, which
  always sets it. This nullability looks carried over from the schema doc rather than
  something this codebase's data actually produces yet.
- **Missing relationships**: `tenant_field_definitions` has no FK to the aggregates it
  describes (`Party`/`Opportunity`) — it's a string-based `aggregate_type` discriminator,
  not a polymorphic FK, which is consistent with it being metadata rather than data.
- **No over-coupling or cross-domain FK violations found**: every FK is intra-schema and
  tenant-safe; the module-boundary tests (`ModuleBoundaryTests`) run in CI to keep it
  that way.

---

## 9. API Surface

**There is no CRM API surface.** `src/Host/Program.cs` defines exactly two endpoints,
neither of which is CRM business logic:

| HTTP method | Route | Command/query | Authorization | Tenant scope | Request DTO | Response DTO |
|---|---|---|---|---|---|---|
| GET | `/` | none | none | none | none | `string` ("Hello World!") |
| GET | `/health/db` | `CrmDbContext.Database.CanConnectAsync` | none | none | none | `200`/`503` |

`CompleteOpportunityHandler` — the one command that exists — is not invoked from any
endpoint. It is only ever called from `CompleteOpportunityHandlerTests`. A client can do
**nothing** against this system today except check its own health.

---

## 10. Events and Integration

| Event name | Producer | Trigger | Payload | Version | Outbox? | Correlation ID? | Consumers |
|---|---|---|---|---|---|---|---|
| `enterprise.crmsales.opportunity.completed.v1` | `CompleteOpportunityHandler` | `Opportunity.Complete()` succeeds | `CompletedPayload` (`OpportunityId`, `TotalAmount`, `Currency`) | `v1` (in the event type string) | Yes — written to `outbox_messages` in the same `SaveChanges()` | Yes (`CorrelationId` from the command; `CausationId` always `null` — nothing upstream produces one yet) | **None** — no dispatcher, no subscriber |

This is the only event the module defines. It is **defined and persisted but never
published** — flagged per the task's instruction: the outbox row is durable and correct,
but nothing polls `outbox_messages WHERE processed_at IS NULL` and delivers it anywhere.
This is a known, tracked gap (`docs/plans/2026-09-16-pilot-enforcement.md` item 2: "Outbox
dispatcher in Worker").

---

## 11. Test Coverage

41 tests total in `tests/CRM.Tests`, all passing as of the last verified run (2026-09-16).

| Category | File | Count | Notes |
|---|---|---|---|
| Domain invariant (state machine) | `OpportunityStateMachineTests` | 10 | Transition legality, required-field gates |
| Domain invariant (money) | `OpportunityMoneyTests` | 6 | Rounding rule, optional/canceled-line exclusion |
| Domain invariant (concurrency token) | `OpportunityRowVersionTests` | 6 | `RowVersion` increment-once-per-mutation rule |
| Architecture / module boundary | `ModuleBoundaryTests` | 2 | NetArchTest: no cross-module dependency, domain doesn't depend on persistence |
| Persistence (integration) | `OpportunityPersistenceTests` | 3 | EF mapping round-trips against real PostgreSQL (Testcontainers) |
| Concurrency (integration) | `OpportunityConcurrencyTests` | 1 | Optimistic-concurrency conflict between two competing writes |
| Tenant isolation (integration, unprivileged role) | `TenantIsolationTests` | 7 | RLS read/write/update/delete denial, pooled-connection leak check, explicit-transaction requirement |
| Command / handler (integration) | `CompleteOpportunityHandlerTests` | 6 | Atomic write, idempotent replay, key-reuse rejection, rollback-on-failure, runtime-role execution, cross-tenant not-found |

**No test exists for:**
- `Party.Create`, `Party.MergeInto`, `Party.SetCustomFields` (only exercised indirectly via `TestData` builders, never asserted on directly)
- `CustomerNeed.Create`
- `OpportunityNeed.Create`
- `TenantFieldDefinition.Create`
- Any API-layer behavior (there is no API layer to test)
- Any authorization behavior (there is no authorization check in the module — `CompleteOpportunityHandler` trusts the caller's `PrincipalRef`/`TenantId` unconditionally; access control is presumably meant to live in `Access`, which has no tests or wiring yet)
- Outbox dispatch (nothing to test — no dispatcher exists)
- Idempotency purge (nothing to test — no purge job exists)
- `estimated_amount` derivation from needs (not implemented)

**Critical behavior with no coverage:** authorization is the most significant gap —
`CompleteOpportunityHandler` will execute for any caller who can construct a valid
`CompleteOpportunityCommand` with a `TenantId` and `PrincipalRef` it doesn't otherwise
validate against a role/permission system. Given `Access` has no RLS, no tests, and no
Host registration yet, this is an open, structural gap rather than a test-coverage gap
per se — but it means today, nothing enforces "assigned principal or manager can
complete" beyond application-layer trust.

---

## 12. Current Scope Summary

**IMPLEMENTED**
- `Opportunity` aggregate: full `Waiting → Offered → Completed`/`Canceled` state machine, with `CancelLine` as a side channel.
- `Party`, `CustomerNeed`, `OpportunityNeed`, `TenantFieldDefinition` as persisted entities.
- Tenant-safe composite FKs across all CRM tables.
- PostgreSQL RLS (`ENABLE`+`FORCE`), verified against an unprivileged runtime role.
- Optimistic concurrency via self-incrementing `RowVersion`.
- One command (`CompleteOpportunity`) with atomic state+outbox+evidence+idempotency writes in a single `SaveChanges()`.
- DB `CHECK` constraints for every single-row invariant the schema doc specifies.
- 41 tests across domain, architecture, persistence, concurrency, tenant isolation, and the one command.
- CI (`dotnet format`, Release build, vulnerable-package check, full test suite).

**PARTIALLY IMPLEMENTED**
- Outbox: rows are written correctly and atomically, but nothing dispatches them.
- Evidence: written for the one risk-catalogued command that exists; append-only by DB grant, not by database-level immutability constraint.
- Custom fields: mechanism (jsonb + metadata table) exists; no command populates or validates against it.
- Tenant isolation in the *running application*: correct at the schema/policy/test level, but `Host`'s default connection string is the RLS-bypassing superuser.

**PLANNED BUT NOT IMPLEMENTED**
- Outbox dispatcher (`docs/plans/2026-09-16-pilot-enforcement.md` item 2).
- HTTP surface for `CompleteOpportunity` (item 3).
- `estimated_amount` derived from `OpportunityNeed`/`CustomerNeed` (tracked as a known gap in memory).
- Idempotency-record purge job.
- `row_version DEFAULT 1` at the database level.

**NOT PRESENT**
- Lead, Pipeline, Pipeline Stage, Account/Company, Contact, Activity, Task, Note, Call, Meeting, Competitor, Lost Reason (structured), Tags.
- Any command for `Create`, `AddLine`, `Offer`, `Cancel`, `CancelLine` (domain methods exist; no application command wraps them).
- Any API endpoint for CRM.
- Any authorization/permission check inside the CRM module.
- Frontend (`web/` is empty).
- Seed/demo data.

**AMBIGUOUS / NEEDS DECISION**
- Whether an approval step between `Offered` and `Completed` is needed — explicitly deferred, tracked in [[feedback-open-decisions]].
- Identifier strategy (opaque global IDs per doc 04 vs. the `bigint` sequences actually in use) — never decided.
- Whether `Opportunity` needs a `LegalEntityId` (doc 04 §2, FF06) — open.
- Per-module DB role scoping (today, one `fynovio_app` role, not yet split per module) — deferred per doc 20's trigger-based tier.

---

## 13. Target-vs-Current Comparison

Target (for comparison purposes only, per the task's framing):
`Party/Company → Contact → Lead → Qualification → Opportunity → Pipeline Stage → Won/Lost`,
plus Activities/Tasks/Calls/Meetings/Notes/Needs/Competitors/Lost Reasons/Ownership/Product
references.

| Target element | Current state | Gap severity |
|---|---|---|
| Party/Company | `Party` exists but conflates person and company (no `party_type`) | MEDIUM — deferred as YAGNI already, not a defect |
| Contact | Not present | HIGH for a real CRM, DEFER for this pilot (Party stands in) |
| Lead | Not present — flow starts at Opportunity | CRITICAL for a real CRM funnel, DEFER for this pilot (pilot targets the tail of the funnel, not the front) |
| Qualification | Not present | HIGH for a real CRM, DEFER for this pilot |
| Opportunity | Fully implemented, well-guarded | — (no gap) |
| Pipeline Stage | Not present — only lifecycle `OpportunityStatus` | CRITICAL for a real CRM, but this pilot's `OpportunityStatus` was scoped deliberately narrow |
| Won/Lost | Not modeled as a distinct outcome from `Canceled`/`Completed` | MEDIUM — `Completed` = won, `Canceled` = a catch-all that doesn't distinguish "lost" from "withdrawn" from "duplicate" |
| Activities (calls, tasks, meetings, notes) | Not present | HIGH for a real CRM, DEFER for this pilot |
| Needs | `CustomerNeed`/`OpportunityNeed` exist but are wired-and-unused | MEDIUM — the persistence exists, the behavior (estimated-amount derivation) doesn't |
| Competitors | Not present | LOW for this pilot |
| Lost reasons | `CancelReason` exists but is unstructured free text | LOW-MEDIUM |
| Ownership | Single `AssignedPrincipal`, no reassignment, no team ownership | MEDIUM |
| Product references | `EntityRef`-based, correctly deferring to a not-yet-existing Master Data module | — (no gap — correctly modeled as a forward reference) |

---

## 14. Recommended Next Actions

Not a recommendation to build every missing concept — separated by urgency, per the
task's explicit instruction not to over-engineer.

**Required for the current CRM pilot to be minimally usable:**
1. An HTTP endpoint for `CompleteOpportunity` (already planned, item 3).
2. Commands for `Create`, `AddLine`, `Offer`, `Cancel` — without these, `CompleteOpportunity` has no legitimate way to reach an opportunity in `Offered` status except through a test or direct DB manipulation.

**Required before production:**
3. Fix the default `Host` connection string / deployment docs so RLS is not silently bypassed by default (§7's highest-severity gap).
4. Outbox dispatcher (already planned, item 2).
5. Authorization check in front of `CompleteOpportunityHandler` (currently unconditional trust of caller-supplied `TenantId`/`PrincipalRef`), which depends on `Access` module wiring being finished.
6. Idempotency-record purge job (`ExpiresAt` index already exists, unused).
7. `row_version DEFAULT 1` at the schema level, to remove reliance on application code always setting it correctly.

**Required for enterprise foundation (not urgent for the pilot):**
8. Decide and either implement or formally drop `estimated_amount` derivation from `OpportunityNeed`/`CustomerNeed`, since the tables exist half-wired today.
9. Resolve the identifier-strategy and `LegalEntityId` open decisions before they get baked further into the schema.

**Future capability (explicitly out of scope now):**
10. Lead/Qualification funnel stages, Pipeline Stage as a distinct configurable concept, Activities (calls/tasks/meetings/notes), Competitors, structured Lost Reasons, multi-owner/team assignment, Contact as distinct from Party, Tags.

---

## CRM CURRENT STATE

- **Implemented concepts:** Opportunity (full state machine), OpportunityLine, Party, CustomerNeed, OpportunityNeed, TenantFieldDefinition, Outbox row (written), Evidence row (written), Idempotency (write+replay), RLS (enforced + tested), tenant-safe composite FKs, optimistic concurrency.
- **Partial concepts:** Outbox (written, never dispatched), Evidence (append-only by grant only), Custom fields (mechanism exists, unused), needs-based estimated-amount (tables exist, computation doesn't).
- **Missing critical concepts:** Lead, Pipeline Stage, Contact, Activities, Won/Lost distinction, any command besides `CompleteOpportunity`, any API endpoint, any authorization check.
- **Main architectural risks:** `Host` defaults to the RLS-bypassing `postgres` superuser; the module's only entry point (`CompleteOpportunity`) has no authorization gate; `opportunity_needs`/`customer_needs` are fully wired at the DB layer but functionally dead code.
- **CRM/Sales boundary issues:** none in practice — CRM and Sales are one merged module by explicit, approved decision; quote/order/pricing concepts live correctly inside that merger; refund is correctly and deliberately absent.
- **Enterprise readiness:** the safety *mechanisms* (RLS, outbox, evidence, idempotency, concurrency, tenant-safe FKs) meet the doc 20 binding-core bar and are DB-verified — but the *domain surface* they protect is one command wide, so "enterprise-ready infrastructure around a pilot-sized domain" is the accurate characterization, not "enterprise-ready CRM."
- **Recommended next 5 actions:** (1) HTTP endpoint + Create/AddLine/Offer/Cancel commands so the flow is reachable outside tests; (2) fix the default connection string/deployment story so RLS isn't silently bypassed; (3) outbox dispatcher; (4) authorization gate on `CompleteOpportunity`; (5) idempotency purge job.
