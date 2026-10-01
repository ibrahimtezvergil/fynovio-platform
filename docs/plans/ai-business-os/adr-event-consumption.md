# ADR: Event consumption (prerequisite for the workflow runtime)

**Status:** Proposed — 2026-10-01. Needs owner decisions **E-1** and **E-2** before any code; E-3..E-5 carry a recommendation the owner may accept as written.
**Related:** `adr-workflow-runtime.md` (decision 1 makes this a prerequisite of phase 5), `2026-09-30-owner-decisions.md` (OD-2, OD-4), `adr-business-os-principles.md`. Plan: `2026-10-01-event-consumption-plan.md`.

## Context

Every module writes its outbox row in the same `SaveChanges()` as its state change (AGENTS.md invariant 3). Five outboxes exist: `crm`, `masterdata`, `access`, `collaboration`, `tenant_lifecycle`. Nothing consumes them.

**Finding — the existing dispatcher is a silent no-op under the runtime role.** `src/Worker/OutboxDispatcherService.cs` connects through `CrmConnectionString.Resolve()` (default user `fynovio_app`) and never sets `app.tenant_id`. Every outbox table has `ENABLE` + `FORCE ROW LEVEL SECURITY` with `tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint`, so the scan matches nothing. Measured on the dev DB, 2026-10-01:

| Schema | Pending rows seen by `fynovio_app` | Pending rows seen by the migration role |
|---|---|---|
| crm | 0 | 5 |
| masterdata | 0 | 9 |
| access | 0 | 8 |
| collaboration | 0 | 0 |
| tenant_lifecycle | 0 | 2 |

Its only test (`tests/CRM.Tests/Integration/OutboxDispatcherServiceTests.cs`) uses `CreateAdminContext()`, which bypasses RLS — that is why this was never caught. The dispatcher also covers only CRM and marks rows processed after a log line.

Other gaps found:
- **Envelope drift.** `collaboration.outbox_messages` has no `causation_id` column; the workflow ADR depends on causation depth ≤ 3. `source` is inconsistent (`crm` in some CRM handlers, `/enterprise/crm-sales` and `/enterprise/access` in others).
- **No tenant registry readable without a tenant context.** Every `*tenant*` table (`tenant_lifecycle.tenant_profiles`, `access.tenant_access_state`, `identity.tenant_memberships`, …) is itself under FORCE RLS.
- **No system actor.** The PDP evaluates `ActorContext` for a human principal; a consumer that runs a PDP-gated command has no principal to act as.

## Decision

### E-1 — Reading outboxes across tenants without weakening RLS *(owner decision)*

The relay must find pending rows of every tenant. Options:

| | Option | Assessment |
|---|---|---|
| **(a)** | **Dedicated relay role, pointer-only.** New login role `fynovio_relay` (`NOSUPERUSER NOBYPASSRLS`). On each `<schema>.outbox_messages` only: column-level `SELECT (id, tenant_id, event_type, processed_at)` and `UPDATE (processed_at)`, plus a role-targeted permissive policy `CREATE POLICY relay_scan ON <schema>.outbox_messages TO fynovio_relay USING (true)`. No grant on any other table. The relay reads `(tenant_id, id)` pointers; delivery then opens a `fynovio_app` connection, sets `app.tenant_id` from the pointer, and reads the full row (payload included) under the normal tenant policy. | **Recommended.** The runtime role and every business table keep exactly today's policies. The new cross-tenant surface is four columns of five tables, never payloads. Provable by a test that `fynovio_relay` cannot read `payload` or any business table. |
| (b) | **Per-tenant iteration.** Loop over tenants, set `app.tenant_id`, scan each outbox. | Needs a tenant list readable without a tenant context, which does not exist — it would need the same kind of narrow exception as (a), and costs `tenants × 5` queries per poll. |
| (c) | **`SECURITY DEFINER` function** returning pending pointers. | Rejected. FORCE RLS applies to a non-superuser table owner too, so the function returns rows only where the owner is a superuser or `BYPASSRLS` — true in dev (migrations run as a superuser, see the table above), not something to rely on in production. A dev/prod divergence trap. |
| (d) | Run the relay as the migration role or a `BYPASSRLS` role. | Rejected: contradicts doc 07 §3 and the runtime-role rule in `scripts/create-runtime-role.sql`. |

### E-2 — First real consumer *(owner decision)*

Selection constraints: idempotent; observable end to end (API or UI); **no aggregate mutation** (see E-5); safe if the backlog is replayed; does not pre-empt a later phase (no workflow, no AI, no Semantic Catalog).

| | Candidate | Notes |
|---|---|---|
| **(a)** | **Opportunity activity timeline** — a CRM-owned read model `crm.opportunity_activity` fed by `enterprise.crmsales.opportunity.*` events, shown on the opportunity detail page. | **Recommended.** Pure projection, replay-safe (replay *wants* history), visible in the UI, read access reuses `GetOpportunity`'s PDP check. Same-module, so it proves the relay/ledger, not a cross-module contract; the first cross-module consumer comes with phase 5 or (b). |
| (b) | **Reassignment e-mail** — `opportunity.reassigned.v1` → e-mail to the new owner through the existing `EmailDispatcher`. | Cross-module and user-visible. An external side effect: must start from a cursor (never replay), needs a recipient lookup in Identity and a per-user opt-out decision. |
| (c) | **Party merge re-point** — `masterdata.party.merged.v1` → CRM rewrites `customer_party_id`. | Not recommended now: mutates `Opportunity` (version, evidence, system actor) for little gain — read-time tombstone resolution already shows the survivor. |

