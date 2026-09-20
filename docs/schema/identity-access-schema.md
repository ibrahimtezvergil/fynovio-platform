# Identity + Access schema (PostgreSQL, `identity`/`access` schemas)

Physical schema decided in `docs/architecture-analysis/19_IDENTITY_ACCESS_AND_DEALER_NETWORK.md`
(in the research project, sibling to this repo). **Revision 4 (2026-09-17) — the Enterprise
Access Foundation Phase 1.5 rebuild:** `Permission`/`RolePermission` replaced by
`ActionRegistryEntry`/`PermissionSet`/`PermissionSetItem`/`RolePermissionSet`; `Role.TenantId`
is `NOT NULL`; `RoleAssignment` drops `ScopeType`/`ScopeId` entirely; `TenantAccessState` added;
Access's own `Outbox`/`Evidence`/`Idempotency` records added; RLS enabled on every tenant-scoped
table; `tests/Access.Tests` (33 tests) added; registered in `Host`. Design authority for this
revision:
- `docs/plans/enterprise-access-foundation/2026-09-17-enterprise-access-foundation-review.md` (round 1),
- `docs/plans/enterprise-access-foundation/2026-09-17-enterprise-access-foundation-review-round2.md` (round 2),
- `docs/plans/enterprise-access-foundation/2026-09-17-enterprise-access-foundation-review-round3-final.md` (round 3),
- `docs/plans/enterprise-access-foundation/2026-09-17-enterprise-access-owner-decisions-final.md` (round 4, the two owner
  decisions this revision implements),
- `docs/plans/enterprise-access-foundation/2026-09-17-enterprise-access-foundation-gap-closure.md` (the HOW-level technical
  decisions — table shapes, the bootstrap problem, the `ActionKey` format),
- `docs/plans/enterprise-access-foundation/2026-09-17-enterprise-access-foundation-execution-plan.md` (the task-by-task plan
  this revision was built from).

**Not yet implemented (deliberately, per the execution plan's scope lock — DESIGN/FREEZE, not
IMPLEMENT NOW):** Organization/Territory scope evaluation (`RoleAssignment` is tenant-wide only
in Phase 1.5 — org-node scope is an additive migration once Organization ships a real fact
provider); Sharing and Field Security evaluators; Restriction/forbid policies; ServicePrincipal/
Agent/Group principal types (`PrincipalType` enum has only `User`); ActingFor/impersonation on
`ActorContext`; direct `PermissionSet` assignment (only `Role → PermissionSet` exists); a
continuous system-template reconciler (Decision A stays OPEN — see round 4). **No CRM code was
touched by this revision** — `Opportunity.Reassign()` and any `Authorize()` call from a CRM
command handler are Phase 2 work; the `OwnedBy` scope term and `relation="owner"` grant exist
so Phase 2 can consume them without an Access contract change.

**Tenant network tables moved out, and `Network` is gone from `RoleAssignment` unconditionally**
(round 3 §6 — cross-tenant access is never modeled as an ordinary assignment scope). `NETWORKS`
and `TENANT_NETWORK_MEMBERSHIPS` in `docs/schema/tenant-network-schema.md` remain unassigned;
cross-tenant scenarios (a user genuinely belonging to another tenant; HQ reading governed
cross-tenant analytics; a future explicit federation grant) are handled outside `RoleAssignment`
when a concrete need arrives, not designed here.

## Diagram

