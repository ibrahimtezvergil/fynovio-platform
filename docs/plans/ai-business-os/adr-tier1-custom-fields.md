# ADR: Tier-1 custom field data shape (Opportunity)

**Status:** Accepted — 2026-09-30 by the platform owner (K2, adopted as written). It closes the open follow-up in doc 15 §7 ("detailed data shape for tier 1 … needs a short ADR once the CRM module gets its first real fields").
**Date:** 2026-09-30
**Related:** `2026-09-30-architecture-reconciliation.md` §F, §G, §N; doc 15 §3, §6, §7; `docs/schema/crm-sales-schema.md` (tier-1 section).

## Precondition

**Resolved (K1, 2026-09-30):** the owner accepted doc 15 tier 1, tier 2 and the direction of §6 (network scope and field locking). Acceptance does not mean implementation: only this tier-1 slice is implemented now.

## Context

What exists today [V]:
- `crm.tenant_field_definitions`: `id, tenant_id, aggregate_type (Party|Opportunity), field_name, field_type (text|number|boolean|date), is_required, created_at`. Unique on `(tenant_id, aggregate_type, field_name)`. RLS enabled (`20260916081636_EnableRowLevelSecurity`).
- `crm.opportunities.custom_fields jsonb`, nullable.
- No handler reads or writes either one. `CreateOpportunityCommand` has no custom-field input, and there is no general opportunity update command.
- There is no production data, so the table can be reshaped with an ordinary expand-shaped migration.

## Decisions

1. **Storage:** values live in the owning aggregate's `custom_fields jsonb` as a single JSON object, not in a separate value table (not EAV). Definitions live in `crm.tenant_field_definitions` for now. In reconciliation stage 2 they move to the Semantic Catalog, and CRM then reads them through a `Contracts` interface. Values stay in CRM.
2. **Key:** `field_name` is the field's **immutable key**, with a CHECK of `^[a-z][a-z0-9_]{1,62}$`. The JSON object is keyed by it. A display `label` is added and is editable. Changing the key is not supported: it is deprecate, then new field, then migrate (same philosophy as `ActionKey`).
3. **Types (v1):** `text` (≤ 2000 chars), `long_text` (≤ 10000), `number` (integer), `decimal` (fixed `scale` in config, 0–6), `boolean`, `date` (`YYYY-MM-DD`), `select` and `multi_select` (stable `option_key` values; labels editable), `email`, `phone`, `url`. **No money type:** money needs a declared rounding point, which belongs to a typed aggregate (`AGENTS.md` money rule). `reference` comes later, with `RelationDefinition`.
4. **JSON encoding:** `number` and `decimal` values are JSON numbers, with Postgres `jsonb` keeping them as arbitrary-precision numeric. `date` values are strings. `multi_select` values are arrays of option keys. A missing key means null. Unknown keys are rejected on write and ignored on read.
5. **New columns:** `label`, `config jsonb` (options, scale, min/max, max length), `status` (`Active | Deprecated`, CHECK), `sort_order`, `owner_scope` (CHECK `= 'Tenant'`, per doc 15 §6: every tier-1 record carries its scope, and `Network` is added together with its resolver in the Organization module), `row_version`, `updated_at`. `is_locked` is **not** added yet: it only has meaning for `Network`-scoped records, and the project rule is to add a value together with the code that evaluates it.
6. **Limits:** at most 100 active fields per aggregate type per tenant, at most 50 options per select field, and a 64 KB cap on the `custom_fields` payload. These are enforced in the handler.
7. **Validation rules:** writes are validated against the currently published definitions: type, required, options, limits, and "not deprecated".
   - **Required:** enforced on create, and on every custom-field update (the full object is validated). Existing records are not retroactively invalid. A newly required field is only demanded on the record's next custom-field write, and the settings screen shows how many records lack it.
   - **Deprecated:** the field is hidden from forms, and writing a value for it gets `422`. Existing values stay readable on the detail page. A deprecated field can be reactivated, since its key never disappears.
8. **Write path:**
   - `CreateOpportunity` gains an optional `CustomFields` input.
   - A new `UpdateOpportunityCustomFields` command applies a full replacement of the custom-field object with `expectedVersion`. It is idempotent, and writes state + outbox (`enterprise.crmsales.opportunity.custom_fields_changed.v1`, with changed keys only and never values) in one transaction.
   - Evidence is not required: the command is not risk-catalogued.
9. **Authorization:**
   - Managing definitions uses the existing `crm.settings.update`.
   - Reading definitions uses `crm.settings.read`, which is already in the read set, so every CRM reader can render the form.
   - Writing values uses a new action `crm.opportunity.update_custom_fields` in the write set. This is a template content change. **Decided (K3):** the CRM template is bumped to **v3** and the development tenants are reseeded. No new in-place exception is made; the `party.create` exception stays a one-off.
10. **Party:** the `Party` value of `aggregate_type` stays in the CHECK for now, but the API refuses to create Party definitions until OD-6 is decided (Party lives in MasterData, which has no `custom_fields` column).
11. **Querying and indexing:** v1 adds no indexes on custom fields. A list filter on a custom field (if added) runs as a `jsonb` predicate. An expression index is added only when a measured slow query justifies it.
12. **Impact before deprecation:** v1 has no stored views that reference individual fields (the form and list render every active field by `sort_order`), so no dependency edges exist yet. The deprecate dialog shows the **data impact**: the number of opportunities holding a value. `dependency_edges` arrives with the first stored `ViewDefinition`.

## Consequences

- Tenants can define their own opportunity fields without a deploy. The form, detail page and list all render from definitions.
- One definition model that moves to the Semantic Catalog without changing the value storage.
- Expand-shaped migration only. The existing table is unused, so the change is fully reversible.
- Not covered: Party fields (OD-6), network-mandated/locked fields, stored views and dependency edges, AI.
