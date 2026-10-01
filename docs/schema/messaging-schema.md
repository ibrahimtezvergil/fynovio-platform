# Messaging schema (`messaging`)

Platform delivery ledger for published facts — not a business module. Decision record:
`docs/plans/ai-business-os/adr-event-consumption.md` (accepted 2026-10-01). Code: `src/Messaging`.
Migrated **after every module**: its RLS migration adds the relay policy to their outbox tables.

## Tables

### `event_deliveries`
One fact owed to one consumer. A pointer, never a copy of the payload.

| Column | Notes |
|---|---|
| `id` | identity |
| `tenant_id` | the fact's tenant; RLS key |
| `consumer` | consumer name (`IEventConsumer.Name`), ≤ 100 |
| `source_schema`, `source_id` | the outbox row (`<schema>.outbox_messages.id`) |
| `event_id`, `event_type` | copied pointer columns |
| `aggregate_type`, `aggregate_id`, `aggregate_version` | per-aggregate ordering |
| `status` | `pending` · `processing` · `delivered` · `skipped` · `dead` (CHECK) |
| `attempts`, `next_attempt_at` | backoff 5 s × 2^(n−1), cap 15 min; `dead` after 10 attempts |
| `locked_until` | lease while `processing`; an expired lease is reclaimable |
| `last_error` | exception type (+ SQLSTATE) only — never a message, detail or payload |
| `created_at`, `delivered_at` | |

Indexes: unique `(consumer, event_id)`; `(status, next_attempt_at)`; `(consumer, tenant_id, source_schema, aggregate_type, aggregate_id, aggregate_version, source_id)`.

### `consumer_registrations`
`consumer` (PK), `start_policy` (`FromBeginning` | `FromNow`), `registered_at`. A `FromNow` consumer gets facts that
occurred before `registered_at` fanned out as `skipped`. Not tenant data; relay role only.

## RLS and roles

- `event_deliveries`: `ENABLE` + `FORCE`, standard `tenant_isolation`, plus `relay_access`.
- `relay_access` (`USING / WITH CHECK (current_user = 'fynovio_relay')`) also exists on `crm`, `masterdata`, `access`,
  `collaboration`, `tenant_lifecycle` and `semantic` `.outbox_messages` (`semantic` added by migration `AddSemanticRelaySource`). It is keyed on `current_user` rather than `TO fynovio_relay`
  because the role is created by a script after migrations run.
- `fynovio_relay` (`scripts/create-relay-role.sql`): on the six outboxes only column `SELECT (id, tenant_id, event_id,
  event_type, aggregate_type, aggregate_id, aggregate_version, occurred_at, processed_at)` and `UPDATE (processed_at)`;
  `SELECT, INSERT, UPDATE` on the two messaging tables. No other table.
- `fynovio_app` (`scripts/create-runtime-role.sql`): `SELECT, UPDATE` on `event_deliveries`, under its tenant.
- `tests/Messaging.Tests` runs both real scripts and proves the relay role cannot read `payload` or any business table.

## Flow

1. **Relay** (`OutboxRelay`, as `fynovio_relay`): per schema, under `pg_try_advisory_xact_lock`, reads up to 100
   pending pointers, inserts one delivery per subscribed consumer (`ON CONFLICT DO NOTHING`), sets `processed_at`.
2. **Delivery** (`DeliveryProcessor`): claims due deliveries of the consumers registered in that process
   (`FOR UPDATE SKIP LOCKED`, lease 5 min), skipping any whose earlier version of the same aggregate is not yet
   `delivered`/`skipped`; reads the full row as `fynovio_app` under the delivery's tenant; calls the consumer.
3. **Consumer**: writes its effect and its module inbox row in one transaction.

`outbox_messages.processed_at` now means "fanned out", not "delivered". Retention/cleanup of outbox and delivery rows
is an open item.
