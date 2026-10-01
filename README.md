# fynovio-platform

Modular-monolith backend for Fynovio, a multi-tenant B2B SaaS platform. Built on .NET 10 / C# 13 with PostgreSQL, following a strict module-boundary architecture designed to scale toward eventual service extraction without paying microservice tax up front.

> **Status:** early-stage. **Phase 1 (Lifecycle/Pipeline foundation) and Phase 1.5 (Enterprise Access Foundation) complete, merged to `main`.** Implemented: the `Contracts` project (`PartyRef`, `PartyType`, `PartyDirectoryEntry`, `IPartyDirectory`, `IPartyIdentityResolver`, plus the authorization primitives `ActionKey`, `ActorContext`, `ResourceDescriptor`, `IAuthorizer`, `IAccessScopeResolver`, `IActionCatalog`); the `CRM` module (entities with Phase 1 lifecycle/pipeline shapes, EF Core mapping, 11 migrations including RLS on every tenant-scoped table, `CompleteOpportunity` command); the `MasterData` module's Party foundation (`Party`/`PartyRelationship`/`PartyExternalIdentity` with `party_type`, RLS, commands, contracts implementations wired into `Host`); the `Access` module's control-plane foundation (`Role`/`PermissionSet`/`RoleAssignment`/`TenantAccessState`, default-deny `AccessAuthorizer`, `AccessScopeResolver`, `Bootstrap`/`Grant`/`Revoke` handlers, RLS, wired into `Host`). `Organization` and `TenantLifecycle` are placeholders. `Host` registers all four `DbContext`s. Test suites: `tests/CRM.Tests` (79 tests), `tests/MasterData.Tests` (30 tests), `tests/Access.Tests` (58 tests) — 167 tests total, all green locally. CI (`.github/workflows/ci.yml`) runs a format check, a Release build, a vulnerable-package check and the full test suite on every push to `main`.

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
  Host/                 ASP.NET Core composition root (API) — registers the CRM and
                         MasterData DbContexts
  Worker/               .NET Worker Service composition root (background jobs) — template only
  Modules/
    CRM/                Pilot module: Opportunities (Draft/Open/Won/Lost lifecycle),
                         Pipeline stages (versioned by definition), Opportunity
                         Lines/Needs, Customer Needs, tenant field customization, outbox,
                         idempotency, evidence records, the CompleteOpportunity command,
                         references Party via PartyRef to masterdata, EF Core persistence
                         and migrations with RLS (PostgreSQL schema: crm)
    Access/              Identity+Access schema: accounts, external identities,
                         tenant memberships, roles, permissions, role assignments
                         (PostgreSQL schemas: identity, access)
    MasterData/          Party foundation: Party (+ party_type), PartyRelationship,
                         PartyExternalIdentity, own outbox/idempotency/evidence, RLS,
                         CreateParty/MergeParty/ResolveOrCreateParty commands,
                         IPartyDirectory/IPartyIdentityResolver implementations
                         (PostgreSQL schema: masterdata)
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
  create-relay-role.sql        Creates the outbox relay role (pointer columns only; run after the runtime role)
tests/
  CRM.Tests/               Domain, architecture (NetArchTest) and PostgreSQL
                            integration tests (Testcontainers)
  MasterData.Tests/        Same shape as CRM.Tests, for the MasterData module
web/                     Frontend SPA (React 19 + Vite + TypeScript; see web/README.md)
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
- **Node.js 22.13+** and npm — for the frontend in `web/`

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

Then apply each module's migrations to create its schema (`crm.*` and `masterdata.*` tables):

```bash
dotnet tool restore   # one-time per clone: restores dotnet-ef
dotnet ef database update \
  --project src/Modules/CRM/CRM.csproj \
  --startup-project src/Modules/CRM/CRM.csproj
dotnet ef database update \
  --project src/Modules/MasterData/MasterData.csproj \
  --startup-project src/Modules/MasterData/MasterData.csproj
dotnet ef database update \
  --project src/Modules/Collaboration/Collaboration.csproj \
  --startup-project src/Modules/Collaboration/Collaboration.csproj
```

### Runtime role (Row-Level Security)

Migrations run as `postgres`, a superuser — and superusers bypass Row-Level Security entirely. **An application connected as `postgres` gets no tenant isolation from the database.** After applying migrations, create the unprivileged runtime role once (edit the password in the script first) — `scripts/create-runtime-role.sql` grants it access to the `crm`, `masterdata` and `collaboration` schemas (and the identity/access ones):

