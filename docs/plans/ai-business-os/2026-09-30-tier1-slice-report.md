# Tier-1 Custom Fields — Slice Report

**Branch:** `docs/ai-business-os-architecture` (not pushed) · **Plan:** `2026-09-30-tier1-custom-fields-plan.md` · **ADR:** `adr-tier1-custom-fields.md`

## 1. Changes

The slice lets a tenant define its own Opportunity fields, without a developer, and use them in the create form, the detail page and the list.

### Domain (`src/Modules/CRM/Customization/`)

- **`TenantFieldDefinition`:**
  - 11 value types: text, long_text, number, decimal, boolean, date, select, multi_select, email, phone, url.
  - Config: options, scale, min/max, max length.
  - Lifecycle: Active and Deprecated, with `row_version` and `owner_scope = Tenant`.
  - Key, type and decimal scale are immutable. An option is never removed, only deprecated.
- **`CustomFieldValues`:**
  - Checks type, required, options, range and length for each field.
  - Rejects unknown and deprecated keys.
  - Enforces a 64 KB cap.
  - Collects errors per field.
  - On update, carries deprecated and unknown stored values forward.

### Persistence

- An expand-shaped migration adds the new columns and the CHECK constraints: key regex, type, status, owner scope, sort order and config object.
- RLS was already in place.
- The schema doc is at revision 11.

### Application

- Definition handlers:
  - `ManageCustomFieldDefinitionHandler` (create/update/deprecate/reactivate) uses `crm.settings.update`, idempotency, outbox and evidence. At most 100 active fields per aggregate type; Party fields are refused.
  - `ListCustomFieldDefinitionsHandler` uses `crm.settings.read`.
  - `GetCustomFieldImpactHandler` uses `crm.settings.update` and counts via a jsonb key-exists check.
- Value writes:
  - `CreateOpportunity` accepts `CustomFields`. The values are included in the request hash.
  - `UpdateOpportunityCustomFields` does a full replacement with `expectedVersion` and is idempotent.
    - Outbox event: `enterprise.crmsales.opportunity.custom_fields_changed.v1`, carrying changed keys only, never values.
    - A no-op update bumps no version and writes no event.
    - An archived record is refused.
- New action `crm.opportunity.update_custom_fields`, placed in the write set. The CRM template is now **v3** (K3).
- `OpportunityDto` and `OpportunitySummaryDto` expose `customFields`.

### Host

- **Definitions:** `/crm/settings/custom-fields` supports GET and POST, `PUT {id}`, `POST {id}/deprecate|reactivate` and `GET {id}/impact`.
- **Values:** `PUT /opportunities/{id}/custom-fields`, and `POST /opportunities` accepts `customFields`.
- **Errors:**
  - `422 custom_field_invalid` returns `errors` (field → messages) and `codes` (field → machine codes).
  - A key conflict is `409 custom_field_key_conflict`.

### Web

- **Settings:** CRM settings has a new "Fırsat alanları" (Opportunity fields) section.
  - Create and edit a field. The key is derived from the Turkish label, and key and type are locked after creation.
  - Add options and deprecate them.
  - Deprecate a field through a dialog that shows the data impact; reactivate it later.
- **Shared renderer:** `lib/custom-fields` and `components/custom-fields` hold one metadata-driven renderer, used by:
  - the create form section;
  - the detail card, which is editable and shows deprecated values read-only;
  - the list columns.
- **Errors:** 422 codes are shown per field in Turkish. Required fields are checked before the request is sent.

## 2. Architecture delta

- **Principles kept:**
  - Values live in the aggregate's `custom_fields jsonb`, not in EAV.
  - The definition model can move into the Semantic Catalog later without changing how values are stored.
  - AI proposes, the kernel executes: there is no AI in this slice.
- **No change** to RLS, the PDP, idempotency, the outbox, evidence or the state machines. Every new write follows the same pattern.
- **Template v3:** tenants already enabled do not receive the new action. There is no reconciler and no in-place exception (K3), so dev tenants need a reset.
- **Shared snake_case mapping:** `TenantFieldValueTypeNames` is now the single mapping for the database and the API.
- **Web boundary:** shared code sits outside `features/`, in `lib` and `components`, per the "features do not import each other" rule.
- **Accepted limits:**
  - The 100-active-field limit is checked in the application only. A concurrent race can reach 101, and a CHECK constraint cannot count across rows.
  - The idempotency hash depends on the JSON's whitespace. A client resends the same body, so this is acceptable.

## 3. Test evidence (2026-09-30)

| Suite | Result |
|---|---|
| `dotnet test` (solution) | 1448 of 1449 pass |
| CRM.Tests | 363/363 |
| Access.Tests | 313/313 |
| Host.Tests | 401/402 |
| `dotnet format --verify-no-changes` | exit 0 |
| `has-pending-model-changes` | no changes |
| `npm run check` (lint + typecheck + vitest) | 737/737 |
| `npm run build` | pass |

- **Host failure:** the single Host failure is `PartyReferencesEndpointTests.Search_finds_...`. It also fails on main: `aaea0fc` added `phone` to the party search response without updating the test. This branch does not touch that code.
- **New tests:** domain, persistence (CHECK/RLS), handlers, host HTTP, and web (values, create/detail flows, settings panel, list columns).
- **Not done:** a browser/visual check. It needs a dev DB reset and a running stack.

## 4a. Follow-up status (2026-10-01)

The owner answered "do all of them". Status of each:

- **Party test:** fixed in `89543f5`. `dotnet test` now passes **1455/1455**.
- **List filter:** added in `f9a496d` (API, GIN index, schema rev 12) and `07956c2` (web pills). Details are in the plan's Task 7.
- **Dev DB reset:** done with the owner's permission. A backup was taken first. The database was recreated with every migration, the seed ran, and CRM is at template v3 in both tenants.
- **Browser check:** done.
  - Passed end to end: define field → create with value → edit on detail (no 403) → list pill filter → deprecate impact → read-only deprecated value.
  - Fixed one visual overlap (the deprecated badge).
- **Merge and push:** merged to main with `--no-ff` and pushed.
- **Next phase:** still open. Choose the order of A, B and C. A and C both reshape the definition model.

## 4. Decisions needed for the next phase

1. **Dev reseed (K3):** reset the dev DB. Until then, dev tenants cannot edit values: they lack the `update_custom_fields` action, so the write gets 403.
2. **Merge and push:** push the branch, then decide whether to merge to main or open a PR.
3. **Party test fix:** should the pre-existing party-search test failure be fixed on this branch or on main?
4. **Next phase:**
   - Option A: tier-2 (`RelationDefinition` / reference type, stored views + dependency edges).
   - Option B: first real event consumption, which is the prerequisite for the workflow ADR.
   - Option C: Semantic Catalog stage 2 (moving definitions into the Catalog).
5. **List filtering on custom fields:** leave out (current), or add a jsonb filter now?
