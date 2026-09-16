# fynovio-platform

Modular-monolith backend for Fynovio, a multi-tenant B2B SaaS platform. Built on .NET 10 / C# 13 with PostgreSQL, following a strict module-boundary architecture designed to scale toward eventual service extraction without paying microservice tax up front.

> **Status:** early-stage. Implemented: the `Contracts` project; the `CRM` module (entities, EF Core mapping, migrations with Row-Level Security, and a first command, `CompleteOpportunity`); the `Access` module's Identity+Access schema (entities and first migration). `MasterData`, `Organization`, and `TenantLifecycle` are placeholders. `Host` registers the CRM `DbContext` and exposes only `/` and `/health/db`; `Worker` is still the template. There is a CRM test suite and a CI workflow.

## Architecture

The system is a **modular monolith**: independently-boundaried modules that share a process and deployment unit today, but are structured so any module could be pulled into its own service later without a rewrite.

Binding rules (see `AGENTS.md`; the module-boundary and tenant-isolation rules are enforced by tests in CI):

- A module (`src/Modules/*`) may depend on `Contracts` only — never on another module's project, namespace, or database schema.
- No cross-module ACID transactions. Modules coordinate through an outbox and published, versioned, past-tense facts (e.g. `OpportunityCompleted`), never shared private domain events.
- Every tenant-scoped query carries a `TenantId` — no tenant-free repository or query path.
- Each module owns its own `DbContext`, its own PostgreSQL schema, and its own EF Core migrations. There is no shared "god" `DbContext`.
- `Host` (API) and `Worker` (background jobs) are the only composition roots. A module's public surface is its commands, queries, and published events — its `Domain`/`Infrastructure` namespaces are never imported from outside the module.

Multi-tenancy is enforced in the database, not just in application code: every tenant-scoped table carries `tenant_id`, foreign keys are tenant-safe composites (`FOREIGN KEY (tenant_id, x_id) REFERENCES x (tenant_id, id)`), and PostgreSQL Row-Level Security is mandatory, tested against an unprivileged runtime role (superusers bypass RLS).

## Repository layout

```
src/
  Contracts/            Shared, dependency-free types every module may reference
                         (TenantId, EntityRef, PrincipalRef, EntityVersion)
  Host/                 ASP.NET Core composition root (API) — registers the CRM DbContext
  Worker/               .NET Worker Service composition root (background jobs) — template only
  Modules/
    CRM/                Pilot module: Parties, Opportunities, Opportunity Lines/Needs,
                         Customer Needs, tenant field customization, outbox,
                         idempotency, evidence records, the CompleteOpportunity
                         command, EF Core persistence and migrations with RLS
                         (PostgreSQL schema: crm)
    Access/              Identity+Access schema: accounts, external identities,
                         tenant memberships, roles, permissions, role assignments
                         (PostgreSQL schemas: identity, access)
    MasterData/          Placeholder
    Organization/        Placeholder
    TenantLifecycle/     Placeholder
docs/
  architecture-analysis/       Current-state analyses and target-model specs for a module
  schema/                      Physical schema designs (read before changing an EF Core model)
  plans/                       Implementation plans
  dotnet-guide.md              .NET/EF Core primer for contributors coming from Laravel
  ai-tooling.md                Record of the AI development-environment setup for this repo
scripts/
  create-runtime-role.sql      Creates the RLS-bound application role (run after migrations)
tests/
  CRM.Tests/               Domain, architecture (NetArchTest) and PostgreSQL
                            integration tests (Testcontainers)
web/                     Frontend (not started)
graphify-out/            Committed code-graph artifacts (graph.json, GRAPH_REPORT.md,
                          manifest.json) used by AI coding agents for navigation
```

The full architecture rationale (module boundaries, canonical domain concepts, fitness functions, the CRM+Sales pilot domain design) lives in a sibling research repository referenced from `AGENTS.md`.

## Stack

- **.NET 10** (LTS) / **C# 13**
- **ASP.NET Core** for the API host, **.NET Worker Service** for background processing
- **PostgreSQL** via `Npgsql.EntityFrameworkCore.PostgreSQL`
- **EF Core** for persistence, with `dotnet-ef` pinned as a repo-local tool

## Prerequisites

- **.NET 10 SDK**
- **PostgreSQL 17** (local dev is expected to run against Postgres, not an in-memory provider)
- **Docker** (Docker Desktop on macOS) — the integration tests start their own PostgreSQL container

### Installing PostgreSQL locally (macOS / Homebrew)

```bash
brew install postgresql@17
brew link postgresql@17 --force   # put psql/pg_ctl on PATH if not already linked
brew services start postgresql@17 # start now and keep it running across reboots
```

