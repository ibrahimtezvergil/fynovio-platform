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
- [ ] `Contracts`: `FieldType`, `FieldStatus`, `FieldOption`, `FieldConfig`, `FieldDefinition` read model, `ISemanticDefinitionReader` (`ListFieldsAsync`, `GetFieldAsync`) scoped to `(tenant, owner_context, object_type)`.

### Task 2 — Module, persistence, migration
- [ ] `src/Modules/SemanticCatalog` project + `SemanticCatalogDbContext` (schema `semantic`): `field_definitions` (CHECKs carried over from tier 1), `idempotency_records`, `evidence_records`, `outbox_messages`; RLS ENABLE + FORCE.
- [ ] Migration copies `crm.tenant_field_definitions` when it exists (ids preserved, sequence advanced); runtime/relay grants; README migration order; slnx.

### Task 3 — Move the write side
- [ ] Domain (`FieldDefinition` entity + config validation) and `ManageFieldDefinitionHandler` move to the catalog, same limits/errors; endpoints unchanged; authorization via the `owner_context` action map (reuse `crm.settings.*`).
- [ ] `ISemanticDefinitionReader` implementation.

### Task 4 — Switch CRM to the reader
- [ ] The five CRM readers (create, update-values, list, impact, activity) and the two value validators use the reader (list-definitions moves to the catalog with the write side); `CustomFieldValues` / `CustomFieldFilter` operate on the contract read model; `CrmDbContext` loses the definitions DbSet.
- [ ] CRM migration drops `crm.tenant_field_definitions` (guarded: refuses while rows exist that the catalog lacks).

### Task 5 — Outbox, relay, roles
- [ ] `OutboxSources` + `semantic`; Messaging RLS migration; relay/runtime role scripts; `RelayRoleTests`; Host/Worker registration; Host fixtures migrate the new context.

### Task 6 — Tests and verification
- [ ] `tests/SemanticCatalog.Tests` (domain, persistence, RLS isolation, idempotency, evidence/outbox, migration copy); CRM tests use a stub reader.
- [ ] Full .NET + web suites green; EF model check clean; dev DB migrated in the documented order and the browser check repeated.

## C-2 — ChangeSet v1 for fields
### Task 7
- [ ] `semantic.change_sets`, `change_set_items`, `catalog_revisions` + migration + RLS; lifecycle state machine with a transition table.
- [ ] `content_hash`; `base_revision` check under a per-tenant row lock → `Superseded`.
- [ ] Field edits publish as one-item sets (Draft→…→Active in one call, self-approval in evidence); zero-participant activation documented in code and ADR.
- [ ] Outbox `enterprise.semantic.change_set.published.v1`; the field-changed event is still emitted.
- [ ] Tests: transition table (illegal moves refused), hash stability, stale base → Superseded, concurrent publish, replay, RLS.

## A-1 — `reference` field type
### Task 8
- [ ] `FieldType.Reference` (`reference` on the wire) with a fixed target in config; v1 target `masterdata/party`.
- [ ] Write: id must be Accessible via `ILinkTargetDirectory` else `422 invalid_reference`; read hydrates `{id,label,accessible}` at the reader's authorization.
- [ ] Web: definition editor + form control + detail rendering; tests incl. denied/unavailable.

## A-2 — Shared table views + `dependency_edges`
### Task 9
- [ ] `semantic.view_definitions`, `dependency_edges` (computed at publish), view items in ChangeSets.
- [ ] API: list/read shared views (`crm.opportunity.read`-gated like the list), manage via settings; impact endpoint lists dependent views.
- [ ] Web: view picker on the opportunity list; settings UI for shared views; deprecate dialog shows dependent views.
- [ ] Tests incl. field deprecation with a dependent view.

## Task 10 — Wrap-up
- [ ] Browser E2E on the dev DB; code review; schema doc, AGENTS.md status, phase table, memory; report to the owner (Turkish), listing Task 5 of event consumption as still deferred.
