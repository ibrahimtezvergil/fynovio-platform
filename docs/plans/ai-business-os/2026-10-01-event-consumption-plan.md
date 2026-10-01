# Plan: Event consumption (phase B)

**Status:** In progress — ADR accepted 2026-10-01 with E-1 (a) and E-2 (a).
**Branch:** `docs/event-consumption-adr` (ADR + plan); implementation on `feat/event-consumption` once accepted.
**Order agreed by the owner (2026-10-01):** B (this) → A (tier-2 relations/stored views) → C (Semantic Catalog stage 2).

Implements the accepted options E-1 (a) and E-2 (a); see the ADR's implementation notes for deviations.

## Task 0 — Decision
- [x] Write the ADR with the measured dispatcher finding and the options.
→ Commit: `7724d59` "docs(adr): propose event consumption design and plan (phase B)"
- [ ] Owner decides E-1 and E-2; ADR status → Accepted.

## Task 1 — Envelope and contracts
- [ ] `Contracts`: `EventEnvelope`, `IEventConsumer`, `ConsumerContext`, `ConsumerStartPolicy` (`FromBeginning` / `FromNow`).
- [ ] Collaboration: additive migration adding `causation_id uuid NULL` to `outbox_messages`; entity + `Create` overload; schema doc revision.
- [ ] Tests: envelope round-trip from each module's outbox row shape.

## Task 2 — Relay role and `messaging` schema
- [ ] `MessagingDbContext` in `Worker`: `messaging.event_deliveries` (unique `(consumer, event_id)`, index on `(status, next_attempt_at)`), RLS ENABLE + FORCE by `tenant_id`.
- [ ] Hand-written policy migration: `relay_scan` on the five `outbox_messages` and on `event_deliveries`, `TO fynovio_relay`.
- [ ] `scripts/create-runtime-role.sql`: `fynovio_relay` with column-level grants only; `messaging` grants for `fynovio_app`.
- [ ] Negative tests as `fynovio_relay`: cannot read `payload`, cannot read any business table, cannot read `identity`/`access` tables.

## Task 3 — Relay (fan-out)
- [ ] `OutboxRelay`: scan pending pointers per schema as `fynovio_relay`, insert one delivery per matching consumer (respecting each consumer's start policy → `pending` or `skipped`), set `processed_at`, one transaction per batch.
- [ ] Remove `OutboxDispatcherService` and its admin-context test.
- [ ] Tests as `fynovio_relay`: rows of two tenants are fanned out; an event no consumer subscribes to is marked processed with no delivery; a re-run creates no duplicates.

## Task 4 — Delivery, inbox, retries
- [ ] `DeliveryProcessor`: claim with `FOR UPDATE SKIP LOCKED`, per-aggregate version ordering, open a `fynovio_app` scope with `app.tenant_id` from the delivery, read the outbox row, build `EventEnvelope`, invoke the consumer.
- [ ] Backoff 5 s × 2ⁿ capped at 15 min, `max_attempts = 10` → `dead`; `last_error` without payload.
- [ ] Tests as `fynovio_app`: redelivery does not double-apply (inbox unique key); a poison message ends `dead`; an earlier version pending blocks the later one of the same aggregate only; a consumer never sees another tenant's event.

## Task 5 — Redeliver command
- [ ] `RedeliverEvent` admin command (PDP-gated platform action, idempotent, evidence-recorded) re-arms a `dead` delivery.
- [ ] Tests: denied without the action; evidence row written; double submit is one re-arm.

## Task 6 — First consumer: opportunity activity timeline (E-2 (a))
- [ ] `crm.opportunity_activity` (+ `crm.consumed_events`) with RLS; consumer for `enterprise.crmsales.opportunity.*`, start policy `FromBeginning`.
- [ ] `GET /crm/opportunities/{id}/activity`, authorized like `GetOpportunity`.
- [ ] Web: activity card on the opportunity detail page.
- [ ] Tests: backend handler + endpoint (403/404 parity with `GetOpportunity`), web component, real-stack check in the browser on the dev DB.

## Task 7 — Wrap-up
- [ ] `docs/schema/*` revisions, AGENTS.md status line, report with test evidence, merge to `main` after owner review.

## Open items (not in this plan)
- Outbox and delivery retention/cleanup.
- `source` field normalisation (a versioning decision).
- System/service principal for consumers (phase 5).
