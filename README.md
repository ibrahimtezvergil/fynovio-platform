# fynovio-platform

Modular-monolith backend for Fynovio, a multi-tenant B2B SaaS platform. Built on .NET 10 / C# 13 with PostgreSQL, following a strict module-boundary architecture designed to scale toward eventual service extraction without paying microservice tax up front.

> **Status:** early-stage. The `Contracts` project and the `CRM` module (entities, EF Core mapping, first migration) are implemented. `Access`, `MasterData`, `Organization`, and `TenantLifecycle` are placeholder module projects. `Host` and `Worker` are unmodified ASP.NET Core / Worker Service scaffolds — no HTTP endpoints or background jobs exist yet beyond the templates.

## Architecture

The system is a **modular monolith**: independently-boundaried modules that share a process and deployment unit today, but are structured so any module could be pulled into its own service later without a rewrite.

Binding rules (enforced by fitness functions once CI exists — see `AGENTS.md`):

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
  Host/                 ASP.NET Core composition root (API) — scaffold only so far
  Worker/               .NET Worker Service composition root (background jobs) — scaffold only so far
  Modules/
    CRM/                Implemented pilot module: Parties, Opportunities, Opportunity
                         Lines/Needs, Customer Needs, tenant field customization,
                         outbox, idempotency, evidence records, EF Core persistence
                         and migrations (PostgreSQL schema: crm)
    Access/              Placeholder
    MasterData/          Placeholder
    Organization/        Placeholder
    TenantLifecycle/     Placeholder
docs/
  schema/crm-sales-schema.md   Physical schema design for the CRM module (read before
                                 changing the EF Core model)
  ai-tooling.md                Record of the AI development-environment setup for this repo
tests/                   Test projects mirroring src/Modules/* (none yet — pending first module test)
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

## Getting started

```bash
dotnet tool restore     # one-time per clone: restores dotnet-ef
dotnet build            # build the solution
dotnet test             # run all tests (none exist yet)
dotnet format           # apply formatting rules before committing
```

Add a new CRM migration:

```bash
dotnet ef migrations add <Name> \
  --project src/Modules/CRM/CRM.csproj \
  --startup-project src/Modules/CRM/CRM.csproj \
  --output-dir Persistence/Migrations
```

EF Core migrations under `Persistence/Migrations/` are generated output — regenerate them with `dotnet ef migrations add`, never hand-edit a migration or its model snapshot.

## Documentation for contributors and coding agents

- **`AGENTS.md`** — the model-independent engineering contract: stack, code conventions, architecture and database rules, testing expectations, and generated-artifact policy. Read this first.
- **`CLAUDE.md`** — Claude Code-specific tool routing and methodology (retrieval order, when to use which skill, reasoning-effort guidance).
- **`docs/schema/crm-sales-schema.md`** — the physical schema design behind the CRM module's EF Core model.
- **`docs/ai-tooling.md`** — what AI tooling is active in this repo and why.

This repo also maintains a Graphify code graph under `graphify-out/` (committed `graph.json`, `GRAPH_REPORT.md`, `manifest.json`) that AI coding agents use for fast, scoped code navigation instead of broad repository scans. It's kept current automatically by a local git `post-commit` hook.
