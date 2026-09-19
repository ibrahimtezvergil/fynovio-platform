# PHASE 1.5 RUNTIME RLS PDP DELTA

**Status:** Resolved 2026-09-20. Discovered while closing a CRM Phase 2 P0 test gap
(HTTP-level non-leak tests in `tests/Host.Tests/OpportunityEndpointsTests.cs`); fixed as a
narrow, separate Phase 1.5 Architecture Delta per explicit owner decision before Phase 2
work resumed.

## Problem

`Access.Application.AccessAuthorizer`, `Access.Application.AccessScopeResolver`, and
`Access.Application.PrincipalResolver.IsActiveTenantMemberAsync` query Access-schema tables
that are RLS-enabled and forced (`identity.tenant_memberships`, `access.role_assignments`,
`access.role_permission_sets`, `access.permission_set_items`, `access.tenant_access_state` —
see `20260917084937_EnableAccessRowLevelSecurity.cs`). The RLS policy on each is:

```sql
USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
```

None of these three classes ever called `AccessDbContextTenantExtensions.SetTenantContextAsync`
before querying. Under the real, unprivileged `fynovio_app` runtime role this policy filters
out every row when `app.tenant_id` is unset — so `PrincipalResolver.IsActiveTenantMemberAsync`
(called by `Host.Authentication.ActorContextMiddleware` on every authenticated request) would
always return `false`, producing a permanent 403 for every request regardless of real
membership data, and `AccessAuthorizer`/`AccessScopeResolver` would always fail-closed-deny
regardless of real grant data.

## Why every existing test suite missed it

Every integration test that exercised these three classes — `Access.Tests` (via
`AccessTestFixture`/`PostgresFixture.CreateAdminContext()`), `CRM.Tests`
(`PostgresFixture.CreateAccessContext()`), and, until this session, `Host.Tests` (which set
`ConnectionStrings__*` directly to the Testcontainers container's default `postgres`
superuser) — ran against a superuser/table-owner connection. Superusers and `BYPASSRLS`
roles bypass RLS unconditionally, independent of policy correctness, so the missing
`SetTenantContextAsync` calls never manifested as a test failure. The defect surfaced only
after `Host.Tests` was fixed (separately, same session) to run the application under a real
`fynovio_app` role built from `scripts/create-runtime-role.sql` — at which point three
otherwise-trivial new HTTP tests, including a plain "authorized caller creates an
opportunity" happy path, started failing with 403.

## Binding principle

**Tenant execution context ≠ authorization grant.** The claimed tenant identifier (from the
validated JWT `tid` claim, already checked by `ActorContextMiddleware` before this code runs)
selects which tenant's RLS partition a query can see. It is not itself an authorization
decision. Every PDP branch downstream still independently re-checks `TenantId`/`AccountId`/
`ActionKey`/relation before granting — RLS is a second, defense-in-depth visibility boundary
on top of the same predicates the LINQ queries already apply, not a replacement for them.

```text
Validated JWT
  -> External Identity / Principal bootstrap (identity.external_identities — global, no RLS)
  -> Resolve claimed TenantId (JWT "tid" claim)
  -> Establish tenant-scoped DB execution context (this delta)
  -> TenantMembership check (RLS-protected)
  -> Coarse capability evaluation (RLS-protected)
  -> Record-level / owner-relation check
  -> Domain command
```

## Chosen implementation

**Tenant source:** the same value each call already receives as a parameter —
`PrincipalResolver.IsActiveTenantMemberAsync(principal, tenantId)`'s `tenantId`,
`AccessAuthorizer.AuthorizeAsync(request)`'s `request.Actor.TenantId`,
`AccessScopeResolver.ResolveAsync(actor, ...)`'s `actor.TenantId`. No new tenant-resolution
path was introduced.

**Transaction boundary / mechanism:** the existing, single canonical primitive —
`context.Database.BeginTransactionAsync()` + `AccessDbContextTenantExtensions.SetTenantContextAsync`
(`SELECT set_config('app.tenant_id', <value>, true)`, transaction-local via the `true`
third argument) + `transaction.CommitAsync()`. This is the exact pattern every CRM/MasterData
handler and Access's own `BootstrapTenantAccessHandler`/`GrantRoleAssignmentHandler`/
`RevokeRoleAssignmentHandler` already use — no second mechanism was created.

