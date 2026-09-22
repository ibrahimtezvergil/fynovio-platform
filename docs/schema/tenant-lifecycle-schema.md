# Tenant Lifecycle schema

`TenantLifecycle` owns the active tenant's identity and operating defaults. It does **not** own
the legal-entity hierarchy inside a tenant (that remains the future `Organization` module), nor
tenant-network membership (`docs/schema/tenant-network-schema.md`).

## Revision 1 — Company settings foundation

### `tenant_profiles`

One profile exists for each provisioned tenant. `tenant_id` is both the tenant boundary and the
natural primary key: there is no separate profile identifier to leak into commands or URLs.

| Column | Type | Constraint / meaning |
|---|---|---|
| `tenant_id` | `bigint` | PK, `CHECK (tenant_id > 0)` |
| `display_name` | `varchar(120)` | `NOT NULL`; trimmed, single-line, 2–120 characters |
| `legal_name` | `varchar(160)` | nullable; trimmed, single-line when supplied |
| `tax_number` | `varchar(32)` | nullable; trimmed, single-line when supplied |
| `tax_office` | `varchar(120)` | nullable; trimmed, single-line when supplied |
| `email` | `varchar(254)` | nullable; normalized lower case by the command |
| `phone` | `varchar(32)` | nullable; trimmed |
| `address` | `varchar(500)` | nullable; multiline allowed |
| `timezone` | `varchar(64)` | `NOT NULL`, IANA identifier accepted by the application |
| `currency_code` | `char(3)` | `NOT NULL`, uppercase ISO 4217-shaped `CHECK` |
| `row_version` | `bigint` | `NOT NULL`; optimistic concurrency token |
| `created_at` | `timestamptz` | `NOT NULL` |
| `updated_at` | `timestamptz` | `NOT NULL` |

`display_name`, `legal_name`, `tax_number`, and `tax_office` carry the PostgreSQL single-line
check `btrim(value) = value AND value !~ '[\\r\\n]'`; database checks protect the invariant even
when a future transport bypasses today's API.

### Supporting records

`outbox_messages` and `idempotency_records` follow the module-local shape already used by CRM,
Access, and Collaboration. Both carry `tenant_id`; `idempotency_records` has an index on
`expires_at`. A profile update commits the profile row, one past-tense outbox fact, and its
idempotency row in the same `SaveChanges()` call.

### Isolation

All three tables use `tenant_lifecycle` schema and have `ENABLE ROW LEVEL SECURITY` plus `FORCE
ROW LEVEL SECURITY`. The policy uses:

```sql
tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint
```

The policy applies to both `USING` and `WITH CHECK`. Integration tests connect as `fynovio_app`,
not the migration owner.

## API surface

| Method | Path | Action | Contract |
|---|---|---|---|
| `GET` | `/company/settings` | `tenant.settings.view` | Current tenant profile + row version |
| `PUT` | `/company/settings` | `tenant.settings.update` | Full replacement, `expectedVersion` in body, `Idempotency-Key` required |

There is deliberately no tenant id in a body, URL, or query string. The Host derives it from the
authenticated actor. `GET` returns 404 when provisioning has not created the tenant profile;
`PUT` returns 409 on stale version and replays a completed request by idempotency key.

