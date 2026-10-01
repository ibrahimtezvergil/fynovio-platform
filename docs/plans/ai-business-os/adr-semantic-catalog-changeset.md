# ADR: Semantic Catalog, ChangeSet v1, `reference` fields and shared table views (phase 2: A + C)

**Status:** Accepted — 2026-10-01 under the owner's delegated go-ahead ("bu iş kapsamın ne gerekiyorsa benim yerime yap", then "başla"). Every decision marked *decided on the owner's behalf* below was made without a fresh question and can be reopened; none of them touches an owner decision that is already recorded (OD-4, OD-5, OD-6, OD-7, K3).
**Related:** `2026-09-30-owner-decisions.md` (OD-4, OD-5, OD-6, OD-7), `adr-tier1-custom-fields.md` (decision 1: "definitions move to the Semantic Catalog … values stay in CRM"; decision 12: `dependency_edges` arrive with the first stored view), `adr-business-os-principles.md`, `adr-event-consumption.md` (E-6: a new outbox is a new relay source). Plan: `2026-10-01-semantic-catalog-plan.md`.

## Context

Tier 1 left definitions in `crm.tenant_field_definitions`, edited in place by seven CRM call sites. That has three limits the owner already accepted as the direction:
- a definition has no history and no review step — an edit is live the moment it is saved (OD-4 wants *nothing usable before `Active`*);
- nothing can say "this view depends on that field", so deprecating a field cannot show what it breaks (ADR tier-1, decision 12);
- the only relation a field can express is a scalar. Doc 15 §3 / ADR tier-1 decision 3 reserved `reference` for when a relation target exists.

## Order of work (deviation from B → A → C)

The owner's agreed order was B → A → C. A (`reference` fields, shared views) needs a place to store view definitions and a change process for them, which is exactly what C builds. Building A in CRM first would mean moving it a week later. So **C's foundation comes first and A is built inside the catalog** (C-1, C-2, A-1, A-2 in the plan). Nothing the owner accepted changes; only the sequencing does.

## Decisions

### S-1 — A `SemanticCatalog` module owns definitions (OD-5)
*Decided on the owner's behalf* — naming only. New module `src/Modules/SemanticCatalog`, schema `semantic`, `SemanticCatalogDbContext`, same per-module shape as the others (own `idempotency_records`, `evidence_records`, `outbox_messages`; RLS ENABLE + FORCE on every table). `Contracts` gets the read side: `ISemanticDefinitionReader` and plain read-model records (`FieldDefinition`, `FieldConfig`, `FieldOption`, `FieldType`, `FieldStatus`). **Consumers never see the catalog's DbContext or entities** — CRM reads definitions through the interface and keeps validating *values* itself (OD-6: values stay with their aggregate).

Definition identity is `(tenant, owner_context, object_type, key)`; for fields in this phase the owner is `crm` / `opportunity`. **Row IDs are preserved** by the data move, so every id in the API and the web UI stays valid.

### S-2 — Behavior-preserving move first (C-1)
C-1 moves the definitions and the write handler to the catalog with **no change in behavior**: same endpoints (`/crm/settings/custom-fields…`), same limits, same validation, same errors, the whole existing suite green. Edits are still immediate in C-1; the lifecycle arrives in C-2. This makes the move reviewable on its own.

### S-3 — Migration order: SemanticCatalog **before** CRM *(decided on the owner's behalf)*
The `semantic` migration copies `crm.tenant_field_definitions` rows (ids preserved, sequence advanced) **if that table exists**; a later CRM migration drops it, and refuses to drop while rows exist that the catalog does not hold. Order is `MasterData → SemanticCatalog → CRM → Access → Collaboration → TenantLifecycle → Messaging`; it works on a fresh database (nothing to copy) and on an existing one (copy, then drop). No production data exists, so no dual-write window is built.

### S-4 — Authorization reuses the CRM settings actions *(decided on the owner's behalf)*
Managing definitions is `crm.settings.update`, reading is `crm.settings.read`, exactly as in tier 1. The catalog maps `owner_context → (read action, write action)` in code; an unknown owner is refused. **No capability-template change and no reseed** — the K3 rule (no new in-place template exception) is respected by construction. A catalog-wide manifest (`semantic.*` actions) is deferred until a second owner context exists.

### S-5 — ChangeSet v1 (OD-4, OD-5)
A **ChangeSet** is a catalog-owned, versioned bundle of definition changes with this lifecycle:

