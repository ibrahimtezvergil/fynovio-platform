# fynovio-platform — Engineering Contract

Model-independent engineering rules for any coding agent (Claude Code, Codex, or a human) working in this repository. This file is the shared contract and should stay valid regardless of which agent is driving. Claude-specific tool routing and methodology lives in `CLAUDE.md`; avoid duplicating the same rule in both files.

Approved 2026-09-14 — binding, not a draft.

## Stack
- .NET 10 (LTS, supported until ~2028), C# 13; ASP.NET Core (`Host`), PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL`.
- Architecture source of truth: `../enterprise ve B2B mimari araştırma/docs/architecture-analysis/` (20 numbered decision docs; see especially 04 Canonical Concepts, 08 Module Boundaries, 12 Fitness Functions, 17 CRM+Sales Pilot Domain, 19 Identity/Access, 20 Pilot Enforcement Scope). That directory is not a git repository.
- Physical schema design lives in `docs/schema/` in this repo (`crm-sales-schema.md` for CRM, `identity-access-schema.md` for Identity+Access, `tenant-network-schema.md` for the unassigned network tables) — read the relevant one before changing an EF Core model.

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
- xUnit, one test project per module under `tests/`, mirroring `src/Modules/*` (`tests/CRM.Tests` exists: `Domain/`, `Architecture/`, `Integration/`).
- Architecture/dependency-rule tests (NetArchTest) enforcing the "Architecture Rules" above run in CI, not just by eye.
- Integration tests against PostgreSQL use Testcontainers — no shared/mutable dev database. Docker must be running. On macOS Docker Desktop, Testcontainers may not find the daemon; export `DOCKER_HOST=unix://$HOME/.docker/run/docker.sock` before `dotnet test` if it doesn't.
- Tenant-isolation tests connect as the unprivileged runtime role (`PostgresFixture.RuntimeConnectionStringAsync()`), never as the superuser that runs migrations.
- A change is done when: it builds clean (`dotnet build`), it doesn't violate an Architecture or Database Rule above, and — for schema changes — the migration has been checked against `docs/schema/*.md` line by line (composite FKs, CHECK constraints, indexes all present).