### E-3 — Delivery model *(recommendation)*

1. **Subscriptions are code, not data.** `IEventConsumer { string Name; IReadOnlySet<string> EventTypes; Task HandleAsync(EventEnvelope, ConsumerContext, CancellationToken) }`, registered in DI in `Worker`. Tenant-defined subscriptions arrive only with the workflow runtime.
2. **Envelope.** `EventEnvelope` in `Contracts`, mirroring the outbox columns (doc 04 §4 CloudEvents profile): `EventId, EventType, Source, Subject, TenantId, AggregateType, AggregateId, AggregateVersion, CorrelationId, CausationId?, OccurredAt, Payload (JsonElement)`. Consumers depend on `Contracts` only — never on another module's outbox entity.
3. **Two ledgers.**
   - **Fan-out:** the relay reads a pending pointer, inserts one `messaging.event_deliveries` row per matching consumer (`tenant_id, consumer, source_schema, event_id, status, attempts, next_attempt_at, last_error`, unique `(consumer, event_id)`), then sets the outbox row's `processed_at`. Meaning of `processed_at` changes from "logged" to "fanned out". RLS on `event_deliveries` by `tenant_id`; the relay role gets the same pointer-style policy as on the outboxes.
   - **Effect dedup (inbox):** each consuming module owns `<schema>.consumed_events (tenant_id, consumer, event_id)` with a unique key, written **in the same transaction as the consumer's effect**. Redelivery hits the unique key and is acknowledged without re-applying. This, not the delivery ledger, is what makes the effect exactly-once.
4. **Payloads are not copied.** `event_deliveries` holds pointers; the payload is read from the source outbox at delivery time under the tenant context (keeps Collaboration's deliberately thin events where they are).
5. **Claiming.** `FOR UPDATE SKIP LOCKED` on `event_deliveries` so more than one worker is safe. Batch 100, poll 2 s, wake early on `LISTEN/NOTIFY` later if latency matters (not in v1).
6. **Failure.** Exponential backoff (5 s × 2ⁿ, cap 15 min), `max_attempts = 10`, then `status = dead`. `last_error` holds the exception type and a truncated message — never the payload. A dead delivery is re-armed by a single admin command (`RedeliverEvent`), evidence-recorded; there is no automatic replay.
7. **Ordering.** No global ordering. Deliveries for one `(consumer, aggregate)` are claimed in `aggregate_version` order and a later version is not claimed while an earlier one is pending or retrying (head-of-line per aggregate, not per consumer). A dead earlier version blocks only that aggregate and is visible in the dead-letter count.
8. **Backlog cutover.** On first deploy each consumer is registered with a start policy: `FromBeginning` (projections, e.g. E-2 (a)) or `FromNow` (side effects, e.g. E-2 (b)); `FromNow` rows are fanned out as `status = skipped`. Outbox rows created before the relay existed are fanned out under that policy — none are silently dropped.

### E-4 — Envelope alignment *(recommendation)*

- Add `causation_id uuid NULL` to `collaboration.outbox_messages` (additive migration, existing rows `NULL`).
- Consumers propagate: anything a consumer writes to an outbox carries `causation_id = envelope.EventId` and the inbound `correlation_id`.
- `source` normalisation is recorded as a known inconsistency and **not** changed now (it is a published field; changing it is a versioning decision). The relay keys on `(source_schema, event_id)`, not on `source`.

### E-5 — Consumers and authorization *(recommendation)*

- In this phase a consumer may write **only** its own module's projections and inbox rows. It may not call a PDP-gated command or mutate an aggregate.
- Reads of a projection are authorized at query time exactly like the aggregate they describe (for E-2 (a): the same action key and `ResourceDescriptor` as `GetOpportunityHandler`).
- A system/service principal for consumers is **deferred to phase 5**, where `adr-workflow-runtime.md` decision 2 already requires an "explicitly granted workflow service principal". No implicit system actor is introduced here.

### E-6 — Coverage and roles *(recommendation)*

- The relay covers all five outboxes from the first version; `Worker` gains the missing `Collaboration` reference.
- `scripts/create-runtime-role.sql` gains `fynovio_relay` (if E-1 (a)) with the column/policy grants above, and grants on the new `messaging` schema.
- `OutboxDispatcherService` is removed; its admin-context test is replaced.
- **Test rule:** relay and consumer integration tests connect as `fynovio_relay` / `fynovio_app`, never as the admin/migration role. Required negative tests: the relay role cannot read `payload` or any non-outbox table; a consumer cannot see another tenant's event; redelivery does not double-apply; a poison message ends `dead` after `max_attempts`.

## Consequences

- The workflow runtime (phase 5) gets triggers, causation and an idempotent ingestion path without inventing them.
- One new cross-tenant surface (E-1 (a)), scoped to pointer columns and testable as such. RLS on business tables, the PDP, idempotency, evidence and the state machines are untouched.
- `processed_at` changes meaning; nothing outside `Worker` reads it today.
- A new `messaging` schema owned by the platform (not a business module), migrated with its own `DbContext` in `Worker`.

## Not in scope

Tenant-defined subscriptions, webhooks/external brokers, the workflow runtime, a system actor, AI, Semantic Catalog, outbox retention/cleanup (recorded as an open item).