Create the role and database the modules expect by default (see `Username=postgres;Password=postgres;Database=fynovio_platform` in `CrmDbContextFactory.cs` — override via the `FYNOVIO_CRM_CONNECTION_STRING` environment variable for anything else, e.g. a stronger local password):

```bash
psql -h localhost -d postgres -c "ALTER ROLE postgres WITH LOGIN SUPERUSER PASSWORD 'postgres';" \
  || psql -h localhost -d postgres -c "CREATE ROLE postgres LOGIN SUPERUSER PASSWORD 'postgres';"
psql -h localhost -d postgres -c "CREATE DATABASE fynovio_platform OWNER postgres;"
```

Verify it's reachable:

```bash
PGPASSWORD=postgres psql -h localhost -U postgres -d fynovio_platform -c "SELECT 1;"
```

Then apply the CRM module's migrations to create its schema (`crm.*` tables):

```bash
dotnet tool restore   # one-time per clone: restores dotnet-ef
dotnet ef database update \
  --project src/Modules/CRM/CRM.csproj \
  --startup-project src/Modules/CRM/CRM.csproj
```

### Runtime role (Row-Level Security)

Migrations run as `postgres`, a superuser — and superusers bypass Row-Level Security entirely. **An application connected as `postgres` gets no tenant isolation from the database.** After applying migrations, create the unprivileged runtime role once (edit the password in the script first):

```bash
psql -h localhost -U postgres -d fynovio_platform -f scripts/create-runtime-role.sql
```

Then point the application at that role:

```bash
export ConnectionStrings__Crm="Host=localhost;Database=fynovio_platform;Username=fynovio_app;Password=<password>"
```

`ConnectionStrings__Crm` is what `Host` reads first; `FYNOVIO_CRM_CONNECTION_STRING` is the fallback, and it is also what `dotnet ef` uses — keep that one on the `postgres` role, since migrations need it.

## Getting started

```bash
dotnet tool restore     # one-time per clone: restores dotnet-ef
dotnet build            # build the solution
dotnet test             # run all tests (Docker must be running)
dotnet format           # apply formatting rules before committing
```

If the integration tests fail to find Docker on macOS, point Testcontainers at Docker Desktop's socket:

```bash
export DOCKER_HOST=unix://$HOME/.docker/run/docker.sock
```

CI (`.github/workflows/ci.yml`) runs a format check, a Release build, a vulnerable-package check and the full test suite on every push to `main` and every pull request.

Add a new CRM migration:

```bash
dotnet ef migrations add <Name> \
  --project src/Modules/CRM/CRM.csproj \
  --startup-project src/Modules/CRM/CRM.csproj \
  --output-dir Persistence/Migrations
```

EF Core migrations under `Persistence/Migrations/` are generated output — regenerate them with `dotnet ef migrations add`, never hand-edit a migration or its model snapshot. The one exception is RLS policy SQL, which has no EF Core model representation (see `AGENTS.md`, "Enforcement Scope").

## Documentation for contributors and coding agents

- **`AGENTS.md`** — the model-independent engineering contract: stack, code conventions, architecture and database rules, testing expectations, and generated-artifact policy. Read this first.
- **`CLAUDE.md`** — Claude Code-specific tool routing and methodology (retrieval order, when to use which skill, reasoning-effort guidance).
- **`docs/architecture-analysis/`** — current-state analyses (e.g. `CRM_CURRENT_STATE_ANALYSIS.md`) and target-model specs (e.g. `Enterprise_CRM_Target_Model_Binding_Implementation_Specification.pdf`) for a module.
- **`docs/schema/`** — the physical schema designs behind each module's EF Core model (`crm-sales-schema.md`, `identity-access-schema.md`, `tenant-network-schema.md`).
- **`docs/plans/`** — implementation plans, e.g. `2026-09-16-pilot-enforcement.md`, `2026-09-16-crm-target-model-phase0-delta-plan.md`, `2026-09-16-masterdata-party-foundation.md` (design), `2026-09-16-masterdata-phase0.5-execution-plan.md` (task/step/commit execution plan, not yet run).
- **`docs/ai-tooling.md`** — what AI tooling is active in this repo and why.
- **`docs/dotnet-guide.md`** — a .NET/EF Core primer for contributors coming from another ecosystem (e.g. Laravel), covering solution/project structure, DI, EF Core, migrations, and how this repo's connection-string resolution works.

This repo also maintains a Graphify code graph under `graphify-out/` (committed `graph.json`, `GRAPH_REPORT.md`, `manifest.json`) that AI coding agents use for fast, scoped code navigation instead of broad repository scans. It's kept current automatically by a local git `post-commit` hook.