## Formatting & Tooling
- Run `dotnet format` before every commit; CI (`.github/workflows/ci.yml`) fails on unformatted code, on any vulnerable package, and on failing tests.
- Treat nullable-reference and analyzer warnings as errors (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`) once the codebase is clean enough to turn it on. The build is currently warning-free; keep it that way.
- `.editorconfig` at repo root holds the machine-enforced formatting rules; it marks `**/Persistence/Migrations/*.cs` as generated so `dotnet format` never rewrites migrations. It is intentionally minimal — extend it rule by rule.
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
dotnet test                               # Run all tests (Docker required)
dotnet format                             # Apply formatting rules
dotnet format --verify-no-changes         # What CI runs
dotnet tool restore                       # Restore repo-local tools (dotnet-ef) once per clone
dotnet ef migrations add <Name> \
  --project src/Modules/CRM/CRM.csproj \
  --startup-project src/Modules/CRM/CRM.csproj \
  --output-dir Persistence/Migrations     # Add a CRM migration (same shape for Access)
dotnet ef migrations has-pending-model-changes \
  --project src/Modules/CRM/CRM.csproj \
  --startup-project src/Modules/CRM/CRM.csproj   # Must report no changes before committing
psql -d fynovio_platform -f scripts/create-runtime-role.sql   # Create the RLS-bound runtime role (after migrations)
```

## Status
As of 2026-09-16: **Phase 1 (Lifecycle/Pipeline foundation) complete.** `Contracts` primitives (including `PartyRef`, `PartyType`, `PartyDirectoryEntry`, `IPartyDirectory`, `IPartyIdentityResolver`); the CRM module (lifecycle Draft/Open/Won/Lost, pipeline_definitions/versions/stages, `Opportunity` references `PartyRef` to masterdata, `CrmDbContext`, eleven migrations including RLS on every tenant-scoped table, the `CompleteOpportunity` command writing state + outbox + evidence + idempotency atomically); the `MasterData` module's Party foundation (`Party` with `party_type`, `PartyRelationship`, `PartyExternalIdentity`, `MasterDataDbContext`, two migrations including RLS, `CreateParty`/`MergeParty`/`ResolveOrCreateParty` commands, `IPartyDirectory`/`IPartyIdentityResolver` implementations). `tests/CRM.Tests` (79 tests, updated 2026-09-17 per `CRM_Phase1_Test_Coverage_Verification_Report.pdf` — see `docs/plans/crm-phase1/2026-09-17-crm-phase1-test-plan.md`) and `tests/MasterData.Tests` (30 tests) cover domain, module boundaries, persistence, concurrency, tenant isolation, RLS isolation and the commands.

As of 2026-09-17: **Phase 1.5 (Enterprise Access Foundation) complete**, per four adversarial architecture-review rounds and their execution plan (`docs/plans/enterprise-access-foundation/`). `Contracts` gained the authorization primitive surface (`ActionKey`, `ActorContext`, `ResourceDescriptor`, `AuthorizationRequest`/`AuthorizationDecision`, `IAuthorizer`, `AccessScope`/`ScopeTerm`, `IAccessScopeResolver`, `IActionCatalog`). The `Access` module was rebuilt from a bare Role→Permission schema into the control-plane foundation: `ActionRegistryEntry`/`PermissionSet`/`PermissionSetItem`/`RolePermissionSet`/`Role` (now tenant-owned, `TenantId NOT NULL`)/`RoleAssignment` (scope columns removed entirely — cross-tenant `Network` is gone unconditionally)/`TenantAccessState` (the one canonical `TenantAccessRevision` counter); `Access.Application` holds the default-deny PDP (`AccessAuthorizer`), the query-scope resolver (`AccessScopeResolver`, implementing exactly `None`/`All`/`AnyOf([OwnedBy])`), `BootstrapTenantAccessHandler` (the one path with no `Authorize()` call — a default-deny system cannot gate its own first grant), and `GrantRoleAssignmentHandler`/`RevokeRoleAssignmentHandler` (authorize-before-idempotency-lookup ordering). Access's own `Outbox`/`Evidence`/`Idempotency` records were added (module-local duplicates, per the "modules reference Contracts only" rule). RLS is enabled on every tenant-scoped Access/Identity table; `scripts/create-runtime-role.sql` grants the `access`/`identity` schemas to `fynovio_app`. `Host` now registers `AccessDbContext` and seeds the action registry at startup. `tests/Access.Tests` (58 tests: domain, NetArchTest module-boundary, the `Authorize`/`ResolveAccessScope` equivalence contract test, RLS-as-unprivileged-role including cross-tenant write rejection, tenant-safe FK/uniqueness/optimistic-concurrency constraint proofs, `TenantAccessRevision` mutation/no-op semantics, exactly-once evidence/outbox, and end-to-end Bootstrap/Grant/Revoke handler tests — the 25-test critical+secondary pack added per the 2026-09-17 test-coverage review, see `docs/plans/enterprise-access-foundation/2026-09-17-enterprise-access-foundation-test-plan.md`) is wired into the solution — 134/134 across the full suite at the time, no CRM/MasterData regression (CRM.Tests grew to 79 the next day per its own 2026-09-17 test-coverage review — 167/167 across the full suite now). **No CRM code was touched**: `Opportunity.Reassign()` and any command-level `Authorize()` enforcement remain Phase 2 work; Access's contracts were built so Phase 2 can consume them without a redesign. Two items remain deliberately open per round 4's owner decisions: the system-template lifecycle strategy (OPEN — `BootstrapTenantAccessHandler` is today's narrow, manual answer, not a reconciler), and exactly which CRM field represents "current owner" for the `OwnedBy` scope term (business meaning frozen as "current responsible principal"; physical mapping to `Opportunity.AssignedPrincipal` needs a `Reassign()` capability CRM doesn't have yet).

The runtime baseline is .NET 10 LTS. Phase 2 (Opportunity commands/API, now unblocked) is next. Work in progress is planned in `docs/plans/`. This file and `CLAUDE.md` are approved and binding; `TreatWarningsAsErrors` is the one rule still deferred.
