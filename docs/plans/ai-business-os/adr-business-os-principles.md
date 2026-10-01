# ADR: AI Business OS principles

**Status:** Accepted — 2026-09-30 by the platform owner. This is the architecture baseline for the AI Business OS direction.
**Related:** `2026-09-30-owner-decisions.md`, `2026-09-30-architecture-reconciliation.md`.

## Context

The platform is moving toward an intent-driven, metadata-defined, deterministically executed business operating layer. The enterprise foundation it builds on already exists: tenancy with RLS, a default-deny PDP, idempotency, outbox, evidence and tested state machines. These principles stop the AI flexibility from eroding those guarantees.

## Decision

**Long-term model:** the architecture is horizontal, while the product and GTM are vertical. A tenant starts from a concrete vertical *Solution Package*, not an empty canvas. The name "Blueprint" is already used in doc 08:66 in a different sense (Business Control's requirements versions), so "Solution Package" is used instead.

**Principles (binding):**

1. **AI proposes; the deterministic kernel executes.** AI has only two write paths: a ConfigChangeSet (configuration) or a catalogued command that passes the PDP (data).
2. **Typed where invariants matter; metadata where variability matters.** Anything carrying money, stock, tax, identity, security or a multi-row invariant stays a typed aggregate.
3. **Configuration changes are versioned, impact-aware and forward-only.** "Undo" is a new, inverse change set. Configuration reversal never undoes external side effects.
4. **Dynamic UI means generated metadata, never runtime-generated code.** Views are specs rendered by a trusted component registry.
5. **No arbitrary AI SQL.** Natural language becomes a validated semantic query, which the owning module compiles to parameterized SQL and runs read-only.
6. **No arbitrary tenant code in the trusted host.** Custom code, if it ever exists, runs out of process in a sandbox (doc 08:30, 08:75).
7. **No secrets in LLM context.** Credentials are held only as references. Their values never reach a model.
8. **Cross-module consistency is event-driven, not cross-module ACID.** This restates the binding `AGENTS.md` rule. Atomicity exists only inside one module.
9. **Abstraction follows concrete use.** Identifiers, versioning, ownership and status are designed up front. Tables, enum values and subsystems are added only when a concrete usage needs them.
10. **Existing enterprise guarantees are never weakened for AI flexibility.** These are RLS, PDP/PEP, idempotency, outbox, evidence and state machines.

## Consequences

- A proposal that conflicts with a principle is rejected at design review. It is not reconciled case by case.
- Principles 5–8 restate or tighten existing rules (`AGENTS.md`, doc 08). Principles 1–4 and 9 are new for the Business OS direction.
- This ADR does not authorize any phase beyond the one currently being implemented (see the phase table in `2026-09-30-owner-decisions.md`).