`Draft → Validated → AwaitingApproval → Approved → Published → Activating → Active | ActivationFailed`, plus the side exits `Rejected`, `Discarded` (from Draft/Validated/AwaitingApproval) and `Superseded`.

- **Atomicity (OD-4):** ACID only inside the catalog. `Published` applies the items to the definition tables and bumps the tenant's catalog revision in one transaction, and writes the outbox fact `enterprise.semantic.change_set.published.v1`.
- **`content_hash`:** SHA-256 over the canonical JSON of the items. The same logical change always hashes the same, which makes replays and "is this the set I approved?" checkable.
- **`base_revision`:** the tenant catalog revision the set was authored against. Publishing takes a per-tenant row lock on `semantic.catalog_revisions`; if the revision has moved, the set becomes `Superseded` instead of publishing — a stale bundle never overwrites a newer definition.
- **Source:** `Human` only in v1 (an `AiAssistant` source arrives with phase 3). **Approval policy:** a settings edit by a principal who holds the write action is a *one-item set that passes Validated → AwaitingApproval → Approved → Published in one call*, the self-approval recorded in evidence. Separate approver, multi-item drafts and review UI are not built; the states and the transition table are, so they can be.
- **Activation:** `Published → Activating → Active | ActivationFailed`. A change set's *participants* are the modules that must act on it (none for field changes: CRM reads definitions at use). **With zero participants the set goes straight to `Active` inside the publish transaction** — the only honest reading of "nothing is usable before Active" when there is nothing to wait for. When the first participant exists, publish stops at `Activating` and the participant's acknowledgement completes it.
- **A ChangeSet carries metadata only (OD-7):** definitions, view layouts, option labels. Never record values, never secrets.

### S-6 — `reference` field type (A-1; OD-6 is untouched)
A `reference` field points at **one entity type that already has a link-target resolver** (`ILinkTargetResolver`, the Collaboration mechanism). v1 target: `masterdata/party`, the only one with an opportunity-facing meaning. The stored value is the target id only (JSON number); the target is fixed in the field's config at creation and never changes (like decimal scale).
- **Write:** the id must resolve to `Accessible` for the writer through `ILinkTargetDirectory`; anything else is `422 invalid_reference` (unknown, other tenant and denied are deliberately indistinguishable).
- **Read:** the response hydrates `{ id, label, accessible }` at the *reader's* authorization; an inaccessible target shows as unavailable and never leaks its label. Nothing is hydrated from storage — the label is never copied into the opportunity.
- **OD-6 is not touched:** the Party's *own* values stay in MasterData and Party still cannot own custom fields. An opportunity merely stores an id that points at a Party.
- No cascade: a reference to a Party that later disappears reads as unavailable.

### S-7 — Shared table views and `dependency_edges` (A-2)
A **ViewDefinition** (kind `table`) is a tenant-shared list layout: an ordered set of columns, each either a built-in column key or a custom-field key, plus a default sort. It is created and changed **only through a ChangeSet** (item kind `view`). `semantic.dependency_edges (tenant, from_kind, from_id, to_kind, to_id)` is *computed at publish* from the view's content, in the same transaction, never hand-edited. Consequences:
- the deprecate dialog lists the views that use the field (the data-impact count from tier 1 stays);
- a ChangeSet that deprecates a field used by a view is allowed (deprecation is soft and reversible) but the impact is shown and recorded; a ChangeSet that **removes** a field is not a v1 operation, as before;
- a deprecated field disappears from rendering in a view and the view keeps working.
Not built: per-user views, filters stored in views, other view kinds (board, calendar), view permissions beyond "who can read opportunities".

### S-8 — Outbox, relay and roles
The catalog's outbox is a **sixth relay source**: `OutboxSources.All` gains `semantic`; `scripts/create-relay-role.sql` and `create-runtime-role.sql` gain the schema and column grants; the Messaging RLS migration gains the `relay_access` policy for `semantic.outbox_messages`; `RelayRoleTests` and the migration README order are updated. Event types: `enterprise.semantic.change_set.published.v1` (change set id, revision, item count, kinds — no definition content beyond keys) and the existing `enterprise.crm.custom_field_definition.changed.v1` is kept for the one-item field flow so existing consumers see no change.

