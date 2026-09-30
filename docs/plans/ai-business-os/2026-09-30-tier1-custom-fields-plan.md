# Tier-1 Custom Fields (Opportunity) — Implementation Plan

> **Standard (`CLAUDE.md` "Plan checkbox tracking"):** a step is marked `[x]` only once its commit exists, with a `→ Commit:` line under it.

**Goal:** a tenant can define its own Opportunity fields without a developer, and use them in the create form, the detail page and the list. Values are validated and deprecated fields cannot be written.

**Binding decisions:** `adr-tier1-custom-fields.md` (Accepted), `2026-09-30-owner-decisions.md` K1–K3 and OD-6, `adr-business-os-principles.md`.

**Out of scope (owner decision, 2026-09-30):** Semantic Catalog, ChangeSet, AI, workflow, generic object store, Party fields, network-scoped/locked fields, stored views and dependency edges.

**Build note:** build and test always with `-m:1 -nodeReuse:false` (sandbox MSBuild hang). Integration tests need Docker (`DOCKER_HOST=unix://$HOME/.docker/run/docker.sock`).

## Implementation choices made while planning (within the ADR)

The ADR leaves these open, so they are decided here:

- **Status of the opportunity:** custom field values can be edited in every lifecycle status **except archived**, in line with `EnsureNotArchived`. They are descriptive data, not lifecycle facts, so Won/Lost records stay editable.
- **Deprecated values on update:** an update replaces the whole object, but the server **carries existing deprecated values forward**. A request that names a deprecated key gets `422`. This keeps ADR decision 7 ("existing values stay readable") intact.
- **Immutable after creation:** `field_type`, `field_name` (key) and `decimal` scale. The ADR has no type-change path in v1.
- **Options:** can be added and relabelled. Removing an option means marking it `IsDeprecated`: existing values stay readable, but new writes are refused. An option key is never deleted.
- **Impact endpoint:** gated by `crm.settings.update`, since it counts across all opportunities, not only the caller's.
- **Dev reseed (K3):** the dev database is reset by the owner. It is destructive, so Claude does not run it. Steps are in the final report.

---

## Task 1 — Domain: definition model and value validator

**Files:** `src/Modules/CRM/Customization/*`, `tests/CRM.Tests/Domain/*`

- [ ] Expand `TenantFieldDefinition`:
  - fields: key, label, 11 value types, config (options, scale, min/max, max length), status, sort order, owner scope, row version;
  - operations: `Update`, `Deprecate`, `Reactivate`.
- [ ] `CustomFieldValues` validator: type/required/option/limit checks, unknown and deprecated key rejection, deprecated value carry-forward, 64 KB cap. Errors are collected per field.
- [ ] Domain tests.
- [ ] Commit.

## Task 2 — Persistence: configuration, migration, schema doc

**Files:** `TenantFieldDefinitionConfiguration.cs`, `CrmDbContext.cs`, generated migration, `docs/schema/crm-sales-schema.md`

- [ ] EF configuration: new columns plus CHECKs for key regex, type, status and owner scope. Id preallocation, same as the catalogs.
- [ ] `dotnet ef migrations add` (generated, expand-shaped). `has-pending-model-changes` must be clean.
- [ ] Schema doc revision, checked line by line.
- [ ] Integration test: CHECK constraints and RLS under the runtime role.
- [ ] Commit.

## Task 3 — Application: definitions and opportunity values

**Files:** `src/Modules/CRM/Application/*`, `tests/CRM.Tests/*`

- [ ] `ManageCustomFieldDefinitionHandler`: create, update, deprecate, reactivate. Uses `crm.settings.update`, idempotency, outbox and evidence. Key conflict is `409`; more than 100 active fields is `422`; Party is refused.
- [ ] `ListCustomFieldDefinitionsHandler` (`crm.settings.read`) and `GetCustomFieldImpactHandler` (`crm.settings.update`).
- [ ] New action `crm.opportunity.update_custom_fields` (catalog). CRM template **v3** with the action in the write set (K3).
- [ ] `CreateOpportunity`: optional `CustomFields`, validated and included in the request hash.
- [ ] `UpdateOpportunityCustomFieldsHandler`: `expectedVersion`, idempotent, outbox `enterprise.crmsales.opportunity.custom_fields_changed.v1` (changed keys only).
- [ ] `OpportunityDto` and `OpportunitySummaryDto` expose `customFields`.
- [ ] Handler tests: happy path, replay, validation errors, deprecated, archived, denial, concurrency.
- [ ] Commit.

## Task 4 — Host: endpoints and error mapping

**Files:** `src/Host/Endpoints/*`, `tests/Host.Tests/*`

- [ ] `GET/POST/PUT /crm/settings/custom-fields`, `POST .../{id}/deprecate|reactivate`, `GET .../{id}/impact`.
- [ ] `PUT /opportunities/{id}/custom-fields`; `POST /opportunities` accepts `customFields`.
- [ ] `422 custom_field_invalid` with per-field errors, and `409 custom_field_key_conflict`.
- [ ] Host tests (HTTP → handler → DB).
- [ ] Commit.

## Task 5 — Web: settings, form, detail, table

**Files:** `web/src/features/crm-settings/*`, `web/src/features/opportunities/*`, `web/src/api/*`

- [ ] Zod schemas and API client for definitions and custom-field updates.
- [ ] Settings: custom field list with create/edit dialog, deprecate with an impact dialog, and reactivate.
- [ ] Metadata-driven `CustomFieldInput` / `CustomFieldValue` renderer shared by form, detail and table.
- [ ] Create form section, detail section (edit + read-only deprecated values), list columns.
- [ ] Vitest tests.
- [ ] Commit.

## Task 6 — Verification and review

- [ ] `dotnet build`, `dotnet format --verify-no-changes`, full `dotnet test`, `npm run lint && npm run type-check && npm test`.
- [ ] Independent code review (`requesting-code-review`), with the findings verified.
- [ ] Final report: changes, architecture delta, test evidence, next-phase decisions.
