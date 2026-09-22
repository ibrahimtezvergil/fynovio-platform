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

The generated migration names every row invariant explicitly: `ck_tenant_profiles_tenant_id`,
`ck_tenant_profiles_display_name`, `ck_tenant_profiles_legal_name`,
`ck_tenant_profiles_tax_number`, `ck_tenant_profiles_tax_office`,
`ck_tenant_profiles_email`, `ck_tenant_profiles_phone`, `ck_tenant_profiles_address`,
`ck_tenant_profiles_timezone`, and `ck_tenant_profiles_currency_code`. Single-line values require
`btrim(value) = value` and reject all PostgreSQL control and Unicode line-separator characters;
`address` permits CR/LF only. `email` must already be lower-case and trimmed. `timezone` is
trimmed, single-line and is then validated by the command with the runtime IANA time-zone
database; `currency_code` matches `^[A-Z]{3}$`. Each named check has a persistence test.

### Supporting records

`outbox_messages` and `idempotency_records` follow the module-local shape already used by CRM,
Access, and Collaboration. Both carry `tenant_id`; `idempotency_records` has an index on
`expires_at`. A profile update commits the profile row, one past-tense outbox fact, and its
idempotency row in the same `SaveChanges()` call. The event is
`enterprise.tenant-lifecycle.tenant-profile.updated.v1`, with source
`/enterprise/tenant-lifecycle`, subject `tenant-profiles/{tenantId}`, and a payload limited to
`tenantId`, `rowVersion`, and `updatedAt` (no contact or legal-identity data).

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
`PUT` returns 409 on stale version and replays a completed request by idempotency key. It is a
full replacement: every field is required in the JSON shape; nullable fields use JSON `null` to
clear a value and omitted fields are a 400. The Host validates and canonicalizes that complete
shape before calculating the idempotency request hash.