**DbContext/connection lifecycle:** `AccessDbContext`, `PrincipalResolver`, `AccessAuthorizer`,
`IActionCatalog`, and `IAccessScopeResolver` are all registered `Scoped` (`Program.cs`), so
within one HTTP request they share one `AccessDbContext` instance/connection. Because
`set_config(..., true)` is transaction-local, each PDP call's context setting is scoped to
its own begin/commit and never survives past it — sequential PDP calls on the same request
scope (e.g. `ActorContextMiddleware`'s membership check, then a later `AuthorizeAsync` call)
never see stale context from an earlier call, and a pooled connection reused by the next
request never inherits the previous request's tenant.

**Reentrancy:** `GrantRoleAssignmentHandler` and `RevokeRoleAssignmentHandler` open their own
transaction and set tenant context *before* calling `authorizer.AuthorizeAsync` on the same
`AccessDbContext` instance — opening a second transaction on an already-transacted connection
throws (`InvalidOperationException`, confirmed by the first test run after this fix). Each of
the three methods now checks `context.Database.CurrentTransaction`: if non-null, it evaluates
directly inside the caller's already-scoped transaction/tenant-context; if null, it owns its
own transaction end-to-end. This is the same rule applied uniformly to all three entry points
rather than three different transaction strategies.

## Security properties

- **Fail closed:** unchanged. Every branch still denies/returns `None` unless a real,
  tenant-and-account-matched grant is found; RLS now additionally prevents any row outside
  the claimed tenant from ever reaching those branches.
- **No BYPASSRLS in production:** not touched — the runtime role stays `NOSUPERUSER
  NOBYPASSRLS`.
- **No tenant-context leakage:** `set_config(..., true)` is transaction-scoped; proven by
  `PdpRuntimeRoleTests.Sequential_authorization_calls_on_the_same_connection_do_not_leak_tenant_context`
  (Tenant A allow, then Tenant B deny, same `AccessDbContext`/connection, same test).
- **No cross-tenant grant visibility:** proven by
  `PdpRuntimeRoleTests.Tenant_wide_grant_authorizes_under_its_own_tenant_and_is_invisible_under_another_tenant`.
- **No caller-controlled arbitrary DB context:** unchanged — the tenant value is always the
  already-validated `ActorContext.TenantId`/JWT-derived `tenantId` parameter, never read from
  a header/query/body inside these three classes.

## Tests

- `tests/Access.Tests/Integration/PdpRuntimeRoleTests.cs` (new, runs against
  `PostgresFixture.RuntimeConnectionStringAsync()`, i.e. the real `fynovio_app` role):
  - `Membership_resolves_under_the_members_own_tenant_but_not_under_a_foreign_tenant`
  - `Tenant_wide_grant_authorizes_under_its_own_tenant_and_is_invisible_under_another_tenant`
  - `Owner_relation_grant_authorizes_only_the_owned_resource_under_the_runtime_role`
  - `Sequential_authorization_calls_on_the_same_connection_do_not_leak_tenant_context`
- `tests/Access.Tests/Integration/AccessRlsTests.cs` (pre-existing, unchanged) continues to
  prove the underlying RLS policies themselves are correct at the raw-SQL level.
- `tests/Host.Tests/OpportunityEndpointsTests.cs`'s 3 new HTTP tests (Phase 2 Step 2) now
  pass under the real runtime role without reverting to the superuser connection —
  end-to-end proof through `ActorContextMiddleware` -> `AccessAuthorizer` -> CRM command.

## Results

- Access.Tests: 64/64 (60 pre-existing unchanged + 4 new runtime-role PDP tests).
- CRM.Tests: 125/125.
- MasterData.Tests: 30/30.
- Host.Tests: 8/8, including the 3 tests that exposed this defect.

## Remaining architectural risk

None identified that blocks Phase 2. The fix is scoped to the three PDP entry points that
had the gap; no other Access.Application class queries RLS-protected tables without already
following this pattern (verified: `BootstrapTenantAccessHandler`, `GrantRoleAssignmentHandler`,
`RevokeRoleAssignmentHandler` already did).

**Ready to resume Phase 2: YES.**
