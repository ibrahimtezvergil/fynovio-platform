# Plan: Event consumption (phase B)

**Status:** In progress — ADR accepted 2026-10-01 with E-1 (a) and E-2 (a).
**Branch:** `docs/event-consumption-adr` (ADR + plan), implementation on `feat/event-consumption`.
**Order agreed by the owner (2026-10-01):** B (this) → A (tier-2 relations/stored views) → C (Semantic Catalog stage 2).

Implements the accepted options E-1 (a) and E-2 (a); see the ADR's implementation notes for deviations.

## Task 0 — Decision
- [x] Write the ADR with the measured dispatcher finding and the options.
→ Commit: `7724d59` "docs(adr): propose event consumption design and plan (phase B)"
- [x] Owner decides E-1 and E-2; ADR status → Accepted.
→ Commit: `3745383` "docs(adr): accept the event consumption ADR with implementation notes"

## Task 1 — Envelope and contracts
- [x] `Contracts`: `EventEnvelope`, `IEventConsumer`, `ConsumerStartPolicy` (`FromBeginning` / `FromNow`).
- [x] Collaboration: additive migration adding `causation_id uuid NULL` to `outbox_messages`; entity + `Create` overload; schema doc revision.
→ Commit: `4e6dbe3` "feat(contracts): add the event envelope and consumer contract; add causation id to the collaboration outbox"
Note: no `ConsumerContext` — the envelope carries the tenant, and a consumer resolves its own dependencies from its DI scope.
- [ ] Tests: envelope round-trip from each module's outbox row shape. (Moved into Task 4: the envelope is built by the delivery reader, so the round-trip is tested there against all five schemas.)

## Task 2 — Relay role and `messaging` schema
- [x] `MessagingDbContext` (in its own `src/Messaging` project — ADR implementation note 1): `messaging.event_deliveries` (unique `(consumer, event_id)`, index on `(status, next_attempt_at)`), RLS ENABLE + FORCE by `tenant_id`.
- [x] Hand-written policy migration: `relay_access` on the five `outbox_messages` and on `event_deliveries` (keyed on `current_user = 'fynovio_relay'`).
- [x] `scripts/create-relay-role.sql`: `fynovio_relay` with column-level grants only; `scripts/create-runtime-role.sql`: `messaging` grants for `fynovio_app`.
- [x] Negative tests as `fynovio_relay`: cannot read `payload`, cannot read any business table, cannot read `identity`/`access` tables.
→ Commit: `ae926da` "feat(messaging): relay outboxes into a delivery ledger and deliver to consumers"
Note: Tasks 2–4 landed in one commit. Policies are `current_user`-keyed rather than `TO fynovio_relay`, and the relay role is created by a separate `scripts/create-relay-role.sql` (ADR implementation note 2). `tests/Messaging.Tests` runs both real scripts.

## Task 3 — Relay (fan-out)
- [x] `OutboxRelay`: scan pending pointers per schema as `fynovio_relay`, insert one delivery per matching consumer (respecting each consumer's start policy → `pending` or `skipped`), set `processed_at`, one transaction per batch.
- [x] Remove `OutboxDispatcherService` and its admin-context test.
- [x] Tests as `fynovio_relay`: rows of two tenants are fanned out; an event no consumer subscribes to is marked processed with no delivery; a re-run creates no duplicates.
→ Commit: `ae926da` "feat(messaging): relay outboxes into a delivery ledger and deliver to consumers"
Note: fan-out also takes a per-schema advisory lock (ADR implementation note 4), with its own test.

## Task 4 — Delivery, inbox, retries
- [x] `DeliveryProcessor`: claim with `FOR UPDATE SKIP LOCKED`, per-aggregate version ordering, open a `fynovio_app` scope with `app.tenant_id` from the delivery, read the outbox row, build `EventEnvelope`, invoke the consumer.
- [x] Backoff 5 s × 2ⁿ capped at 15 min, `max_attempts = 10` → `dead`; `last_error` without payload.
- [x] Tests as `fynovio_app`: redelivery does not double-apply (inbox unique key); a poison message ends `dead`; an earlier version pending blocks the later one of the same aggregate only; a consumer never sees another tenant's event.
→ Commit: `ae926da` "feat(messaging): relay outboxes into a delivery ledger and deliver to consumers"
Note: the inbox belongs to the consumer, so "redelivery does not double-apply" is proven with the first real consumer in Task 6. The envelope round-trip moved here from Task 1 and runs against all five schemas. Messaging.Tests: 41/41.

## Task 5 — Redeliver command
- [ ] `RedeliverEvent` admin command (PDP-gated platform action, idempotent, evidence-recorded) re-arms a `dead` delivery.
- [ ] Tests: denied without the action; evidence row written; double submit is one re-arm.
Note (2026-10-01): deferred to an owner decision. A PDP-gated action needs a permission home; adding it to an existing capability template version in place is the exception the owner refused (K3). Options: (a) a new `messaging` v1 manifest with a tenant-administrator endpoint, (b) a platform-operator Worker command outside tenant permissions, with evidence. Until then a dead delivery is re-armed by SQL (`status = 'pending', next_attempt_at = now()`).

## Task 6 — First consumer: opportunity activity timeline (E-2 (a))
- [x] `crm.opportunity_activity` (+ `crm.consumed_events`) with RLS; consumer for `enterprise.crmsales.opportunity.*`, start policy `FromBeginning`.
- [x] `GET /opportunities/{id}/activity`, authorized like `GetOpportunity`.
→ Commit: `e36c897` "feat(crm): project opportunity facts into an activity timeline"
Note: the route is `/opportunities/{id}/activity` (the opportunity endpoints are not under `/crm`). Line add/cancel publish no outbox fact, so they are not on the timeline.
- [x] Web: activity card on the opportunity detail page.
→ Commit: `e6b49fd` "feat(web): show the opportunity activity timeline on the detail page" (follow-up fix: `fcd6a7e` "fix(web): re-read the activity timeline shortly after the opportunity changes")
- [x] Tests: backend handler + endpoint (403/404 parity with `GetOpportunity`), web component, real-stack check in the browser on the dev DB.
→ Commit: `e36c897`, `e6b49fd`, `fcd6a7e` (as above)
Note: the browser check (dev DB, 2026-10-01) showed the 24 pending outbox rows fanned out and two live reassignments appear without a reload; it surfaced the hidden-tab polling gap fixed in `fcd6a7e`. The code review surfaced nothing; my own review found the late-registration history gap, fixed in `9535b68` "fix(messaging): backfill history for a from-beginning consumer registered later".

## Task 7 — Wrap-up
- [x] `docs/schema/*` revisions (`messaging-schema.md` new, CRM revision 13, collaboration causation note), AGENTS.md status line, README runbook. Test evidence: .NET 1504/1504, web 743/743 (typecheck, lint, build clean).
→ Commit: `ef72110` "docs: event consumption schema, status and plan"
- [ ] Merge to `main` after owner review.

## Open items (not in this plan)
- Outbox and delivery retention/cleanup.
- `source` field normalisation (a versioning decision).
- System/service principal for consumers (phase 5).