```mermaid
erDiagram
    ACCOUNTS ||--o{ EXTERNAL_IDENTITIES : "authenticates via"
    ACCOUNTS ||--o{ TENANT_MEMBERSHIPS : "belongs to"
    ACCOUNTS ||--o{ ROLE_ASSIGNMENTS : "granted"
    ROLES ||--o{ ROLE_ASSIGNMENTS : "assigned as"
    ROLES ||--o{ ROLE_PERMISSION_SETS : grants
    PERMISSION_SETS ||--o{ ROLE_PERMISSION_SETS : "granted via"
    PERMISSION_SETS ||--o{ PERMISSION_SET_ITEMS : contains
    ACTIONS ||--o{ PERMISSION_SET_ITEMS : "referenced by"
    TENANTS ||--o{ TENANT_MEMBERSHIPS : has
    TENANTS ||--o{ ROLE_ASSIGNMENTS : "scoped to"
    TENANTS ||--|| TENANT_ACCESS_STATE : "revision for"

    ACCOUNTS {
        bigint id PK
        text email "NOT NULL"
        text display_name "NOT NULL"
        text locale
        timestamptz created_at
        timestamptz updated_at
    }

    EXTERNAL_IDENTITIES {
        bigint id PK
        bigint account_id FK "-> accounts"
        text issuer "NOT NULL, UNIQUE(issuer,subject)"
        text subject "NOT NULL"
        jsonb raw_claims
        timestamptz linked_at
    }

    TENANT_MEMBERSHIPS {
        bigint id PK
        bigint tenant_id "NOT NULL, UNIQUE(tenant_id,account_id), RLS"
        bigint account_id FK "-> accounts"
        text status "CHECK: invited|active|disabled"
        timestamptz invited_at
        timestamptz joined_at
        timestamptz disabled_at
        bigint row_version "NOT NULL DEFAULT 1, EF Core concurrency token (IHasRowVersion)"
    }

    ACTIONS {
        text action_key PK "natural key, e.g. access.role_assignment.grant"
        text owner_module "NOT NULL, e.g. Access, CRM"
        text resource_type "NOT NULL"
        text risk_class "nullable"
        boolean is_deprecated "DEFAULT false; deprecate-not-delete"
    }

    PERMISSION_SETS {
        bigint id PK
        bigint tenant_id "NOT NULL, UNIQUE(tenant_id,id), UNIQUE(tenant_id,key), RLS"
        text key "NOT NULL"
        text name "NOT NULL"
        text origin "CHECK: tenant|system_template"
    }

    PERMISSION_SET_ITEMS {
        bigint id PK
        bigint tenant_id "NOT NULL, RLS"
        bigint permission_set_id FK "-> permission_sets(tenant_id,id), composite"
        text action_key FK "-> actions"
        text relation "nullable, CHECK: NULL or 'owner'"
    }

    ROLES {
        bigint id PK
        bigint tenant_id "NOT NULL (system-role/NULL model retired), UNIQUE(tenant_id,id), UNIQUE(tenant_id,key), UNIQUE(tenant_id,name), RLS"
        text key "NOT NULL"
        text name "NOT NULL"
        text origin "CHECK: tenant|system_template"
    }

    ROLE_PERMISSION_SETS {
        bigint tenant_id "NOT NULL, RLS"
        bigint role_id FK "-> roles(tenant_id,id), composite"
        bigint permission_set_id FK "-> permission_sets(tenant_id,id), composite"
    }

    ROLE_ASSIGNMENTS {
        bigint id PK
        bigint tenant_id "NOT NULL, RLS"
        text principal_type "CHECK: 'user' (only value in Phase 1.5)"
        bigint account_id "no FK across access/identity schemas, indexed (tenant_id,account_id)"
        bigint role_id FK "-> roles(tenant_id,id), composite (AGENTS.md binding-core #1)"
        text source "CHECK: manual|bootstrap"
        bigint granted_by_account_id
        text reason "nullable"
        timestamptz valid_from
        timestamptz valid_to "nullable, CHECK: valid_to IS NULL OR valid_to > valid_from"
        bigint row_version "NOT NULL, EF Core concurrency token (IHasRowVersion)"
    }

    TENANT_ACCESS_STATE {
        bigint tenant_id PK "natural key, RLS"
        bigint revision "NOT NULL DEFAULT 0, the one canonical TenantAccessRevision counter"
        bigint row_version "NOT NULL, EF Core concurrency token"
    }
```

`access.outbox_messages`/`idempotency_records`/`evidence_records` follow the same shape as
CRM/MasterData's (module-local duplicates — a module may reference `Contracts` only), all
RLS-covered, `evidence_records` append-only at the runtime-role grant level.

