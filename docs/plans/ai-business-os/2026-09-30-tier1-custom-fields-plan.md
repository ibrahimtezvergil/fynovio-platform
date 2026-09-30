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

- [x] Expand `TenantFieldDefinition`:
  - fields: key, label, 11 value types, config (options, scale, min/max, max length), status, sort order, owner scope, row version;
  - operations: `Update`, `Deprecate`, `Reactivate`.
- [x] `CustomFieldValues` validator: type/required/option/limit checks, unknown and deprecated key rejection, deprecated value carry-forward, 64 KB cap. Errors are collected per field.
- [x] Domain tests.
- [x] Commit.
  → Commit: `9766ab1` "feat(crm): tier-1 custom field definition model and value validator" (pre-existing main build break fixed first: `3a7e787` "fix(crm-tests): drop duplicate ResolveDisplayNamesAsync from principal directory stub")

## Task 2 — Persistence: configuration, migration, schema doc

**Files:** `TenantFieldDefinitionConfiguration.cs`, `CrmDbContext.cs`, generated migration, `docs/schema/crm-sales-schema.md`

- [x] EF configuration: new columns plus CHECKs for key regex, type, status and owner scope. Id preallocation, same as the catalogs.
- [x] `dotnet ef migrations add` (generated, expand-shaped).
  → Commit: `0101708` "feat(crm): persist expanded tenant field definitions"
  `has-pending-model-changes` → "No changes have been made to the model since the last migration." (run with `--startup-project src/Modules/CRM/CRM.csproj --no-build`; Host has no EF Design reference).
- [x] Schema doc revision, checked line by line.
  → Commit: `35012ba` "docs(schema): update TENANT_FIELD_DEFINITIONS to revision 11"
- [x] Integration test: CHECK constraints and RLS under the runtime role.
  → Commit: `18b86ba` "test(crm): persistence, constraint and RLS coverage for tenant field definitions" (follow-up fix: `a90fed3` "test(crm): assert the specific CHECK constraint name in tenant field tests")
- [x] Commit.
  Note: the implementer subagent ran on Haiku, so the trailer on `35012ba`/`18b86ba` reads "Claude Haiku 4.5". Full CRM suite 340/340 passed, re-run by the controller.

## Task 3 — Application: definitions and opportunity values

**Files:** `src/Modules/CRM/Application/*`, `tests/CRM.Tests/*`

- [x] `ManageCustomFieldDefinitionHandler`: create, update, deprecate, reactivate. Uses `crm.settings.update`, idempotency, outbox and evidence. Key conflict is `409`; more than 100 active fields is `422`; Party is refused.
- [x] `ListCustomFieldDefinitionsHandler` (`crm.settings.read`) and `GetCustomFieldImpactHandler` (`crm.settings.update`).
- [x] New action `crm.opportunity.update_custom_fields` (catalog). CRM template **v3** with the action in the write set (K3).
  → Commit: `d084527` "feat(crm): manage tenant custom field definitions" (follow-up fix: `b4cb5dc` "fix(crm): custom field definition handler replay, event keys and tests")
  Note: the subagent committed without running the tests, and all 11 failed. Fixed by the controller. Review notes accepted as-is:
  - The 100-active limit is checked in the application only. A concurrent race can reach 101, which is harmless for a product limit, and a CHECK cannot count across rows.
  - `status` stays `Active`/`Deprecated` (the DB values). Only `type` uses snake_case.
- [x] `CreateOpportunity`: optional `CustomFields`, validated and included in the request hash.
- [x] `UpdateOpportunityCustomFieldsHandler`: `expectedVersion`, idempotent, outbox `enterprise.crmsales.opportunity.custom_fields_changed.v1` (changed keys only).
- [x] `OpportunityDto` and `OpportunitySummaryDto` expose `customFields`.
- [x] Handler tests: happy path, replay, validation errors, deprecated, archived, denial, concurrency.
- [x] Commit.
  → Commit: `4cff695` "feat(crm): write and expose opportunity custom field values" (follow-up fix: `655de28` "refactor(crm): refuse archived custom field updates up front")
  Notes on how this step was done:
  - The controller implemented Task 3b directly, because Task 3a's subagent was unreliable.
  - A no-op update (no changed keys) keeps the row version and writes no outbox fact.
  - `ChangedKeys` compares values semantically, because jsonb reformats the JSON it stores.
  - The create hash appends the custom fields only when they are present, so requests without them keep their pre-existing hash.

