# Semantic Catalog schema (`semantic`)

**Module:** `src/Modules/SemanticCatalog` · **Context:** `SemanticCatalogDbContext` · **Decisions:** `docs/plans/ai-business-os/adr-semantic-catalog-changeset.md` (OD-4, OD-5, OD-6).
**Revision 1 (2026-10-01):** field definitions moved here from `crm.tenant_field_definitions` (behavior-preserving; C-1).

The catalog owns *definitions* (what a tenant's custom fields are). Values stay on the owning aggregate (OD-6). Other modules read definitions through `Contracts.ISemanticDefinitionReader`; they never see this schema.

Every table has `ENABLE` + `FORCE ROW LEVEL SECURITY` with the standard `tenant_isolation` policy (`app.tenant_id`). `evidence_records` is append-only for `fynovio_app` (`REVOKE UPDATE, DELETE`).

## `semantic.field_definitions`
| Column | Type | Notes |
|---|---|---|
| `id` | bigint identity | **Preserved** from `crm.tenant_field_definitions` by the data move. |
| `tenant_id` | bigint | RLS key. |
| `owner_context` | varchar(32) | `crm` (CHECK; widened with a second owner). |
| `object_type` | varchar(32) | `opportunity` (CHECK). Party is not allowed (OD-6). |
| `key` | varchar(63) | Immutable. CHECK `^[a-z][a-z0-9_]{1,62}$`. |
| `label` | varchar(100) | Editable. |
| `field_type` | varchar(16) | `text`, `long_text`, `number`, `decimal`, `boolean`, `date`, `select`, `multi_select`, `email`, `phone`, `url` (CHECK). |
| `is_required` | bool | |
| `config` | jsonb | Object (CHECK). Options, scale, min/max, max length. |
| `status` | varchar(16) | `Active` \| `Deprecated` (CHECK). |
| `sort_order` | int | 0–10000 (CHECK). |
| `row_version` | bigint | Optimistic concurrency token. |
| `created_at`, `updated_at` | timestamptz | |

Unique: `(tenant_id, owner_context, object_type, key)`. The former `owner_scope` column is gone: it only ever held `Tenant`; a `Network` scope returns together with the Organization resolver that evaluates it.

## `semantic.idempotency_records`, `semantic.evidence_records`, `semantic.outbox_messages`
Same shape as every other module (state + outbox + evidence + idempotency commit together). The outbox is the **sixth relay source** (`Messaging.OutboxSources`); event type `enterprise.crm.custom_field_definition.changed.v1` is unchanged, `source` is now `semantic-catalog`.

## Migration order
`MasterData → SemanticCatalog → CRM → Access → Collaboration → TenantLifecycle → Messaging`. The initial catalog migration copies `crm.tenant_field_definitions` Opportunity rows **if the table exists** (ids kept, sequence advanced; Party rows are not copied). CRM's `MoveFieldDefinitionsToSemanticCatalog` then drops the table and refuses to if the catalog lacks any row. Pre-cutover `ManageCustomFieldDefinition:*` idempotency records and the historical evidence/outbox rows stay in `crm`.