```bash
psql -h localhost -U postgres -d fynovio_platform -f scripts/create-runtime-role.sql
```

Then point the application at that role:

```bash
export ConnectionStrings__Crm="Host=localhost;Database=fynovio_platform;Username=fynovio_app;Password=<password>"
export ConnectionStrings__MasterData="Host=localhost;Database=fynovio_platform;Username=fynovio_app;Password=<password>"
export ConnectionStrings__Collaboration="Host=localhost;Database=fynovio_platform;Username=fynovio_app;Password=<password>"
```

`ConnectionStrings__Crm`/`ConnectionStrings__MasterData`/`ConnectionStrings__Collaboration` are what `Host` reads first; `FYNOVIO_CRM_CONNECTION_STRING`/`FYNOVIO_MASTERDATA_CONNECTION_STRING`/`FYNOVIO_COLLABORATION_CONNECTION_STRING` are the fallbacks, and are also what `dotnet ef` uses — keep those on the `postgres` role, since migrations need it.

## Running locally: API, web and signing in

Requires the local PostgreSQL from above, with the `fynovio_platform` database created. Development only — none of this is a production procedure. In short: **(1)** migrations and runtime role once, **(2)** the API in one terminal, **(3)** the web app in another, **(4)** sign in with a seeded account.

### 1–2. Database and API

```bash
# 1. Migrations for every module, as the migration role (postgres)
dotnet tool restore
for module in MasterData CRM Access Collaboration TenantLifecycle; do   # CRM's migrations reference masterdata tables
  dotnet ef database update \
    --project src/Modules/$module/$module.csproj \
    --startup-project src/Modules/$module/$module.csproj
done
# Messaging last: its RLS migration adds the relay policy to every module's outbox table.
dotnet ef database update --project src/Messaging/Messaging.csproj --startup-project src/Messaging/Messaging.csproj

# 2. Roles. appsettings.Development.json expects the password `runtime`; the Worker's relay default is `relay`
sed "s/change-me/runtime/" scripts/create-runtime-role.sql \
  | psql -h localhost -U postgres -d fynovio_platform
sed "s/change-me/relay/" scripts/create-relay-role.sql \
  | psql -h localhost -U postgres -d fynovio_platform

# 3. Start the API (the launch profile also switches the development seed on)
dotnet run --project src/Host --launch-profile http     # http://localhost:5208

# Optional, in another terminal: the outbox relay and event consumers (e.g. the opportunity activity timeline)
dotnet run --project src/Worker
```

If the API stops at start-up with `relation "access.actions" does not exist` (or any other missing relation), step 1 was skipped or your database predates the current migrations — run the migration loop again. `curl http://localhost:5208/health/db` returns `200` when the API is up.

### 3. Web

In a second terminal (Node 22.13+):

```bash
cd web
npm ci
npm run dev      # http://localhost:5173
```

No `.env` is needed: the dev server proxies `/api/*` to the API on `:5208`, which keeps the refresh cookie (`SameSite=Strict`) and the CSRF/Origin checks same-origin. **Use port 5173** — `appsettings.Development.json` allows exactly `http://localhost:5173` as the app origin and as the base of e-mailed links, so another port makes sign-in fail the origin check. Stop any other dev server holding that port first.

### 4. Signing in

On start the API seeds five sign-in identities plus the CRM data the Opportunity screens need (idempotent; Development only, and only via the `dotnet run` launch profiles — `DevSeed:Enabled` is `false` in `appsettings.Development.json` so test hosts are unaffected). All five share the password **`Dev-Only-Passw0rd-Change-Me`** (`DevSeed:Password` in `src/Host/appsettings.Development.json`; a development-only value that must never be reused anywhere else). An account is created only if it does not exist yet, so changing `DevSeed:Password` later does not touch existing accounts — set a new one with the *Forgot password* flow or the account-security page instead.