## Task 4 — Host: endpoints and error mapping

**Files:** `src/Host/Endpoints/*`, `tests/Host.Tests/*`

- [x] `GET/POST/PUT /crm/settings/custom-fields`, `POST .../{id}/deprecate|reactivate`, `GET .../{id}/impact`.
- [x] `PUT /opportunities/{id}/custom-fields`; `POST /opportunities` accepts `customFields`.
- [x] `422 custom_field_invalid` with per-field errors, and `409 custom_field_key_conflict`.
- [x] Host tests (HTTP → handler → DB).
- [x] Commit.
  → Commit: `6916041` "feat(host): custom field definition and value endpoints"
  Notes:
  - The snake_case type-name mapping now lives in one place, `TenantFieldValueTypeNames`, and is used by both EF and the API. `has-pending-model-changes` reports no changes.
  - Pre-existing and unrelated: `PartyReferencesEndpointTests.Search_finds_...` fails on main, because `aaea0fc` added `phone` to the party search response without updating the test.

## Task 5 — Web: settings, form, detail, table

**Files:** `web/src/features/crm-settings/*`, `web/src/features/opportunities/*`, `web/src/api/*`

- [x] Zod schemas and API client for definitions and custom-field updates.
- [x] Settings: custom field list with create/edit dialog, deprecate with an impact dialog, and reactivate.
- [x] Metadata-driven `CustomFieldInput` / `CustomFieldValue` renderer shared by form, detail and table.
- [x] Create form section, detail section (edit + read-only deprecated values), list columns.
- [x] Vitest tests.
- [x] Commit.
  → Commit: `6177c86` "feat(web): metadata-driven opportunity custom fields" (backend follow-ups: `e104bfb` "fix(host): custom field 422 uses the shared errors map plus codes", `dc54bea` "fix(host): custom field type is read on create only")
  Deviations from the plan:
  - **Location.** The shared code lives in `web/src/lib/custom-fields/` and `web/src/components/custom-fields/`, not under `features/`. Features must not import each other (`web/AGENTS.md`), and both crm-settings and opportunities use it.
  - **Display.** Values are shown through the `formatCustomFieldValue` helper rather than a separate `CustomFieldValue` component.
  - **Editing.** Edits happen inline in the settings section, like the catalogs, rather than in a dialog.
  - **422 shape.** The response keeps the shared `errors` shape (field → messages) and adds `codes` (field → machine codes), so the UI can show the Turkish message for each code.
  - **Test setup.** `src/test/setup.ts` answers the definitions request with `[]` in every test by default. The handler is registered per test rather than in `handlers`, so the dev MSW never answers the real API.

## Task 6 — Verification and review

- [x] `dotnet build`, `dotnet format --verify-no-changes`, full `dotnet test`, `npm run lint && npm run type-check && npm test`.
  → Commit: `d892d8d` "style: dotnet format on custom field handler and host tests"
  Results:
  - `dotnet format --verify-no-changes` exits 0.
  - `dotnet test` (solution): 1448 of 1449 pass. The one failure, `PartyReferencesEndpointTests.Search_finds_...`, also fails on main: `aaea0fc` added `phone` to the party search response and did not update the test.
  - `npm run check`: 737/737 pass. `npm run build` passes.
  - Not done: a browser/visual check. It needs the dev DB reset (K3) and the owner's running stack.
- [x] Independent code review (`requesting-code-review`), with the findings verified.
  Result: the final review of the whole slice returned APPROVED. Of the earlier per-task findings:
  - Fixed: 3a replay, event keys and the IsActive query.
  - Refuted: 3a "status must be snake_case" and 3b "archived bypass". Neither holds against the code; the domain already enforced the archived rule.
- [x] Final report: changes, architecture delta, test evidence, next-phase decisions.
  → `2026-09-30-tier1-slice-report.md`
