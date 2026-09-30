# ADR: Workflow runtime (OD-2)

**Status:** Accepted — 2026-09-30 by the platform owner. **Not implemented.** It governs phase 5, which has not been opened.
**Related:** `2026-09-30-owner-decisions.md` (OD-2), `2026-09-30-architecture-reconciliation.md` §J. Deviates from doc 09:19 ("not a home-built general workflow engine"): the runtime defined here is deliberately *not* general.

## Context

The Business OS needs to show that software can *act*, not only configure and report. Doc 09 says to adopt a durable runtime once two real long-running processes exist (09:99). Today there is no event consumption infrastructure at all. `src/Worker/OutboxDispatcherService.cs` only reads CRM's outbox, logs each message and marks it processed. There is no subscriber and no consumer ledger.

## Decision

1. **Prerequisite:** before any workflow code, build real event consumption. That means subscriptions, a per-consumer delivery ledger, retries/dead-letter, and reading under the runtime role with tenant context set. It covers every module outbox, not just CRM's.
2. **Scope of the first runtime:** a narrow interpreter on PostgreSQL running in `Worker`:
   - **Trigger:** a published outbox event type plus a filter expression.
   - **Condition:** the shared, non-Turing-complete expression tree (reconciliation §F).
   - **Action:** only catalogued actions. The PDP is checked on every execution, under the invoking principal or an explicitly granted workflow service principal.
   - **Wait:** a duration, or "until event X, at most T".
   - **Approval:** a human approval with a timeout branch.
   - **Shape:** linear steps plus a single level of if/else.
   - **Explicitly excluded:** loops, parallel execution, nested/child workflows, arbitrary code.
3. **Abstraction:** everything runs behind `IWorkflowRuntime`. Workflow *definitions* are ours and runtime-neutral (doc 09: "own capability adapters and definitions").
4. **Durability model:**
   - `workflow_runs` pins a definition version. A unique `trigger_event_id` makes starting idempotent.
   - `workflow_timers (due_at)` holds pending waits.
   - `workflow_step_executions` records steps, with the idempotency key `run_id + step_index`.
   - The worker polls with `FOR UPDATE SKIP LOCKED`.
5. **Safety limits:**
   - At most 20 steps and at most 30 days per run.
   - A per-definition rate limit.
   - Causation chain depth of at most 3, since workflow-emitted events carry `causation_id`.
   - `Irreversible` actions execute at most once.
6. **Activation:** a workflow definition is usable only after its ChangeSet reaches `Active` (OD-4).
7. **Temporal is not added now.**

## Criteria for moving to Temporal

Temporal (or another durable engine) is re-evaluated in a spike when **any** of the following is measured. Each comes with the evidence required:

| # | Trigger | Evidence |
|---|---|---|
| T1 | At least **two real, paying-tenant processes** need long waits (days or more) *and* in-flight version migration (doc 09:99) | Named processes, with the version-migration requirement written down |
| T2 | A real process genuinely needs a construct this runtime excludes: parallel branches, child workflows, loops or saga-style multi-step compensation | The process spec, plus proof that a linear redesign does not work |
| T3 | Timer/run volume or latency outgrows polling. Starting thresholds: > 50,000 active runs/timers **or** p95 timer-fire lag > 30 s under normal load | Production metrics over at least 2 weeks |
| T4 | Operational reliability: more than 2 incidents in a quarter of stuck, lost or duplicated runs whose root cause is the runtime itself | Incident reports with root cause |
| T5 | Workflow execution must be isolated from the monolith (independent scaling or failure isolation, doc 08 §6 extraction checklist) | A completed extraction-readiness checklist |

Also required before adopting it:
- Operating cost and ownership (self-hosted vs. Temporal Cloud) are accepted.
- Data residency and KVKK are cleared for workflow payloads.

**Migration path:** definitions stay ours. A second `IWorkflowRuntime` implementation is added. New runs start on the new runtime, in-flight runs drain on the old one, and the old runtime is removed once no runs remain.

## Consequences

- One runtime, small and replaceable. No external infrastructure until a measured trigger fires.
- The expression tree and action catalog are shared with the metrics and validation layers. This keeps dependency extraction uniform.
- Phase 5 cannot start before the event consumption prerequisite is done.