| Account | State it exercises |
|---|---|
| `admin@fynovio.local` | tenant administrator of tenants 1 and 2 → tenant selection, then full CRM access (the seed enables the CRM module in both tenants, which grants the administrator the `crm_manager` role) |
| `rep@fynovio.local` | member of tenant 1 only, CRM sales representative (`crm_sales_representative`: read + create/work opportunities + customer search, **no** reassign) → is offered as a Reassign candidate, is not offered the Reassign control |
| `single@fynovio.local` | member of tenant 1 only, no grants → signed in directly, CRM calls are `403` |
| `viewer@fynovio.local` | member of tenant 1 only, read-only CRM (`crm_viewer`: `crm.opportunity.read`/`list`) → sees Opportunities, every mutation is denied and no action is offered |
| `nomember@fynovio.local` | no membership → the `no_membership` state |

Quick check without the frontend (note the required CSRF header; the refresh cookie is `HttpOnly`):

```bash
curl -i -X POST http://localhost:5208/auth/login \
  -H 'Content-Type: application/json' -H 'X-Requested-With: fynovio' \
  -d '{"email":"single@fynovio.local","password":"<DevSeed:Password>"}'
```

The seed also creates one pipeline per dev tenant (`Sales pipeline` v1: Qualification → Proposal → Negotiation, plus one retired stage) and a few sample customers (`Party` rows, created through the real `CreatePartyHandler`; six in tenant 1, three in tenant 2) so the customer picker has something to search. The CRM roles are **not** seeded separately any more: the seed runs the same module-enablement path production uses (see *Enabling a business module for a tenant* below), so the development database exercises the production grant mechanism. Both go through production paths too: the pipeline through `ProvisionPipelineHandler` (the `provision-crm-pipeline` command below; the seed adds one retired stage on top), the parties through `CreatePartyHandler` behind `POST /crm/references/parties`. Only the sample data itself is dev-only. A development database created before Phase 2.6 already holds `crm_*` roles that the template would have to adopt; enablement refuses to adopt tenant-authored rows (`TemplateKeyConflict`, logged as a warning by the seed), so recreate such a database.

Production has no seed and no default credential (see *Bootstrapping a tenant administrator* below). Authentication settings live under `Authentication:*` in `appsettings*.json`; the signing key, allowed origins and public base URL must be supplied per environment.

### Invitations, password reset and e-mail

Invitation and password-reset links are e-mailed. The link always points at `Authentication:PublicAppBaseUrl` and carries the single-use token in the URL *fragment* (`/accept-invite#token=…`, `/reset-password#token=…`), so it never reaches server logs, proxies or `Referer`.

| Where | What happens to a message |
|---|---|
| Development (always) | recorded in memory and readable at `GET /dev/mailbox[?to=address]` (newest first, with `link` and `token`); `DELETE /dev/mailbox` clears it. The route is not mapped outside Development. |
| `Email:Smtp:Enabled=true` | additionally delivered over SMTP, off the request path (a background worker), so the time an e-mail takes can never show through a response. Failures are logged (template + masked recipient only) and change no response. |
| Otherwise | not delivered; a log line says so (masked recipient + template, never the link). |

SMTP settings are `Email:Smtp:{Enabled,Host,Port,Username,Password,EnableSsl,FromAddress,FromName}`. **Keep the credentials out of tracked files** — use user-secrets (the Host has a `UserSecretsId`) or environment variables (`Email__Smtp__Password`). Example with a Mailtrap sandbox inbox:

```bash
cd src/Host
dotnet user-secrets set "Email:Smtp:Enabled"  "true"
dotnet user-secrets set "Email:Smtp:Host"     "sandbox.smtp.mailtrap.io"
dotnet user-secrets set "Email:Smtp:Port"     "2525"
dotnet user-secrets set "Email:Smtp:Username" "<mailtrap username>"
dotnet user-secrets set "Email:Smtp:Password" "<mailtrap password>"
```

Then, with the API running, `curl -X POST http://localhost:5208/auth/password/forgot -H 'Content-Type: application/json' -H 'X-Requested-With: fynovio' -d '{"email":"admin@fynovio.local"}'` puts a reset mail in the Mailtrap inbox. Automated tests force `Email:Smtp:Enabled=false`, so they never send mail even when secrets are present. Queued mail is in memory only: a crash loses it (a transactional outbox is the follow-up if that matters).

Related switches (all off by default): `Authentication:SelfRegistration:Enabled` (public sign-up creates an identity and *nothing else* — no membership; outside Development it also needs `AcknowledgeUnverifiedEmail=true` because addresses are not verified), and `Authentication:Tokens:{InviteDays=7,PasswordResetMinutes=30,PasswordSetupHours=24}`.