## Design notes

### Account is the one deliberate exception to mandatory `tenant_id`
`08_MODULAR_MONOLITH_BOUNDARIES.md`'s global rule (research project, sibling to this repo) forbids "no tenant-free repository method for tenant records." `accounts` is not tenant-owned business data — it is the identity a `tenant_membership` row points at. Every other table here that carries `tenant_id` enforces it per the same convention CRM uses (tenant-safe composite FKs, RLS with `FORCE ROW LEVEL SECURITY`, tested against the unprivileged runtime role).

### Role is never a column on `tenant_memberships`
Membership (are you in this tenant, and are you active/invited/disabled) and authorization (what can you do, and where) are different questions with different lifecycles — matching GitLab/Sentry's two-tier role model (org-level + narrower project/team-level) referenced in `19_IDENTITY_ACCESS_AND_DEALER_NETWORK.md` §2.

### Revision 4: `Permission`/`RolePermission` → `ActionRegistryEntry`/`PermissionSet`/`PermissionSetItem`/`RolePermissionSet`
Round 1's decision #6 (`Role -> PermissionSet -> ActionKey`, PermissionSet as the first-class reusable capability bundle) replaces the old direct `Role -> Permission` join. `actions` keeps `Permission`'s old role as the platform-owned action vocabulary projection (natural `action_key` text PK instead of a surrogate `bigint`, matching every other business-key table in this schema doc), sourced from each module's own code manifest (`Access.Application.AccessActionCatalog` today — CRM/Sales register their own vocabularies when they add real enforcement, not seeded here). `PermissionSetItem.Relation` is Phase 1.5's only ABAC-shaped seam: `NULL` (unrestricted within the assignment's scope) or `'owner'` (only when `ResourceDescriptor.OwnerPrincipal` matches the acting principal) — a closed set, extended only when a new relation's evaluator actually exists.

### Revision 4: `Role.TenantId` is `NOT NULL` — the system-role/NULL-tenant model is retired
Round 4 (Decision A) settled the open question this doc's Revision 3 note left unresolved: system-provided roles are no longer represented as `tenant_id = NULL` catalog rows. Every `Role`/`PermissionSet` row is tenant-owned; a platform-template-provisioned one carries `Origin = 'system_template'` instead. **Still open (round 4, genuinely OPEN, not resolved by this revision):** the template *lifecycle* — whether/how a future `TenantLifecycle.ProvisionTenant` copies platform templates into a new tenant, and whether upgrades ever reconcile existing tenant rows. Phase 1.5 ships only `BootstrapTenantAccessHandler` (`Access.Application`), a narrow, explicitly-trusted, non-HTTP path that creates one tenant's first `Role`/`PermissionSet`/`RoleAssignment` directly — the concrete, minimal answer to the bootstrap problem (a default-deny system cannot gate the creation of its own first grant), not a general reconciler.

### Revision 4: `RoleAssignment` drops `ScopeType`/`ScopeId` entirely
Revision 3's `scope_type IN ('tenant','organization_unit','network')` mixed an organizational scope concept with a cross-tenant one in a single enum — flagged across all four review rounds as a real defect (round 1 decision #11, round 3 §6 "Network is NOT an ordinary RoleAssignment scope"). Every `RoleAssignment` in Phase 1.5 is implicitly tenant-wide; no column represents scope at all. Org-node scope will be a new, additive column pair when Organization ships a real `ResolveScopeAt`-shaped fact provider — not a value sitting unused (and always denied) in today's schema. Cross-tenant access (§ above) is never an ordinary assignment scope, full stop.

### `RoleAssignment` provenance: `source`, `granted_by_account_id`, `reason`
Added in Revision 4 so an assignment answers "who granted this, how, and why" without a separate audit table lookup — cheap now, expensive to backfill later (this was flagged as a missing enterprise capability — access review provenance — in round 1 §5). `source` is `manual` (via `GrantRoleAssignmentHandler`) or `bootstrap` (via `BootstrapTenantAccessHandler`, the one path with no `Authorize()` check).

### Concurrency token vs. `TenantAccessState.Revision` — two different counters, never conflated
`row_version` (via `Contracts.IHasRowVersion` + `Access.Persistence.RowVersionInterceptor`, unchanged since Revision 3) protects a single row from a lost update. `TenantAccessState.Revision` — new in Revision 4 — is the single canonical `TenantAccessRevision`: a tenant-wide monotonic counter that increments whenever effective authorization configuration changes (a `Role`, `PermissionSet`, `PermissionSetItem`, or `RoleAssignment` changes), carried on every `AuthorizationDecision`. Round 3 froze this as one concept, explicitly superseding the Revision 3 note's separate, still-undesigned "decision epoch" — they are the same thing, and no second revision/epoch counter should ever be introduced (round 3 freeze #17).

### `role_assignments.account_id` has no FK, even though it's the same assembly as `accounts`
`role_assignments` lives in the `access` schema, `accounts` in `identity`. The Identity+Access merge is an assembly-level pilot exception "with explicit internal ownership" (doc 08 §2) — not a promise the schema boundary stays fused. A real FK across `access` → `identity` would work today but block splitting them later, exactly what doc 07 §3 warns against for cross-schema FKs. Treated as a cross-module reference instead: plain `bigint` + a `(tenant_id, account_id)` index, no FK, same convention as `PrincipalRef`/`EntityRef` elsewhere. Every *other* FK in this schema stays real and, as of Revision 4, tenant-safe composite (`role_assignments.role_id → roles(tenant_id, id)`, `role_permission_sets`'s two FKs, `permission_set_items.permission_set_id → permission_sets(tenant_id, id)`) because every other reference is same-schema (`identity` → `identity` or `access` → `access`).

### Namespace layout, Revision 4
`src/Modules/Access/Domain/Identity/` (`Account`, `ExternalIdentity`, `TenantMembership`) and `Domain/Authorization/` (`ActionRegistryEntry`, `PermissionSet`, `PermissionSetItem`, `Role`, `RolePermissionSet`, `RoleAssignment`, `PrincipalType`, `TenantAccessState`) — named `Authorization`, not `Access.Domain.Access`, to avoid the stutter; `Persistence/AccessDbContext.cs` owns both schemas with `Persistence/Configurations/*` split one file per entity, matching CRM's layout. `Access.Outbox`/`Access.Evidence`/`Access.Idempotency` (new in Revision 4) mirror CRM's module-local shapes. `Access.Application` (new) holds the PDP (`AccessAuthorizer`), the query-scope resolver (`AccessScopeResolver`), the action registry seeder/service, and the Grant/Revoke/Bootstrap commands and handlers.

### `PrincipalRef` mapping
`external_identities.(issuer, subject)` is the physical backing for `Contracts/PrincipalRef.cs`, already committed. CRM's `opportunities.assigned_principal_issuer/subject` (Revision 3, `crm-sales-schema.md`) references this pair with no FK, per the `EntityRef`/cross-module-reference convention — Identity/Access owns this table, CRM never joins to it directly. Revision 4's `Contracts.ActorContext`/`AuthorizationRequest`/`IAuthorizer`/`IAccessScopeResolver` all key on `PrincipalRef` at the module boundary too — `Access` resolves it to its own internal `account_id` via `PrincipalResolver`, never leaking that internal id into `Contracts`.

**Amendment (Revision 2 review):** `ix_opportunities_tenant_id_assigned_principal_subject` (CRM migration `20260914063206_InitialCrmSchema.cs`, line 285) indexes `(tenant_id, assigned_principal_subject)` — `assigned_principal_issuer` is missing. `PrincipalRef.cs`'s own doc comment states identity is the `(issuer, subject)` *pair*; an index on subject alone contradicts the primitive it indexes. Checked the other two principal-bearing tables in the same migration: `idempotency_records`' primary key is already the composite `(tenant_id, principal_issuer, principal_subject, operation, idempotency_key)` — issuer is already in the key, no gap. `evidence_records` has no principal-based index at all (only `correlation_id` and `tenant_id/aggregate_type/aggregate_id`) — no gap either. Only `opportunities` needs fixing; still outstanding, unrelated to this revision.

### Revision 8 (2026-09-20) — Phase 2.5A Authentication Foundation
**Backend password-based authentication, session management, and token rotation:**
New tables in `identity` schema (not tenant-scoped, platform-global):
- `account_credentials` — password credential, unique normalized login email, lockout state machine (failed attempts, locked-until timestamp)
- `auth_sessions` — refresh token family session, account+active_tenant_id, revocation tracking (revoked_at, revoked_reason: logout|reuse_detected|password_reset|password_changed|expired|admin)
- `auth_refresh_tokens` — rotating token within a session; token_hash UNIQUE (SHA-256); rotation state (rotated_at, replaced_by_id); single-use, issued at expiry (idle+absolute window)
- `auth_events` — append-only audit log (event_type, outcome, account_id/tenant_id/session_id, correlation_id, ip_hash, detail jsonb with allow-listed keys); runtime role gets SELECT/INSERT only (no UPDATE/DELETE)

**RLS policy — additive complement to existing `tenant_isolation`:**
Added `membership_self_view` on `identity.tenant_memberships` (SELECT-only permissive):
```sql
USING (account_id = NULLIF(current_setting('app.account_id', true), '')::bigint)
```
Allows an authenticated account (via `SetAccountContextAsync` after credential/refresh validation) to list only its own `TenantMembership` rows across tenants before tenant selection. Uses the `app.account_id` GUC, set transaction-locally in server code only after authentication. No write path — never narrows or bypasses `tenant_isolation`, only supplements SELECT.

**Contract & lifecycle:**
- Login: normalize email (trim+NFC+lowercase) → credential lookup → lockout check → password verify (dummy-verify for unknown accounts) → create session+token → load active memberships via `SetAccountContextAsync` + `membership_self_view` policy → auto-select if exactly one, else return selection-required
- Refresh: parse token, verify hash (constant-time), check expiry and revocation, detect reuse (rotated token outside grace window → revoke session family), rotate to new token, validate active membership still exists, return new access token
- Logout: idempotent revocation
- Tenant select: membership validation (identical error for unknown/non-member to prevent enumeration), set active_tenant_id, return new access token with new tid
- Session validation: on every protected API request, check session not revoked and not expired (one indexed lookup)
- Password/session reset on password change: all other sessions revoked, current kept, audit event

**Tables are deliberately account-scoped/global, never tenant-scoped:**
- Account identity and sign-in credential are pre-tenant concerns (authentication occurs before tenant selection)
- Session/token state is account-owned, not tenant-owned (a session may span tenant selection/switching)
- Audit events include pre-tenant events (login, forgot-password requests) that cannot use tenant-scoped `evidence_records`
- Composite FKs to accounts; no tenant_id on these tables, no RLS policies (except the additive `membership_self_view` on the cross-referenced `tenant_memberships`)

**Grants in `scripts/create-runtime-role.sql`:**
- `fynovio_app` role gets SELECT/INSERT/UPDATE/DELETE on the new `account_credentials`, `auth_sessions`, `auth_refresh_tokens` tables (via default-privileges on `identity` schema + USAGE/SELECT on `auth_events_id_seq`)
- `auth_events` table: runtime role can only INSERT and SELECT (REVOKE UPDATE, DELETE) — append-only audit log enforcement
- Both enforced at the grant level (simpler than triggers; matches `evidence_records` pattern)

### Revision 9 (2026-09-20) — Phase 2.5A invitations and password lifecycle (`account_tokens`)

`identity.account_tokens` holds the three single-use, single-purpose opaque tokens: `invite`, `password_reset`, `password_setup` (the last is the production bootstrap path — an account without a credential receives one).

| Column | Notes |
|---|---|
| `id` (uuid, PK) | first half of the raw token `<id>.<secret>` |
| `purpose` | `invite` \| `password_reset` \| `password_setup` (CHECK `ck_account_tokens_purpose`) |
| `token_hash` (UNIQUE) | SHA-256 of the secret. **The secret itself is never stored, logged, evidenced or returned after issue.** |
| `account_id` (FK accounts, nullable) | required for `password_reset`/`password_setup` (CHECK `ck_account_tokens_account_purposes_have_account`) |
| `tenant_id`, `email_normalized`, `display_name`, `locale` | invitations only; `tenant_id` + `email_normalized` are required for `invite` (CHECK `ck_account_tokens_invite_shape`) |
| `expires_at`, `consumed_at`, `revoked_at`, `created_at`, `created_by_account_id` | lifecycle; TTLs are configuration (`TokenOptions`: 7 d / 30 min / 24 h by default) |

Indexes: unique `token_hash`; `account_id`; a partial index on `(email_normalized, tenant_id)` for outstanding invitations (re-inviting revokes the previous one).

**Deliberate design points**
- **Outside tenant RLS, like `accounts`/`external_identities`/the Revision 8 tables.** A token must be redeemable before any tenant is known, and there is no list/search endpoint; the row carries `tenant_id` only as data. Reads and writes of tenant-scoped rows that redemption triggers (the membership) happen afterwards inside the invitation's tenant context.
- **Accounts are created when an invitation is ACCEPTED, not when it is issued.** `accounts.email` is intentionally not unique, so creating the account at invite time would mint duplicates for re-invites and make "does this address have an account" observable. The invitation therefore carries the invitee's address, and acceptance either creates the account + credential + platform identity or — when the address already has a credential — requires the existing password.
- **Single use is an atomic compare-and-set** (`UPDATE … SET consumed_at = now WHERE id = @id AND consumed_at IS NULL AND revoked_at IS NULL AND expires_at > now`, affected rows must be 1), performed first in the redeeming transaction, so concurrent redemptions produce exactly one winner and the losers create nothing. A wrong password or a policy violation happens BEFORE consumption and leaves the token usable.
- **Uniform failure:** unknown, malformed, wrong-secret, wrong-purpose, expired, consumed and revoked tokens all produce the same result (`InvalidOrExpiredToken`), and an unknown id still costs one constant-time hash comparison.
- **Sessions:** a password reset revokes ALL sessions of the account; a password change revokes all OTHER sessions and keeps the caller's. Both also revoke the account's other outstanding reset tokens.
- **Runtime role:** the existing schema-wide grant already gives `fynovio_app` SELECT/INSERT/UPDATE/DELETE on the table; the application only needs SELECT/INSERT/UPDATE (consuming and revoking are updates) and never deletes.
- **Action registry:** `identity.membership.invite` (`Identity.TenantMembership`, risk class `high`) is added to `AccessActionCatalog`; `BootstrapTenantAccessHandler` grants the whole catalog, so tenant administrators created **after** this revision hold it automatically. Tenants bootstrapped earlier (development databases) do not — re-bootstrap or grant it.
- **Evidence:** creating an invitation and activating the membership write tenant-scoped `evidence_records` + outbox (an aggregate id, an invitation id and a hash of the address — never the address or the token); every flow also writes an allow-listed `auth_events` row (`invite_created`, `invite_accepted`, `password_reset_requested`, `password_reset_completed`, `password_setup_completed`, `password_change`, `registration_created`, `bootstrap_completed`).
- **Known limitation:** the bootstrap command creates the account/membership, then runs the (separately transactional) Phase 1.5 `BootstrapTenantAccessHandler`, then issues the setup token. A crash between the steps leaves a credential-less account behind (harmless: it cannot sign in) and a re-run creates a second one.

### Revision 10 (2026-09-20) — Phase 2.6 module capability templates (`tenant_module_enablements`, template provenance)

The answer to "how does a tenant get a business module's roles" (Phase 2.5B OD2), built on the frozen Phase 1.5 rule: **template → tenant-local copy at an explicit, operator-invoked step; no reconciler, no silent propagation.** Nothing here adds a permission model — enablement writes ordinary `roles`/`permission_sets`/`permission_set_items`/`role_permission_sets`/`role_assignments` rows that the existing PDP already reads.

| Change | Notes |
|---|---|
| `roles.origin_module_key` (varchar 64, null), `roles.origin_version` (int, null) | provenance of a template-derived row. CHECK `ck_roles_provenance`: both null or both set; set only with `origin = 'system_template'`; version ≥ 1. |
| `permission_sets.origin_module_key` / `origin_version` | same shape and CHECK (`ck_permission_sets_provenance`). Tenant-authored rows keep both null. Access's own bootstrap now records provenance `("access", 1)`. |
| `role_assignments.source` gains `module_enablement` | CHECK `ck_role_assignments_source` widened; the assignments an enablement makes (CRM: the administrators receive `crm_manager`) are distinguishable from manual and bootstrap grants. |
| `tenant_module_enablements` (`id`, `tenant_id`, `module_key`, `template_version`, `enabled_at`) | one row per (tenant, module) — UNIQUE `(tenant_id, module_key)` is the idempotency key; CHECK `template_version >= 1`. **RLS enabled and forced** (hand-written migration `EnableModuleEnablementRowLevelSecurity`, same policy shape as the other tenant tables). |

**Deliberate design points**
- **Manifest lives in `Contracts`, composed by `Host`.** `ModuleCapabilityManifest`/`PermissionSetTemplate`/`RoleTemplate` (Contracts) are published by each module (`CrmModuleCapabilities`); `Host.Modules.PlatformModules` composes the union and hands it to Access — Access never references CRM (same pattern as `AccessActionCatalogSeeder`). The same union is the action registry seeded at start-up, so a module's action keys cannot be deprecated by another manifest's seeding.
- **No wildcards, fail closed.** Template items are explicit action keys (`ModuleCapabilityManifest.Validate()` rejects `*`), and enablement refuses a manifest that references an action key that is not an active registry entry.
- **Never modifies what it did not create.** A tenant already enabled at version N is `AlreadyEnabled` and untouched by a newer template. A colliding `roles.key`/`permission_sets.key` that is not template-derived from this module yields `TemplateKeyConflict` — a tenant-authored row is never adopted or overwritten.
- **Administrators only at enablement time.** Roles flagged `GrantToTenantAdministrators` go to the tenant's *current* active administrators; members added later receive roles through normal grants. Every write bumps `TenantAccessRevision`; evidence + outbox are written in the same transaction; a unique-violation race between two operators resolves to `AlreadyEnabled`.
- **Assignable principals are not stored anywhere.** `IAuthorizedPrincipalDirectory` (Contracts, implemented in `Access.Application`) restates the PDP's grant rule set-based over active members (platform-issuer identity, all required actions permitted as `owner`); `AuthorizedPrincipalDirectoryTests` proves it agrees with `AccessAuthorizer` member-for-member over a grant matrix. Team/org/territory/record-policy fact providers still do not exist (gap-closure §4), so the directory consumes exactly what the PDP evaluates today; it is the extension point when the PDP grows.

### Not yet designed here
- `delegations` and `sod_rules` tables (decision file §2) — named but not column-designed yet; DESIGN/FREEZE per the Phase 1.5 execution plan's scope lock, deferred until a concrete delegation/SoD scenario is scoped.
- Sharing, Field Security, Restriction/forbid policy tables — DESIGN/FREEZE; the evaluators are logical Access subdomains (round 3 ownership matrix) but no schema exists yet.
- Organization-node/Territory scope columns on `RoleAssignment` — added additively once a real fact provider exists (see the Revision 4 note above).
- `ServicePrincipal`/`AgentIdentity`/`Group` principal types, `ActingFor`/impersonation chain on `ActorContext` — DESIGN/FREEZE, no schema or Contracts fields yet.
- A `crm.opportunity.*` action vocabulary in `access.actions` — not seeded by this revision; Phase 2 registers it when CRM adds real command-level enforcement.
- A production e-mail provider (the `IEmailSender` abstraction exists; the Host ships a development sink only) — deferred, see the Phase 2.5A plan §14 D5
