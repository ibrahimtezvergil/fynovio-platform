# fynovio-platform — Engineering Contract

Model-independent engineering rules for any coding agent (Claude Code, Codex, or a human) working in this repository. This file is the shared contract and should stay valid regardless of which agent is driving. Claude-specific tool routing and methodology lives in `CLAUDE.md`; avoid duplicating the same rule in both files.

Approved 2026-09-14 — binding, not a draft.

## Stack
- .NET 10 (LTS, supported until ~2028), C# 13; ASP.NET Core (`Host`), PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL`.
- Architecture source of truth: `../enterprise ve B2B mimari araştırma/docs/architecture-analysis/` (18 numbered decision docs; see especially 04 Canonical Concepts, 08 Module Boundaries, 12 Fitness Functions, 17 CRM+Sales Pilot Domain).
- Physical schema design lives in `docs/schema/` in this repo (`crm-sales-schema.md` is the current checkpoint for the CRM module) — read it before changing the EF Core model.

## Code Conventions
- Enable `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` in every `.csproj`.
- File-scoped namespaces (`namespace Foo.Bar;`), one public type per file, file name matches type name.
- `PascalCase` for types/public members, `camelCase` for locals/parameters, `_camelCase` for private fields.
- No abbreviations except well-known ones (`id`, `url`, `db`).
- Prefer `record`/`record struct` for immutable contracts/DTOs (see `Contracts` project); encapsulated `class` with private setters + factory methods for aggregates with identity and invariants (see `CRM.Domain.Opportunity`).
- Async all the way: suffix `Async`, never block with `.Result`/`.Wait()`, always accept/forward `CancellationToken` on I/O paths.
- No magic strings for domain identifiers — use the canonical typed primitives from doc 04 (`TenantId`, `EntityRef`, `PrincipalRef`, `EntityVersion` in `Contracts`).
- Use C# `enum`s instead of string/int constants for closed sets of values; when an enum backs a DB column, give it an explicit string conversion matching the schema doc's CHECK constraint values (see any `Persistence/Configurations/*Configuration.cs`).
- Comments only where the *why* isn't obvious (a workaround, a non-obvious invariant); never restate what the code already says.

## Architecture Rules (binding — enforced by fitness functions, doc 12)
- A module project (`src/Modules/*`) may reference `Contracts` only — never another module's project, namespace, or database schema.
- No cross-module ACID transactions. Cross-module coordination happens via the outbox + published, versioned, past-tense facts (`XChanged`, `XCompleted`), never shared private domain events.
- No tenant-free repository/query method for tenant-scoped data — every query path carries `TenantId`.
- `Host` (and `Worker`) are the only composition roots; modules never use a service locator or reach into another module's DI registrations.
- Each module owns its own `DbContext`/schema/migrations. No shared "god" `DbContext`. One PostgreSQL schema per module.
- Public module surface = commands, queries, and published events only. Domain and Infrastructure namespaces inside a module are never imported from outside it.

## Database Rules
- Every table carries `tenant_id`; every foreign key is the tenant-safe composite form (`FOREIGN KEY (tenant_id, x_id) REFERENCES x (tenant_id, id)`) — a bare `tenant_id NOT NULL` plus RLS is not a referential-integrity guarantee.
- Row-Level Security (`ENABLE`/`FORCE ROW LEVEL SECURITY`) is mandatory on tenant-scoped tables; isolation tests must run as an **unprivileged runtime role**, not the migration-running superuser (fitness function FF03, doc 12) — superusers and table owners bypass RLS regardless of policy.
- No soft delete (no `deleted_at`). Use a domain `status` value instead.
- One declared money-rounding rule per aggregate: entered values 2dp, computed values 4dp, rounded to 2dp exactly once at a named lifecycle transition — don't invent a second rounding point.
- State/date invariants that a single row can express are DB `CHECK` constraints; invariants spanning multiple rows (e.g. "at least one active required line before completion") are enforced in the aggregate, not the database — see `CRM.Domain.Opportunity.Complete()`.
- Outbox rows commit in the same transaction as the domain state they describe (same `SaveChanges()` call), with a CloudEvents-shaped envelope (`source`, `subject`, `correlation_id`, `causation_id`, `aggregate_version`, unique `event_id`).
- EF Core migrations under `Persistence/Migrations/` are generated output — regenerate with `dotnet ef migrations add`, never hand-edit a migration or its model snapshot.

## Enforcement Scope (approved 2026-09-16)