A tenant administrator invites a member with `POST /tenants/{tenantId}/invitations` (bearer token, action `identity.membership.invite`); `GET /auth/me` reports `capabilities.canInviteMembers`. A development database that was bootstrapped *before* this action existed has no such grant for its administrators — recreate the database (or re-run the seed against a fresh one).

### Bootstrapping a tenant administrator (production)

There is no default credential. The first administrator of a tenant is created by an operator command that runs *instead of* the web server (never over HTTP):

```bash
Bootstrap__Enabled=true dotnet Host.dll bootstrap-tenant-admin \
  --tenant-id 1 --email admin@example.com --display-name "Jane Admin"
```

It refuses unless `Bootstrap:Enabled=true`, refuses a tenant that is already bootstrapped, and prints a **single-use password-setup link** (valid `PasswordSetupHours`) once to stdout — never to the logs. The operator hands it to the administrator, who sets a password through `/reset-password`; every further member arrives by invitation. Exit codes: `0` ok, `2` not enabled, `3` already bootstrapped, `64` bad arguments.

Add `--modules crm,collaboration` (comma-separated module keys) to enable business modules in the same run: the keys are validated **before** anything is created, and the modules are enabled after the administrator exists (a module that fails to enable does not hide the setup link — fix the cause and run `enable-tenant-module` for the remainder).

### Enabling a business module for a tenant (production)

A tenant administrator's grants are Access's own catalog only. Business modules (CRM and Collaboration today; Sales/Inventory later, through the same mechanism) arrive through their **versioned capability template**: each module publishes a manifest (permission sets of explicit action keys — never a wildcard — and system roles built from them) and the operator enables it for a tenant:

```bash
Bootstrap__Enabled=true dotnet Host.dll enable-tenant-module --tenant-id 1 --module collaboration
```

Enablement copies the module's **current** template version into ordinary tenant-local `Role`/`PermissionSet` rows (provenance: `origin_module_key`, `origin_version`), assigns the roles marked for administrators (CRM: `crm_manager`; Collaboration: `collaboration_user`) to the tenant's current administrators, bumps `TenantAccessRevision` and writes evidence + outbox in one transaction. It is idempotent by state: re-running reports `AlreadyEnabled` (exit `0`) and changes nothing, **even if the template has since moved to a newer version** — there is deliberately no reconciler and no silent propagation (Phase 1.5 decision); an upgrade path would be a new decision. It never adopts a role or permission set with a colliding key that a tenant authored itself (`TemplateKeyConflict`, exit `5`). Exit codes: `0` enabled/already enabled, `2` not enabled by configuration, `4` tenant not bootstrapped, `5` template key conflict, `64` bad arguments or unknown module. Module keys come from `Host.Modules.PlatformModules` — adding a module there is what makes it enable-able and registers its actions.

### Giving a tenant its first sales pipeline (production)

A tenant with the CRM module still cannot Open or move an opportunity until it has a pipeline. The platform does **not** invent a stage template — the operator names the stages, in order; the first is the entry stage:

```bash
Bootstrap__Enabled=true dotnet Host.dll provision-crm-pipeline --tenant-id 1 --name "Sales pipeline" --stages "Qualification,Proposal,Negotiation"
```

**Important:** To use custom stages, run `provision-crm-pipeline` **before** `enable-tenant-module crm`. Enabling CRM now auto-provisions a minimal one-stage "Sales pipeline" (safety net, so a tenant is never pipeline-less); if you provision custom stages first, that auto-provisioning becomes a no-op. Running `provision-crm-pipeline` after CRM is enabled succeeds with no changes (idempotent by state, same as running it twice).

It creates pipeline version 1 in one transaction under the tenant's RLS context. Like module enablement it is idempotent by state and never edits what exists: a tenant that already has a pipeline gets `already has a pipeline; nothing changed` (exit `0`), even when different stages are passed. A tenant that is not bootstrapped is refused (a typo in `--tenant-id` must not create configuration for a tenant that does not exist). Exit codes: `0` provisioned/already provisioned, `2` not enabled by configuration, `4` tenant not bootstrapped, `64` bad arguments (missing values, duplicate stage names ignoring case, more than 50 stages, names over 100 characters). Changing a pipeline afterwards has no path yet.

### Customers (Parties) and opportunities

