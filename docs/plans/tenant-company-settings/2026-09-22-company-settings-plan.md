# Company settings — execution plan

## Decision record

- **User-facing name:** Şirket Ayarları / Company settings.
- **Route:** `/company/settings`; it is a tenant administration utility, never a section of
  `/profile/settings`.
- **Module owner:** `TenantLifecycle`. The first requirement is the tenant's own display and
  operating profile, exactly the deferred tenant-display-name capability. Legal-entity hierarchy
  remains outside this slice in the future `Organization` module.
- **Initial scope:** profile/legal identity/contact/default locale only. Logo/file storage,
  invoice numbering, banking data, working calendars, hierarchy, SSO and SCIM are explicitly out
  of scope; each has a separate ownership and risk model.
- **Authorization:** `tenant.settings.view` and `tenant.settings.update`. The v1 tenant-admin
  role receives both; all other roles are denied until an administrator grants them.

## S1 — tenant profile foundation

1. Replace the `TenantLifecycle` scaffold with Domain, Application, Persistence, outbox and
   idempotency layers; add `TenantProfile` with a natural `TenantId` identity.
2. Generate the EF Core schema migration and the otherwise-empty RLS migration. Register the
   module DbContext in Host and grant the runtime role in `create-runtime-role.sql`.
3. Add `GetCompanySettings` and idempotent/concurrency-protected `UpdateCompanySettings` handlers.
   Authorize before idempotency lookup; update state, outbox and idempotency in one transaction.
4. Add tenant-safe persistence, RLS-as-runtime-role, invariant, concurrency, idempotency, outbox,
   and module-boundary tests.

## S2 — authorization and HTTP

1. Add the two action keys, action catalog entries and `TenantLifecycle` module capability
   manifest. Compose them from `Host.Modules.PlatformModules`.
2. Map authenticated `GET`/`PUT /company/settings` endpoints. Bodies have a bounded streaming
   parser, malformed inputs are 400, authorization is 403, stale versions are 409, and an absent
   profile is 404.
3. Add Host HTTP tests for actor-derived tenancy, validation, default denial, update/replay, and
   response shape.

## S3 — frontend

1. Add a `company-settings` feature with a React Query query/mutation boundary and a Zod contract.
2. Add `/company/settings` as a utility route and the available utility registry entry. It is not
   placed in the user menu, which remains personal-account navigation.
3. Build one company-profile form with server row-version concurrency handling and the existing
   save-bar/form primitives. Use the existing time-zone and currency input patterns; do not share
   the personal Settings Zustand store.
4. Add `tr`/`en` catalog entries, focused tests, responsive/light/dark browser verification and
   an API-backed E2E path.

## Definition of done

- Schema matches `docs/schema/tenant-lifecycle-schema.md`, migrations are generated, and every
  tenant-scoped table has forced RLS exercised as `fynovio_app`.
- Every profile mutation is authorized, idempotent, optimistic-concurrency protected and commits
  its outbox fact atomically.
- The UI has no mock company data and cannot send a tenant identifier.
- `dotnet build`, relevant .NET suites, `dotnet format`, web lint/typecheck/tests/build and the
  affected E2E flow pass.
