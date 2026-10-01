# Plan: Semantic Catalog + ChangeSet v1 + `reference` fields + shared views (phase 2: A + C)

**Status:** In progress — ADR accepted 2026-10-01 under delegated go-ahead (`adr-semantic-catalog-changeset.md`).
**Branch:** `feat/semantic-catalog`.
**Order:** C-1 → C-2 → A-1 → A-2 (deviation from B → A → C recorded in the ADR).
**Invariants to keep green at every commit:** full .NET suite (1504 at the start) and web suite (743 at the start). No weakening of RLS, PDP, idempotency, outbox, evidence or state machines.

## Task 0 — Decision
- [x] Write the combined ADR and this plan.
→ Commit: `01c2a32` "docs(adr): accept the semantic catalog and changeset ADR and plan (phase 2)"

## C-1 — Behavior-preserving move to the catalog
### Task 1 — Contracts
- [x] `Contracts`: `FieldType`, `FieldStatus`, `FieldOption`, `FieldConfig`, `FieldDefinition` read model, `ISemanticDefinitionReader` (`ListFieldsAsync`, `GetFieldAsync`) scoped to `(tenant, owner_context, object_type)`.
→ Commit: `3ae3bc1` "feat(semantic-catalog): move field definitions to a SemanticCatalog module behind a reader (C-1)"
Note: the read model also carries `FieldConfig` helpers (`TextLengthCap`, `IsOptionActive`, `HasOption`) so CRM keeps no second copy of the definition side; `Validate` lives only in the catalog (`FieldConfigRules`).

### Task 2 — Module, persistence, migration
- [x] `src/Modules/SemanticCatalog` project + `SemanticCatalogDbContext` (schema `semantic`): `field_definitions` (CHECKs carried over from tier 1), `idempotency_records`, `evidence_records`, `outbox_messages`; RLS ENABLE + FORCE.
- [x] Migration copies `crm.tenant_field_definitions` when it exists (ids preserved, sequence advanced); runtime/relay grants; README migration order; slnx.
→ Commit: `3ae3bc1` "feat(semantic-catalog): move field definitions to a SemanticCatalog module behind a reader (C-1)"
Note: the catalog table drops the tier-1 `owner_scope` column (it only ever held `Tenant`).

### Task 3 — Move the write side
- [x] Domain (`FieldDefinition` entity + config validation) and `ManageFieldDefinitionHandler` move to the catalog, same limits/errors; endpoints unchanged; authorization via the `owner_context` action map (reuse `crm.settings.*`).
- [x] `ISemanticDefinitionReader` implementation.
→ Commit: `3ae3bc1` "feat(semantic-catalog): move field definitions to a SemanticCatalog module behind a reader (C-1)"

### Task 4 — Switch CRM to the reader
- [x] The five CRM readers (create, update-values, list, impact, activity) and the two value validators use the reader (list-definitions moves to the catalog with the write side); `CustomFieldValues` / `CustomFieldFilter` operate on the contract read model; `CrmDbContext` loses the definitions DbSet.
- [x] CRM migration drops `crm.tenant_field_definitions` (guarded: refuses while rows exist that the catalog lacks).
→ Commit: `3ae3bc1` "feat(semantic-catalog): move field definitions to a SemanticCatalog module behind a reader (C-1)"

### Task 5 — Outbox, relay, roles
- [x] `OutboxSources` + `semantic`; Messaging RLS migration; relay/runtime role scripts; `RelayRoleTests`; Host/Worker registration; Host fixtures migrate the new context.
→ Commit: `3ae3bc1` "feat(semantic-catalog): move field definitions to a SemanticCatalog module behind a reader (C-1)"
Note: Host.Tests fixtures also needed `ConnectionStrings__SemanticCatalog` (otherwise the app silently falls back to the localhost dev default) and the `semantic` grants in their hand-written runtime-role SQL; the Collaboration fixture slices `create-runtime-role.sql` by `-- X module.` headings, so the new section heading had to follow that pattern.

### Task 6 — Tests and verification
- [x] `tests/SemanticCatalog.Tests` (domain, persistence, RLS isolation, idempotency, evidence/outbox, migration copy); CRM tests use a stub reader.
→ Commit: `3ae3bc1` "feat(semantic-catalog): move field definitions to a SemanticCatalog module behind a reader (C-1)"
Note: .NET suites after C-1: Access 313, Collaboration 306, CRM 334, Host 405, MasterData 44, Messaging 49, SemanticCatalog 57, TenantLifecycle 20 = 1528 (was 1504; the definition tests moved to the catalog, and the new tests cover the reader as the runtime role, the authorization agreement, the data move and the endpoint error contract). EF pending-model checks clean for SemanticCatalog, CRM and Messaging; dev DB migrated in order (the one real definition, id 1, copied). The web suite and the browser check run once at the end (no web change in C-1).
- [ ] Full .NET + web suites green; EF model check clean; dev DB migrated in the documented order and the browser check repeated.

## C-2 — ChangeSet v1 for fields
### Task 7
- [x] `semantic.change_sets`, `change_set_items`, `catalog_revisions` + migration + RLS; lifecycle state machine with a transition table.
- [x] `content_hash`; `base_revision` check under a per-tenant row lock → `Superseded`.
- [x] Field edits publish as one-item sets (Draft→…→Active in one call, self-approval in evidence); zero-participant activation documented in code and ADR.
- [x] Outbox `enterprise.semantic.change_set.published.v1`; the field-changed event is still emitted.
- [x] Tests: transition table (illegal moves refused), hash stability, stale base → Superseded, concurrent publish, replay, RLS.
→ Commit: `812cdcf` "feat(semantic-catalog): publish field edits as one-item change sets (C-2)"
Note: the concurrency test found that the one-item flow read the base revision before taking the lock, so concurrent edits superseded each other; fixed in the same commit (`authorUnderLock`), see the ADR implementation notes. SemanticCatalog.Tests: 200; the other suites unchanged (Host 405, CRM 334, Messaging 49).

## A-1 — `reference` field type
### Task 8
- [x] `FieldType.Reference` (`reference` on the wire) with a fixed target in config; v1 target `masterdata/party`.
- [x] Write: id must be Accessible via `ILinkTargetDirectory` else `422 invalid_reference`; read hydrates `{id,label,accessible}` at the reader's authorization.
- [x] Web: definition editor + form control + detail rendering; tests incl. denied/unavailable.
→ Commit: `d13f310` "feat(semantic-catalog): add reference fields pointing at a customer (A-1)"
Note: tests added — SemanticCatalog.Tests 209, CRM.Tests 353, Host.Tests 407, web 754 (incl. the settings editor). The role-overlap check found `update_custom_fields` and `party.search` in the same write set (no writer is locked out) but `party.search` absent from the read set, so a viewer sees references as unavailable: documented in the ADR as an owner decision (template v4 + reseed, K3). Merged parties read as their survivor; also in the ADR.

## A-2 — Shared table views + `dependency_edges`
### Task 9
- [ ] `semantic.view_definitions`, `dependency_edges` (computed at publish), view items in ChangeSets.
- [ ] API: list/read shared views (`crm.opportunity.read`-gated like the list), manage via settings; impact endpoint lists dependent views.
- [ ] Web: view picker on the opportunity list; settings UI for shared views; deprecate dialog shows dependent views.
- [ ] Tests incl. field deprecation with a dependent view.

## Task 10 — Wrap-up
- [ ] Browser E2E on the dev DB; code review; schema doc, AGENTS.md status, phase table, memory; report to the owner (Turkish), listing Task 5 of event consumption as still deferred.