## Not decided here (and not built)
- `semantic.*` capability manifest and a separate approver role (needs a second owner context).
- Doc 15 "tier 2" meant the **intake flow** (`OpportunityCreationSteps`); that is a different thing from `reference` fields and is **not** done by this phase.
- AI-authored change sets (phase 3), semantic metrics (phase 4), workflow (5b), business actions (6).
- Task 5 of event consumption (redeliver command) stays deferred to the owner: (a) a `messaging` manifest or (b) an operator Worker command (recommended).

## Cutover notes
- Existing `ManageCustomFieldDefinition:*` idempotency records stay in `crm.idempotency_records`; a replay inside the 1-day TTL right after the cutover re-executes instead of replaying (harmless: no production data).
- Historical evidence and outbox rows for `TenantFieldDefinition` stay in `crm`.
- The catalog cannot call CRM code, so it carries its own copy of the settings authorization (same `ResourceDescriptor` and action keys), and an agreement test proves it matches `GetCrmSettingsHandler.AuthorizeAsync`.
- Definition-side validation (`FieldConfig.Validate`, option rules) lives only in the catalog; CRM keeps only the *value-side* helpers it needs, answered by the read model (length caps, option activity, decimal scale).
- The reader opens its own transaction and sets `app.tenant_id` itself; its test runs as the runtime role, not through an admin context.

## Consequences
- One more module, one more schema, one more outbox source — the cost of OD-5.
- Definition edits get history, a content hash, stale-write protection and an impact view, without any capability-template change.
- CRM loses its definition tables and write handler but keeps value validation; its tests that created definitions through its own DbContext move to the catalog or use a stub reader.

## Implementation notes (C-2, 2026-10-01)
- **The base revision of a settings edit is read under the tenant lock.** A one-item set is drafted and published in one transaction, so `ChangeSetPublisher.DraftAsync(..., authorUnderLock: true)` takes the `catalog_revisions` lock *before* it reads the revision it authors against. Reading it first and locking at publish time (the first implementation) made concurrent edits for one tenant supersede each other; the concurrency test caught it. A draft that waits for review reads the revision unlocked, which is what the publish-time stale check is for.
- **Evidence:** one `ChangeSet.Published` (or `ChangeSet.Superseded`) record per set, carrying the transitions walked, the content hash, the base and published revisions and `approval: "self"`; the per-definition `TenantFieldDefinition.*` evidence and outbox facts of tier 1 are kept unchanged (the evidence now also names `changeSetId`).
- **API:** the settings responses gain an additive `changeSetId`; nothing else on the wire changed.
- **Not built (as decided):** a draft/review API, multi-item sets from the UI, an approver other than the author. `ChangeSetPublisher` is public and tested with multi-item and stale sets so those can be added without reshaping the engine.

## Implementation notes (A-1, 2026-10-01)
- **Verification only for changed values.** A custom-field update replaces the whole object, so a stored reference the writer can no longer see must not block an unrelated edit. `CustomFieldReferences.Changed` compares the normalized object with the stored one and only new or different ids go to `ILinkTargetDirectory`; `invalid_reference` (422, the existing `custom_field_invalid` shape) is returned per field, and unknown / other-tenant / denied are indistinguishable.
- **Hydration is additive and per reader.** Detail and list responses gain `customFieldReferences: { key: { id, accessible, label } }`; `customFields` still holds the bare id. Hydration happens only after an allowed read decision, in one directory call per list page, and an unavailable target returns the id without a label. Labels are never stored.
- **Merged parties:** the Party resolver answers a merged party's survivor label under the stored id. Accepted: the stored value is not rewritten by a merge, and it reads as the survivor until the field is edited.
- **A reader without `crm.reference.party.search` sees a reference as unavailable.** Today only the CRM *write* set carries that action; the read set (`crm_opportunity_read`) does not, so a viewer reads the id but never a label. Fixing it means adding the action to the read set, which is a template content change (K3: a version bump and reseed, no in-place edit) — **left as an owner decision**. Writers are unaffected: `update_custom_fields` and `party.search` are in the same write set, so no role can edit a reference field yet be unable to verify it.
- **Allow-list vs. resolvers:** `ReferenceTargets.Allowed` is the catalog's allow-list; a Host test asserts every entry has a registered `ILinkTargetResolver` (otherwise every write of that reference would be a silent 422).
- **Web:** the field kit stays entity-agnostic — `CustomFieldInputs` takes a `renderReference`; the opportunities feature passes a Party-picker control. A stored reference this reader cannot see renders as unavailable, is kept on save, and is replaced only by an explicit clear. The definitions list schema is a closed enum, so the web change ships with the backend one.