**Binding core — enforced in CI from now on:**
1. Every tenant-scoped table carries `tenant_id` and every FK is the tenant-safe composite form.
2. RLS (`ENABLE` + `FORCE`) on every tenant-scoped table, exercised by an integration test running as the unprivileged runtime role.
3. Business state and its outbox row commit in the same `SaveChanges()`.
4. Every state-changing command is idempotent (tenant + principal + operation + key).
5. Migrations are reversible, expand/contract-shaped, and generated — never hand-edited except for the RLS exception below.
6. Single-row invariants are DB `CHECK` constraints and each one has a test.
7. Supported runtime (.NET LTS) and a dependency-vulnerability check in CI.

**Trigger-based — not required yet:**
Evidence records are required only for risk-catalogued commands (money-carrying transitions, authorization changes, cancellation). Evidence tamper-proofing, per-module DB roles, delegation/SoD/decision epochs and the remaining fitness functions in doc 12 stay NOT APPLICABLE until the capability they guard is built (doc 12 §1 permits a visible not-applicable state, never a fake pass).

**Named exception to the generated-migration rule:** PostgreSQL RLS has no EF Core model representation, so RLS policies live in an otherwise-empty generated migration whose `Up`/`Down` bodies are written by hand with `migrationBuilder.Sql(...)`. This is the only permitted hand-written migration content.

## Testing / Definition of Done
- xUnit, one test project per module under `tests/`, mirroring `src/Modules/*` (not yet created — pending first module test).
- Architecture/dependency-rule tests (e.g. NetArchTest) enforcing the "Architecture Rules" above must run in CI, not just be reviewed by eye.
- Integration tests against PostgreSQL use Testcontainers — no shared/mutable dev database in CI.
- A change is done when: it builds clean (`dotnet build`), it doesn't violate an Architecture or Database Rule above, and — for schema changes — the migration has been checked against `docs/schema/*.md` line by line (composite FKs, CHECK constraints, indexes all present).

## Formatting & Tooling
- Run `dotnet format` before every commit; CI should fail on unformatted code once CI exists.
- Treat nullable-reference and analyzer warnings as errors (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`) once the codebase is clean enough to turn it on.
- `.editorconfig` at repo root should be added to make these rules machine-enforced (not yet created).
- `dotnet-ef` is a repo-local tool (`.config/dotnet-tools.json`) — run `dotnet tool restore` once per clone, then `dotnet ef ...` or `dotnet tool run dotnet-ef ...`.

## Generated / Derived Artifacts — don't hand-edit
- `**/Persistence/Migrations/*` — EF Core migrations and the model snapshot.
- `graphify-out/graph.json`, `graphify-out/GRAPH_REPORT.md`, `graphify-out/manifest.json` — regenerate with `graphify update .` (AST-only, no API key) or `graphify extract . --code-only` for a full rebuild; `graphify-out/cache/` and `graphify-out/graph.html` are gitignored and always safe to delete/regenerate.

## Repository Intelligence (cross-agent, tool-agnostic)
This repo has a Graphify code graph at `graphify-out/`. Prefer `graphify query "<question>"`, `graphify explain "<symbol>"`, or `graphify path "<A>" "<B>"` over a broad repository grep when the installed agent supports it; fall back to targeted `rg` for exact literals/config/generated text, and to a full file read only once the relevant region is identified. A git `post-commit` hook rebuilds the graph automatically after each commit on this local clone (re-run `graphify hook install` on a fresh clone to get it there too).

## Cross-Agent / Multi-Agent Policy
- Default isolation: **1 task = 1 branch = 1 worktree = 1 agent session**. Two agents (e.g. Claude Code and Codex) must not concurrently edit the same working tree.
- Preferred cross-model review flow: one agent implements → verifies → exposes the branch/diff → the other agent reviews independently → the first agent validates findings against actual code/tests/runtime evidence and fixes only valid findings. The reverse flow is equally valid.
- Codex CLI is present on this machine (detected, not configured by this repo) — cross-model review today is invoked manually by the developer, not automatically triggered by either agent.

## Commands
```bash
dotnet build                              # Build the solution
dotnet test                               # Run all tests (none exist yet)
dotnet format                             # Apply formatting rules
dotnet tool restore                       # Restore repo-local tools (dotnet-ef) once per clone
dotnet ef migrations add <Name> \
  --project src/Modules/CRM/CRM.csproj \
  --startup-project src/Modules/CRM/CRM.csproj \
  --output-dir Persistence/Migrations     # Add a CRM migration
```

## Status
`Contracts` primitives and the CRM+Sales pilot module (entities, `CrmDbContext`, first migration) are implemented as of 2026-09-14; see `docs/schema/crm-sales-schema.md` for the design. This file and `CLAUDE.md` are approved and binding, but some rules (e.g. `TreatWarningsAsErrors`, CI) are intentionally aspirational until more of the codebase exists to exercise them against — that's a scoping note, not a draft status.