`POST /crm/references/parties` (header `Idempotency-Key`; body `{ partyType: "Person"|"Organization", name, surname?, phone?, email? }`) registers a customer in the caller's tenant — gated by the CRM action `crm.reference.party.create` (held by `crm_sales_representative` and `crm_manager`, not `crm_viewer`), answering `201 { id, replayed }`. `POST /opportunities` now verifies the customer: an unknown or other-tenant party is `422 party_not_found`, and a merged party is stored as its surviving party. **Template note:** `crm.reference.party.create` was added to the CRM v1 permission set *in place* — no v2 — because enablement is copy-once (no reconciler) and nothing is in production yet; from the first production tenant on, a template content change must bump the version. A tenant enabled *before* this key existed does not receive it (recreate a development database).

### End-to-end tests (real API + PostgreSQL + browser)

```bash
scripts/e2e.sh                   # or: cd web && npm run e2e
scripts/e2e.sh e2e/login.e2e.ts  # arguments go to `playwright test`
```

The script starts a **throwaway PostgreSQL container** (removed on exit; nothing touches a local database), applies the migrations and the runtime role, builds and starts the real API (Development: dev seed and dev mailbox on, SMTP off, rate limits raised) and the Vite dev server, then drives your installed Google Chrome with Playwright (`E2E_BROWSER_CHANNEL=chromium` after `npx playwright install chromium` uses the bundled browser). It needs Docker, the .NET SDK and `cd web && npm ci`. Ports (all overridable, chosen not to collide with a normal dev setup): PostgreSQL `55432` (`E2E_PG_PORT`), API `5209` (`E2E_API_PORT`), Vite `5174` (`E2E_APP_PORT`). The suite covers sign-in/out and reload, deep-link return and open-redirect attempts, tenant selection and switching, invitation acceptance (new and existing accounts), forgot/reset/change password across two browser contexts, the registration-disabled screen, and token/tenant manipulation (edited claims, foreign tenants, missing CSRF header, foreign origin).

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

Add a new MasterData migration:

```bash
dotnet ef migrations add <Name> \
  --project src/Modules/MasterData/MasterData.csproj \
  --startup-project src/Modules/MasterData/MasterData.csproj \
  --output-dir Persistence/Migrations
```

EF Core migrations under `Persistence/Migrations/` are generated output — regenerate them with `dotnet ef migrations add`, never hand-edit a migration or its model snapshot. The one exception is RLS policy SQL, which has no EF Core model representation (see `AGENTS.md`, "Enforcement Scope").

## Documentation for contributors and coding agents

- **`AGENTS.md`** — the model-independent engineering contract: stack, code conventions, architecture and database rules, testing expectations, and generated-artifact policy. Read this first.
- **`CLAUDE.md`** — Claude Code-specific tool routing and methodology (retrieval order, when to use which skill, reasoning-effort guidance).
- **`docs/architecture-analysis/`** — current-state analyses (e.g. `CRM_CURRENT_STATE_ANALYSIS.md`) and target-model specs (e.g. `Enterprise_CRM_Target_Model_Binding_Implementation_Specification.pdf`) for a module.
- **`docs/schema/`** — the physical schema designs behind each module's EF Core model (`crm-sales-schema.md`, `identity-access-schema.md`, `tenant-network-schema.md`).
- **`docs/plans/`** — implementation plans, e.g. `2026-09-16-pilot-enforcement.md`, `2026-09-16-crm-target-model-phase0-delta-plan.md` (§5's five STOP-list questions all resolved; §12 has the revised dependency graph), `2026-09-16-masterdata-party-foundation.md` (design), `2026-09-16-masterdata-phase0.5-execution-plan.md` (task/step/commit execution plan — complete, MasterData's Party foundation is implemented), `crm-phase1/2026-09-16-crm-phase1-lifecycle-pipeline-execution-plan.md` (task/step/commit execution plan for Phase 1 — complete, merged to `main`).
- **`docs/ai-tooling.md`** — what AI tooling is active in this repo and why.
- **`docs/dotnet-guide.md`** — a .NET/EF Core primer for contributors coming from another ecosystem (e.g. Laravel), covering solution/project structure, DI, EF Core, migrations, and how this repo's connection-string resolution works.

This repo also maintains a Graphify code graph under `graphify-out/` (committed `graph.json`, `GRAPH_REPORT.md`, `manifest.json`) that AI coding agents use for fast, scoped code navigation instead of broad repository scans. It's kept current automatically by a local git `post-commit` hook.
