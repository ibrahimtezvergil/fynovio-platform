# Phase 2.5A — Authentication Frontend: Implementation Plan

> **Status:** approved for implementation, 2026-09-20. **Source spec:** `docs/Enterprise_Phase_2_5A_Authentication_Frontend_Implementation_Prompt.pdf` (binding task definition). **Owner decisions:** Ibrahim, 2026-09-20 (§0). **Branch:** `feat/phase-2-5a-auth`.
> Phase 2.5A is one piece of work executed in slices. It is complete only when **every** capability in §16 is done, tested and reviewed. Phase 2.5B (CRM UI) is out of scope.

## 0. Binding owner decisions (2026-09-20)

1. **Session model.** Short-lived bearer **access JWT held in browser memory only** + **rotating HttpOnly refresh cookie**. Refresh rotation with token reuse/replay detection. Reload restores the session through the refresh endpoint. No auth token in `localStorage`/`sessionStorage`. Existing chain `JWT → ActorContextMiddleware → Phase 1.5 authorization` is preserved. Tenant switching is server-side: validate membership, mint a new access JWT with the new `tid`; the frontend can never create or change a tenant claim.
2. **Identity/credential model.** `Access.Account` stays the canonical user identity. **No** parallel `IdentityUser`/`AspNetUsers` domain. Microsoft Identity *primitives* (password hasher) may be used; no custom crypto. Reset/invite tokens: cryptographically random opaque, **only the hash stored**, expiring, single-use, with consumed/revoked state.
3. **Bootstrap.** Development: a safe dev seed. Production: no hard-coded/default admin credential; an explicit one-time bootstrap mechanism (reuse `BootstrapTenantAccessHandler`); afterwards onboarding is invite-based. **Public self-registration is DISABLED by default.**
4. **Execution.** Sliced, no re-approval between slices. Stop only for a missing *binding* decision or a real conflict with existing decisions (→ `OPEN DECISION`).
5. **Frontend branch.** The `web/` import is the canonical frontend. Already integrated into `main` (merge `bf1b0e4`, verified: 545 tracked files under `web/`, backend untouched, `npm ci` + lint + typecheck + 62 tests + build green).
6. **Security.** CSRF threat model for the cookie endpoints is resolved explicitly (§6). No wildcard CORS. Prefer same-origin deployment. Rate limiting, expiry, replay protection and audit/evidence are designed in.
7. **Completion.** This plan → implementation → tests → security review → `PHASE_2_5A_AUTH_FRONTEND_FINAL_REPORT.md`. Stop at the final report.

## 1. Verified current state

### 1.1 Backend (verified by reading code, not docs)
| Area | Fact | Evidence |
|---|---|---|
| Token validation | JwtBearer, HMAC-SHA256, `MapInboundClaims=false`, issuer/audience/lifetime validated, `NameClaimType="sub"`, default ClockSkew (5 min) | `src/Host/Program.cs` |
| Token issuance | **None.** Only `tests/Host.Tests/JwtTestTokenFactory.cs` mints tokens (`sub`,`tid`, issuer/audience from constants). Phase 2 plan explicitly deferred issuance to "Identity's public surface". | `docs/plans/crm-phase2/…execution-plan.md` |
| Middleware order | `UseAuthentication` → `UseAuthorization` → `ActorContextMiddleware` → `UseExceptionHandler`; **no** CORS, CSRF, rate limiting, forwarded headers, HTTPS redirect, cookie auth | `Program.cs` |
| ActorContext | Built from `iss`,`sub`,`tid` claims; missing/unparsable → 401; not an **Active** membership → 403; stores `ActorContext` in `HttpContext.Items` | `src/Host/Authentication/ActorContextMiddleware.cs` |
| Errors | `CrmProblemDetailsExceptionHandler` → RFC 7807 `ProblemDetails` with `type` values (`not_found`, `validation_error`, `concurrency_conflict`, …). Record-level denial → 404 (non-leaking). | `src/Host/Endpoints/CrmProblemDetailsExceptionHandler.cs` |
| Endpoints | `/`, `/health/db`, `/opportunities/*`, `/pipelines/*`. **No** `/auth/*`. No OpenAPI. | `Program.cs`, `OpportunityEndpoints.cs` |
| Identity domain | `identity.accounts` (`Email` non-unique **by design**, profile only; comment forbids a unique constraint), `identity.external_identities` (`UNIQUE(issuer,subject)`), `identity.tenant_memberships` (`Invited|Active|Disabled`, RLS-covered). **No credential/password/session/token tables.** No `Tenant` entity (TenantLifecycle is a stub; tenant id is a bare `bigint`). | `src/Modules/Access/Domain/Identity/*`, `docs/schema/identity-access-schema.md` |
| Principal resolution | `PrincipalResolver`: `(iss,sub)` → `ExternalIdentity` → `Account` → active `TenantMembership` (sets `app.tenant_id` via `SetTenantContextAsync` inside a transaction). `accounts` and `external_identities` are **not** RLS-covered; `tenant_memberships` is. | `src/Modules/Access/Application/PrincipalResolver.cs`, migration `…EnableAccessRowLevelSecurity` |
| Bootstrap | `BootstrapTenantAccessHandler`: no `Authorize()` by design; requires a pre-linked `ExternalIdentity`; refuses if tenant already has `TenantAccessState`; writes evidence+outbox. | `…/BootstrapTenantAccessHandler.cs` |
| Evidence | `access.evidence_records` is **tenant-scoped + RLS**, append-only for the runtime role. Pre-tenant events (failed login, forgot-password) cannot use it. | `create-runtime-role.sql`, RLS migration |
| Infra | .NET 10, EF Core 10/Npgsql 10, JwtBearer 10.0.4. **No** clock abstraction, email abstraction, OpenTelemetry, Serilog. `TreatWarningsAsErrors` deferred. | csproj files |
| Tests | Testcontainers Postgres 17 fixtures; Host tests use `WebApplicationFactory<Program>` + `JwtTestTokenFactory` + `SeedGrantAsync` (Account, ExternalIdentity, active membership, role, grant). NetArchTest module-boundary rules. | `tests/*` |

### 1.2 Frontend (`web/`, verified)
| Area | Fact |
|---|---|
| Auth today | Mock only. `features/auth/api.ts` `POST /auth/login` handled by MSW (`mocks/handlers/auth.ts`) — accepts anything ≥8 chars, returns `mock-token` + hard-coded user. `LoginForm` pre-fills `deniz@fynovio.com / fynovio123`. |
| Auth state | `features/auth/store/useAuthStore.ts`: zustand **persisted to `localStorage['fynovio-auth']`** (`isAuthenticated`, `user`, **`token`**). Violates decision §0.1 → must go. |
| Consumers of the auth store | `home/GreetingSection`, `components/data-table/lib/viewState.ts`, `layouts/components/UserMenu.tsx`, `lib/permissions/usePermission.ts`, `routes/ProtectedRoute.tsx`, `routes/PublicOnlyRoute.tsx`. (Some import across feature boundaries → the shared session module moves out of `features/`.) |
| Routing | `routes/index.tsx`: `PublicOnlyRoute → AuthLayout → login`, `ProtectedRoute → DashboardLayout → app`. Guard passes `state.from` (pathname only). No 403/no-access page, only `NotFoundPage`. |
| API layer | `api/client.ts` axios, baseURL `VITE_API_URL ?? '/api'`, module-level bearer, `ApiError` normalisation (Laravel-style 422 `fields`). **No 401/403 handling, no `withCredentials`, no tenant or correlation header.** `QueryClient`: `retry:1`, `throwOnError:true`. |
| Mocking | MSW started in `main.tsx` unless `VITE_API_MOCKING=disabled`; vitest setup errors on unhandled requests. |
| Tooling | No Vite dev proxy. oxlint enforces "features never import features" and "features never import router/guards". No Playwright. Node `^22.13`. |
| Reusable | `PasswordInput`, `Field`, shadcn `Card/Button/Input/Label/Alert`, react-hook-form + zod pattern (`LoginForm`), i18n namespace `auth` (tr default, en), `AuthLayout`, `useAppMutation`, Sonner. |
| Wire contract drift | Frontend endpoint map is mock-oriented (e.g. `/auth/login`); backend routes have **no `/api` prefix**. Vite proxy will strip `/api`. CRM endpoint alignment is Phase 2.5B. |

## 2. Target architecture — identity & session

```
Browser (SPA, memory: access JWT)                 Host (.NET)
  │  POST /api/auth/login  ───────────────────────►  validate credential (hash verify, lockout, rate limit)
  │  ◄─ Set-Cookie: refresh (HttpOnly,Secure,SameSite=Strict, scoped path)
  │  ◄─ 200 { status, accessToken?, account, memberships, activeTenant? }
  │
  │  Authorization: Bearer <access JWT: iss,aud,sub,tid,sid,jti,exp>
  │  ───────────────────────────────────────────►  JwtBearer → ActorContextMiddleware
  │                                                   (+ session-not-revoked check via `sid`) → Phase 1.5 PDP
  │  POST /api/auth/refresh (cookie) ─────────────►  rotate refresh token; reuse ⇒ revoke whole session family
  │  POST /api/auth/tenants/select (cookie) ──────►  membership check ⇒ new access JWT with new `tid`
```

**Key design choices**
- **Bearer tokens always carry `tid`.** A user with N>1 memberships and no selected tenant gets **no access token**; the session (refresh cookie) exists, the response says `tenant_selection_required`, and `POST /auth/tenants/select` (cookie-authenticated) returns the tenant-scoped access JWT. This leaves `ActorContextMiddleware` semantics untouched and avoids tenant-less bearer tokens.
- **Platform IdP identity.** Each account that can sign in with a password has one `ExternalIdentity(issuer = Authentication:Jwt:Issuer, subject = random opaque id)`. Access tokens use that `(iss, sub)`, so `PrincipalResolver`, PDP and evidence keep working unchanged and a future external IdP just adds another `ExternalIdentity`.
- **Immediate revocation.** Access JWT includes `sid` (session id). `ActorContextMiddleware` additionally verifies the session is not revoked/expired (one indexed lookup, same transaction as the membership check). Logout, reuse detection, password reset and "revoke other sessions on password change" therefore take effect on protected APIs immediately, not after access-token expiry. Config `Authentication:Session:RequireSessionClaim` (default `true`; the legacy `JwtTestTokenFactory` environment sets `false` so Phase 0.5–2 tests keep their meaning).
- **Login handle is a credential concern, not an Account concern.** `Account.Email` stays profile data (no unique constraint, per the existing binding comment). Uniqueness of the sign-in handle lives on `identity.account_credentials.login_email_normalized UNIQUE`.
- **Code location.** Auth data model + application handlers live in the `Access` assembly under `Domain/Authentication` and `Application/Authentication` (the `identity`+`access` schemas already share one assembly/DbContext by the documented pilot exception; a separate `Identity` module would have to reference Access tables, which the module-boundary rules forbid). JWT signing, cookie handling, CORS, rate limiting and HTTP endpoints live in `Host`. Extraction into an `Identity` module is deferred (§14). CRM contains no auth code.

### 2.1 Access token
`iss`, `aud` (existing options), `sub` (platform ExternalIdentity subject), `tid`, `sid`, `jti`, `iat`, `exp`. **TTL 10 min** (`Authentication:Jwt:AccessTokenMinutes`). `ClockSkew` set explicitly to 30 s. No PII beyond ids in the token.

### 2.2 Refresh token & cookie
- 256-bit random secret, base64url; DB stores `SHA-256(secret)` only. Cookie value is `<refreshTokenId>.<secret>`.
- Cookie: `HttpOnly`, `Secure` (relaxed only when `Authentication:Session:AllowInsecureCookieInDevelopment` and environment is Development over `http://localhost`), `SameSite=Strict`, `Path` configurable (`Authentication:Session:CookiePath`, default `/auth`; the Vite proxy/production reverse proxy uses `/api/auth`), no `Domain` attribute, name `fynovio_rt` (`__Host-` prefix not used because the path is scoped; documented trade-off).
- Lifetimes: idle 7 days (sliding via rotation), absolute 30 days (`Authentication:Session:*`). Both configurable.
- Rotation on every refresh. A rotated token presented again ⇒ **reuse**: revoke the whole session (`revoked_reason = reuse_detected`), write an auth event, return 401. A configurable grace (default 5 s) returns `409 refresh_conflict` without revoking, to survive benign double-submits; the browser additionally single-flights refresh across tabs with the Web Locks API.

## 3. Data model & migration impact (schema `identity`, module `Access`)

New migration `AddAuthenticationFoundation` (timestamped, reversible `Down`, `dotnet ef migrations add`; RLS/grant statements hand-written per the existing exception).

| Table | Purpose | Key columns | RLS |
|---|---|---|---|
| `identity.account_credentials` | password credential, 1:1 with account | `account_id` PK/FK, `login_email_normalized` **UNIQUE**, `password_hash`, `password_changed_at`, `failed_attempts`, `locked_until`, `last_login_at`, `row_version` | none (platform-global like `accounts`; documented) |
| `identity.auth_sessions` | one row per sign-in (refresh-token family) | `id` uuid PK, `account_id`, `active_tenant_id` null, `created_at`, `last_used_at`, `absolute_expires_at`, `revoked_at`, `revoked_reason`, `user_agent_hash` | none (account-scoped, like `external_identities`) |
| `identity.auth_refresh_tokens` | rotating tokens within a session | `id` uuid PK, `session_id` FK, `token_hash` UNIQUE, `issued_at`, `expires_at`, `rotated_at`, `replaced_by_id` | none |
| `identity.account_tokens` | invite + password-reset tokens | `id` uuid PK, `purpose` (`invite`\|`password_reset`\|`password_setup`), `token_hash` UNIQUE, `account_id`, `tenant_id` null, `membership_id` null, `expires_at`, `consumed_at`, `revoked_at`, `created_at`, `created_by_account_id` null | none — must be readable before a tenant is known; no list endpoint; documented exception |
| `identity.auth_events` | append-only **security event log** (pre-tenant events cannot use tenant-scoped `evidence_records`) | `id`, `occurred_at`, `event_type`, `account_id` null, `tenant_id` null, `session_id` null, `correlation_id`, `ip_hash`, `outcome`, `detail` jsonb (allow-listed keys only) | none; runtime role gets `INSERT`/`SELECT` only (no `UPDATE`/`DELETE`) |

- Tenant-scoped state changes made **inside** an authenticated tenant context (invite created, membership activated, password change while tenant-scoped) additionally write the existing `access.evidence_records` + outbox, matching Phase 1.5 handlers.
- `scripts/create-runtime-role.sql` gains the new grants; `docs/schema/identity-access-schema.md` gets **Revision 8** describing the tables, the RLS exceptions and the session-not-tenant-state boundary (closes the "Auth session/cookie table shape" gap noted there).
- **RLS finding (verified):** `identity.tenant_memberships` is `FORCE ROW LEVEL SECURITY` with a single `tenant_isolation` policy on `app.tenant_id`, so *before* a tenant is chosen the runtime role can see **zero** membership rows — login could not list a user's memberships. Resolution (least-privilege, additive): a second, **SELECT-only** permissive policy `membership_self_view` on `tenant_memberships`: `USING (account_id = NULLIF(current_setting('app.account_id', true), '')::bigint)`. `app.account_id` is set transaction-locally (`SetAccountContextAsync`, same shape as `SetTenantContextAsync`) only by server code after the account has been authenticated (credential verified or refresh token validated). It exposes only the caller's own membership rows, grants no write path, and leaves `tenant_isolation` untouched. Covered by RLS tests (own rows visible across tenants; other accounts' rows invisible; no INSERT/UPDATE/DELETE through it).
- `account_tokens` (invite/reset) is created by S3's migration `AddAccountTokens`; S1 migration `AddAuthenticationFoundation` creates `account_credentials`, `auth_sessions`, `auth_refresh_tokens`, `auth_events` and the `membership_self_view` policy.
- Existing accounts get no credential until they sign in through an invite/bootstrap/seed; no data migration. Impact on existing tables: **one additive RLS policy on `tenant_memberships`**; no column changes.

## 4. Backend endpoints (Host)

Paths shown without the `/api` prefix (added by the reverse proxy / Vite proxy). Names follow existing minimal-API group style (`MapGroup`, ProblemDetails).

| Method & path | Auth | CSRF | Rate-limit policy | Notes |
|---|---|---|---|---|
| `GET /auth/config` | none | – | `auth-public` | `{ selfRegistrationEnabled, passwordPolicy:{minLength,maxLength,…} }` — single source for UI hints; no drift |
| `POST /auth/login` | none | header+Origin | `auth-login` | Body `{email,password}`. 200 `{status:'authenticated',accessToken,expiresIn,account,activeTenant,memberships}` or `{status:'tenant_selection_required',account,memberships}` or `{status:'no_membership',account}`. Sets refresh cookie. **401 `invalid_credentials`** for unknown user / bad password / locked / disabled (identical body & timing). |
| `POST /auth/refresh` | refresh cookie | header+Origin | `auth-refresh` | Same response shape as login. Rotates. 401 `session_invalid` (clears cookie). |
| `POST /auth/logout` | refresh cookie | header+Origin | `auth-refresh` | Revokes session, clears cookie, 204. Idempotent (204 even if already gone). |
| `GET /auth/me` | bearer (`tid`) | – | – | `{account, activeTenant, memberships, capabilities:{canInviteMembers}}`; capabilities come from the PDP (`IAuthorizer`), UX-only. |
| `POST /auth/tenants/select` | refresh cookie | header+Origin | `auth-refresh` | Body `{tenantId}`. Validates **Active** membership server-side, updates session `active_tenant_id`, returns `{accessToken,expiresIn,activeTenant}`. 403 `tenant_not_permitted` (no leak of tenant existence: same response for unknown and non-member). Used for first selection and switching. Audit event. |
| `POST /auth/invitations/validate` | none (token) | – | `auth-token` | Body `{token}` → `{valid, email(masked), tenantId, accountHasCredential, expiresAt}`; invalid/expired/consumed → uniform 400 `invalid_or_expired_token`. |
| `POST /auth/invitations/accept` | none (token) | header+Origin | `auth-token` | Body `{token,password,displayName?}`. No credential yet → sets password (policy-checked); has credential → password must match existing. Activates membership (tenant context set from the token row), consumes token, creates session + cookie, returns login-shaped response. |
| `POST /auth/password/forgot` | none | header+Origin | `auth-forgot` | **Always** 202 neutral body. Eligible account ⇒ token (TTL 30 min, single-use) + email via `IEmailSender`. Constant-time-ish work for unknown accounts. |
| `POST /auth/password/reset` | none (token) | header+Origin | `auth-token` | Body `{token,newPassword}` → 204; consumes token; **revokes all sessions** of the account; resets lockout. Client then goes to login. |
| `POST /auth/password/change` | bearer | header+Origin | `auth-password` | Body `{currentPassword,newPassword}` → 204; **revokes all *other* sessions**, keeps the current one; new hash; audit. |
| `POST /auth/register` | none | header+Origin | `auth-token` | **Mapped only when `Authentication:SelfRegistration:Enabled=true`** (default `false` ⇒ 404). Creates Account+credential only — **never** a membership/role/tenant; neutral response; email verification deferred (§14), so enabling in Production requires `AcknowledgeUnverifiedEmail=true` or startup fails. |
| `POST /tenants/{tenantId}/invitations` | bearer + PDP | header (bearer, not cookie) | `auth-password` | Minimal invite creation for tenant admins: PDP action `identity.membership.invite` (registered in `AccessActionCatalog`, therefore automatically granted to the bootstrap tenant-administrator role); route tenant must equal `ActorContext.TenantId`. Creates/links Account, `TenantMembership.Invite()`, invite token (TTL 7 d), email. **No user-management console.** |
| `GET /dev/mailbox` | Development only | – | – | Reads the dev email sink so E2E/manual testing can fetch invite/reset links. Not mapped outside Development (test asserts 404). |

**Error taxonomy** reuses ProblemDetails with `type` codes: `invalid_credentials`, `session_invalid`, `invalid_or_expired_token`, `validation_error` (400, `errors` map), `password_policy_violation` (400), `tenant_not_permitted` (403), `refresh_conflict` (409), `rate_limited` (429 + `Retry-After`). 401 = no valid session/credentials; 403 = authenticated but not permitted (never redirect 403 to login).

**Password handling.** `Microsoft.AspNetCore.Identity.PasswordHasher<T>` (in the ASP.NET shared framework / `Microsoft.Extensions.Identity.Core` — implementer verifies availability offline) for hash/verify/rehash; unknown-account logins verify against a dummy hash. Policy (server-authoritative, configurable): min 12, max 128, must differ from email local-part and current password, no composition rules (NIST 800-63B); exposed via `/auth/config`.

**Lockout.** `Authentication:Lockout:MaxFailedAttempts=5`, `LockoutMinutes=15`; lockout is invisible to the client (same 401), visible in `auth_events`.

**Email.** `IEmailSender` (Contracts-free, Host/Access boundary) + `DevMailboxEmailSender` (in-memory + `/dev/mailbox`, Development only) + `LoggingEmailSender` that logs **recipient and template name only, never links/tokens**. Production provider **deferred** (documented). Links are built from `Authentication:PublicAppBaseUrl` (config, validated at startup), never from `Host`/`X-Forwarded-*` request headers. Links put the token in the **URL fragment** (`/accept-invite#token=…`) so it never reaches server logs, proxies or `Referer`.

## 5. Flows
1. **Login:** credentials → session + cookie → response state `authenticated` (exactly one active membership ⇒ auto-select), `tenant_selection_required` (N>1), `no_membership` (0 ⇒ onboarding/no-access state). Failed login: neutral error, lockout counter, event.
2. **App start / reload:** SPA `auth state = unknown` → single-flight `POST /auth/refresh` (Web Lock) → `authenticated` | `tenant_selection_required` | `no_membership` | `unauthenticated`. Protected content is never rendered while `unknown` (route-level gate shows the shell skeleton).
3. **Expiry mid-use:** 401 on a protected call → one shared refresh → retry once; refresh failure ⇒ `expired` → redirect to `/login?returnUrl=…` (validated). Loop guards: auth endpoints excluded from the interceptor; max one retry per request.
4. **Logout:** `POST /auth/logout`, clear memory + query cache, navigate to `/login` (replace).
5. **Tenant select/switch:** `POST /auth/tenants/select` → new access token; query cache cleared (tenant boundary); topbar switcher for N>1.
6. **Invite:** admin creates invite → email → `/accept-invite#token` → validate → set/confirm password → membership Active → session → shell.
7. **Forgot/reset:** neutral response → email → `/reset-password#token` → new password → all sessions revoked → login.
8. **Change password:** `/account/security` → `password/change` → other sessions revoked.
9. **Registration disabled:** `/register` reads `/auth/config`; shows the invite-only state, not a dead form.
10. **403:** dedicated `/no-access` experience; 403 never triggers logout; 404 tenant-safe messages preserved.

## 6. Security controls & threat model

| Threat | Control |
|---|---|
| XSS token theft | Access token in memory only; refresh cookie HttpOnly; legacy `fynovio-auth` localStorage key purged on boot; existing DOMPurify rule retained. |
| **CSRF on cookie endpoints** (`refresh`, `logout`, `tenants/select`, `login`, password/invite/register) | Layered: (1) `SameSite=Strict` cookie; (2) required custom header `X-Requested-With: fynovio` (forces a CORS preflight for cross-origin callers); (3) server `Origin` (fallback `Referer`) must be in `Authentication:AllowedOrigins`, else 403; (4) `Sec-Fetch-Site` `cross-site` rejected when present; (5) responses never expose data to cross-origin readers (non-wildcard CORS). Residual: logout-CSRF is neutralised by (1)–(3); refresh CSRF yields a token only the same-origin page can read. Bearer-authenticated endpoints are not cookie-authenticated and need no CSRF token. |
| CORS | Named policy from `Authentication:AllowedOrigins` (**explicit list, empty by default**, no wildcard, credentials only for listed origins, explicit methods/headers). Dev/prod default is same-origin via proxy, so CORS is normally unused. |
| Session fixation / replay | New session id per login; refresh rotation; reuse ⇒ family revoked; access tokens bound to `sid` and checked against revocation. |
| Credential stuffing / brute force | ASP.NET `RateLimiter` policies (partitioned by IP and by normalised identifier, configurable in `Authentication:RateLimiting`), lockout, uniform errors and timing, dummy-hash verification. |
| Account enumeration | Uniform `invalid_credentials`; forgot-password always 202; invitation/reset invalid states share one error; registration neutral. |
| Token theft from DB | Only hashes stored; tokens single-use; short TTL; consumed/revoked state. |
| Open redirect | `returnUrl` allow-list validator: same-origin relative path starting with a single `/`, no `//`, no `\`, no scheme/host, re-validated after decode; anything else ⇒ dashboard. |
| Host-header poisoning | Links built from configured `PublicAppBaseUrl`; `ForwardedHeaders` only with configured `KnownProxies`. |
| Secret leakage | No passwords/tokens/links in logs, evidence, events, API responses beyond the intended delivery; console logging disabled for auth bodies; unit test scans log output. |
| Tenant escalation | `tid` only minted server-side after Active-membership check; `ActorContextMiddleware` re-validates membership on every request; client-supplied tenant ids are inputs to `select`, never trusted. |
| Registration abuse | Disabled by default; when enabled creates no membership; production guard. |
| Transport | HTTPS + `Secure` cookies + HSTS in non-Development; `Cache-Control: no-store` on auth responses; `Referrer-Policy: no-referrer` on token pages. |

## 7. Audit / evidence
`identity.auth_events` types: `login_succeeded`, `login_failed`, `account_locked`, `session_created`, `session_refreshed`, `refresh_reuse_detected`, `session_revoked`, `logout`, `tenant_selected`, `password_change`, `password_reset_requested`, `password_reset_completed`, `invite_created`, `invite_accepted`, `bootstrap_completed`, `registration_created`. Details are allow-listed metadata (ids, outcome, reason code); **never** secrets. Tenant-scoped mutations also write `access.evidence_records` + outbox per Phase 1.5 pattern. Logs (ILogger) carry correlation id (`X-Correlation-Id` reused) and event names only.

## 8. Observability
Structured `ILogger` events for auth outcomes with correlation id, no PII/secrets; counters via `System.Diagnostics.Metrics` (`auth.login.failed`, `auth.refresh.reuse`, `auth.ratelimit.rejected`) so a future OpenTelemetry exporter can pick them up (no OTel package added in this phase — none exists in the repo).

## 9. Frontend design (`web/`)

**Modules**
- `src/lib/auth/` (shared, feature-boundary safe): `session.ts` (non-persisted zustand store; states `unknown|unauthenticated|tenant_unresolved|authenticated|expired`; in-memory access token), `sessionClient.ts` (login/refresh/logout/select calls, single-flight + `navigator.locks`), `returnUrl.ts` (`sanitizeReturnUrl`), `SessionGate.tsx`. `features/auth/index.ts` re-exports for backward compat during migration; consumers switch to `@/lib/auth`.
- `src/api/client.ts`: `withCredentials`, `X-Requested-With`, `X-Correlation-Id`, bearer from the session module, response interceptor: 401 → shared refresh + single retry, then `expired`; 403 preserved as `ApiError` (`status:403`) for route/UI handling; `429` surfaces `Retry-After`; normalise ProblemDetails (`type`→`code`, `errors`→`fields`).
- Routes (each in the owning feature's `routes.ts`, assembled by `routes/index.tsx`): `/login`, `/select-tenant`, `/accept-invite`, `/forgot-password`, `/reset-password`, `/register` (invite-only state), `/no-access`, `/account/security`. Guards: `PublicOnlyRoute` (authenticated → dashboard/returnUrl), `ProtectedRoute` (unknown → skeleton; unauthenticated → login with safe returnUrl; tenant unresolved → `/select-tenant`).
- Components: `LoginForm` (remove pre-filled credentials), `TenantSelector`, `TenantSwitcher` (Topbar/UserMenu), `AcceptInviteForm`, `ForgotPasswordForm`, `ResetPasswordForm`, `ChangePasswordForm`, `PasswordPolicyHint`, `RegistrationDisabledNotice`, `NoAccessPage`. Reuse `Field`, `PasswordInput`, shadcn primitives; forms: react-hook-form + zod; no second component library; duplicate submit prevented via `isPending`; non-sensitive values preserved on recoverable errors; correct `autocomplete` attributes; accessible labels/live regions; i18n `auth` namespace (tr+en); no sensitive console logging.
- **Dev/mocking:** Vite dev proxy `/api → http://localhost:5208` (strip `/api`). MSW keeps mocking the *other* features but the auth handlers are removed and `onUnhandledRequest: 'bypass'` in the browser worker, so login goes to the real backend. Vitest supplies its own auth handlers per test (`src/test/authHandlers.ts`) and keeps `error` on unhandled requests. `.env.example` documents `VITE_API_URL`/proxy.
- Query cache is cleared on logout, session expiry and tenant switch. `useAppStore` (theme etc.) is untouched. `data-table/viewState.ts` and `usePermission` read the user from the new session module.

## 10. Bootstrap & dev seed
- **Development seed** (`DevSeed:Enabled`, only honoured when `IHostEnvironment.IsDevelopment()`): idempotent; tenants `1` and `2`; `admin@fynovio.local` (tenant-administrator in both → exercises tenant selection) and `single@fynovio.local` (one tenant, no admin rights → exercises 403); password from `DevSeed:Password` in `appsettings.Development.json` (dev-only value, clearly marked). Uses `BootstrapTenantAccessHandler`.
- **Production bootstrap:** `dotnet Host.dll bootstrap-tenant-admin --tenant-id <n> --email <e> --display-name <name>` — refuses unless `Bootstrap__Enabled=true`; refuses if the tenant already has `TenantAccessState`; creates Account + platform ExternalIdentity + Active membership, runs `BootstrapTenantAccessHandler`, issues a one-time `password_setup` token (TTL 24 h, hash stored) printed **once** to stdout for the operator, writes `bootstrap_completed`. No default credentials exist anywhere in production paths.
- `scripts/dev-up.sh` (or README section) documents: local Postgres → migrations → runtime role → `dotnet run` Host → `npm run dev` in `web/`. Existing README instructions stay the source for Postgres/role setup.

## 11. Configuration keys (all documented in `appsettings*.json`/README/`.env.example`)
`Authentication:Jwt:{Issuer,Audience,SigningKey,AccessTokenMinutes}` · `Authentication:Session:{RefreshIdleDays=7,RefreshAbsoluteDays=30,CookieName,CookiePath,AllowInsecureCookieInDevelopment,RefreshGraceSeconds=5,RequireSessionClaim=true}` · `Authentication:AllowedOrigins[]` · `Authentication:PublicAppBaseUrl` · `Authentication:Password:{MinLength=12,MaxLength=128}` · `Authentication:Lockout:{MaxFailedAttempts=5,LockoutMinutes=15}` · `Authentication:RateLimiting:*` · `Authentication:SelfRegistration:{Enabled=false,AcknowledgeUnverifiedEmail=false}` · `Bootstrap:Enabled=false` · `DevSeed:{Enabled,Password}`. Nothing secret is committed for non-Development environments.

## 12. Test matrix

**Backend (xUnit; Testcontainers Postgres where DB semantics matter)**
- *Unit/domain:* password policy; token generation/hashing/expiry/single-use state machine; session/refresh rotation state machine incl. reuse; lockout counter; returnUrl-independent; email normalisation.
- *Integration (Access.Tests):* credential uniqueness; rotation+reuse family revocation; invite accept (new credential and existing credential); reset revokes all sessions; change-password revokes others; tenant select rejects non-member/disabled; RLS still holds for `tenant_memberships` when accessed via auth handlers; runtime role cannot UPDATE/DELETE `auth_events`; bootstrap refuses second run.
- *HTTP (Host.Tests, `WebApplicationFactory`):* login success/failure (identical body+status for unknown/bad password/locked/disabled); **401 vs 403** distinction (no token → 401; valid token, non-member/unauthorized → 403; revoked `sid` → 401); refresh/rotation/reuse; logout invalidates protected API immediately; tenant select minting `tid` accepted by Phase 2 endpoints (end-to-end with `/opportunities`); forgot-password neutral 202 for known/unknown; invalid/expired/consumed/replayed invite & reset tokens; registration 404 when disabled and no membership when enabled; CSRF: missing custom header ⇒ 403, foreign Origin ⇒ 403; CORS: no wildcard, unlisted origin gets no ACAO; rate-limit 429 + `Retry-After`; `/dev/mailbox` 404 outside Development; secrets/tokens absent from captured logs and evidence; existing 213 tests unchanged in intent (fixture env sets `RequireSessionClaim=false`).

**Frontend (vitest + Testing Library + MSW)**
Session store state machine; bootstrap refresh single-flight; interceptor 401→refresh→retry-once and failure→expired; no retry loop on auth endpoints; 403 not redirected; `sanitizeReturnUrl` negative cases (`//evil.com`, `/\evil`, `javascript:`, `https://…`, encoded variants); route guards (unknown/unauthenticated/tenant-unresolved/authenticated); no auth token in `localStorage/sessionStorage` (spy); legacy key purge; Login/Invite/Forgot/Reset/Change forms (loading, submitting, duplicate-submit prevention, server errors, preserved non-sensitive values, a11y labels); `TenantSelector`; registration-disabled state.

**Playwright E2E (`web/e2e`, real backend + Postgres + Vite; dev mailbox for links):** login, wrong credentials, logout (then protected API and page inaccessible), reload session restore, expiry/forced revoke → clean redirect with safe returnUrl, forgot→reset→login, invite acceptance (new + existing account), change password (other session dies), protected-route redirect + return, multi-tenant selection & switch, 403 no-access page, registration-disabled screen, **tenant manipulation attempt** (editing client state / request `tid` cannot reach another tenant), open-redirect attempt fails.

**Security negatives (§29 of the spec)** are each covered by at least one test above (open redirect, token replay, cross-tenant manipulation, unauthenticated protected call, authenticated-unauthorised without leaking, tokens absent from logs/responses).

## 13. Files to add / change (expected; implementers finalise within these areas)

**Backend**
- Add: `src/Modules/Access/Domain/Authentication/*` (`AccountCredential`, `AuthSession`, `RefreshToken`, `AccountToken`, `AuthEvent`), `src/Modules/Access/Persistence/Configurations/*` for them, migration `AddAuthenticationFoundation` (+RLS/grant SQL notes), `src/Modules/Access/Application/Authentication/*` (login, refresh, logout, select-tenant, invite create/validate/accept, forgot/reset/change, register, bootstrap-admin handlers; `IEmailSender`, `IPasswordPolicy`), `src/Host/Authentication/*` (`AccessTokenIssuer`, `RefreshCookieWriter`, `CsrfOriginGuard`, options classes, dev seed, `DevMailboxEmailSender`), `src/Host/Endpoints/AuthEndpoints.cs`, `InvitationEndpoints.cs`, `DevEndpoints.cs`, bootstrap CLI entry, tests under `tests/Access.Tests/**` and `tests/Host.Tests/**`.
- Change: `src/Host/Program.cs` (options, CORS policy, rate limiter, forwarded headers, HSTS/HTTPS in non-dev, endpoint mapping, seed/bootstrap), `ActorContextMiddleware.cs` (`sid` revocation check), `AccessDbContext.cs`, `AccessActionCatalog.cs` (+`identity.membership.invite`), `appsettings*.json`, `scripts/create-runtime-role.sql`, `docs/schema/identity-access-schema.md` (Revision 8), `README.md` (local run + config), `tests/Host.Tests` fixtures (`RequireSessionClaim=false`).

**Frontend (`web/`)**
- Add: `src/lib/auth/*`, `src/features/auth/{pages,components,api,schema}` additions for the 7 new flows, `src/routes/{NoAccessPage}.tsx`, `src/locales/{tr,en}/auth.ts` keys, `src/test/authHandlers.ts`, unit/component tests, `playwright.config.ts`, `e2e/*`, `package.json` scripts (`test:e2e`), Vite proxy.
- Change: `src/api/client.ts`, `src/api/endpoints.ts`, `src/routes/{index.tsx,ProtectedRoute.tsx,PublicOnlyRoute.tsx,paths.ts}`, `src/features/auth/**` (store removed/migrated), `src/layouts/components/{UserMenu,Topbar}.tsx`, `src/features/home/components/GreetingSection.tsx`, `src/components/data-table/lib/viewState.ts`, `src/lib/permissions/usePermission.ts`, `src/mocks/*` (remove auth handler, bypass unhandled), `src/main.tsx`/`App.tsx` (SessionGate), `.env.example`, `web/AGENTS.md` (auth section), CI notes.

## 14. Open decisions & deferrals — **none blocking**

| # | Item | Resolution |
|---|---|---|
| D1 | Cookie vs bearer session | **Decided** (owner, §0.1). |
| D2 | Where auth code lives | Access assembly, `Authentication` sub-namespace (rules above). Extraction into a dedicated `Identity` module **deferred**. |
| D3 | Login handle vs "no unique email on Account" | Resolved without conflict: unique handle on `account_credentials`. |
| D4 | Password reset/change session semantics | Reset ⇒ revoke all sessions; change ⇒ revoke all others, keep current. Recorded here as the explicit decision (spec §11 asked for it). |
| D5 | Production email provider | **Deferred** (no vendor decision in repo). Abstraction + dev sink shipped; production sender is a documented follow-up. Reset/invite work end-to-end in Development. |
| D6 | Tenant display names | `TenantLifecycle` is a stub, no tenant entity ⇒ memberships expose `tenantId` only; UI shows "Tenant {id}" until the tenant module exists. **Deferred**. |
| D7 | Email verification for self-registration | **Deferred**; enabling registration in Production requires an explicit acknowledgement flag. |
| D8 | MFA, SSO/OIDC, SCIM, IAM console | Explicit non-goals of the spec. |
| D9 | Numeric defaults (TTLs, lockout, rate limits, password length) | Configurable; defaults above are recommendations, not binding values. |
| D10 | Frontend CI | The `web/.github` workflow is inert in this monorepo. Porting frontend + Playwright jobs into the root `ci.yml` (path-filtered) is a **follow-up**, noted in the final report, not part of 2.5A acceptance. |

## 15. Environment constraints & risks
- Testcontainers needs the Docker socket; this agent sandbox denies it, so backend integration/HTTP tests must be run with an explicit sandbox exception (per command). Use `dotnet … -m:1 -nodeReuse:false` (known MSBuild hang otherwise).
- NuGet/npm installs and the Playwright browser download need network access beyond the sandbox default.
- `.claude/agents/**` is write-protected by the sandbox — no agent files are added; the verification pass is run with the repo's canonical commands.
- Risk: `ActorContextMiddleware` change touches every Phase 2 request path → regression suite (213 tests) is the gate after every backend slice.
- Risk: refresh-cookie path differs between direct backend (`/auth`) and proxied (`/api/auth`) → single config key, covered by an E2E test.

## 16. Implementation order (slices) & definition of done

Checkboxes are ticked only after the corresponding commit exists (repo standard; see CLAUDE.md "Plan checkbox tracking").

- [ ] **S1 — Backend auth core.** Migration + entities + password hashing + JWT issuer + login/refresh/logout/`/me`/tenant select + cookie + CSRF/Origin guard + CORS + rate limiting + lockout + auth events + `sid` revocation check + dev seed + production bootstrap command + backend tests (unit, integration, HTTP).
- [ ] **S2 — Frontend auth core.** `lib/auth` session module, API client 401/403/refresh, guards, LoginPage/TenantSelector/TenantSwitcher/UserMenu logout, legacy storage purge, Vite proxy, MSW change, `NoAccessPage`, tests.
- [ ] **S3 — Backend invitation & password lifecycle.** `account_tokens`, `IEmailSender` + dev mailbox, invitation create/validate/accept, forgot/reset/change, registration flag/endpoint, extra rate limits, tests.
- [ ] **S4 — Frontend invitation & password lifecycle.** Accept-invite, forgot, reset, change-password, registration-disabled screens, tests.
- [ ] **S5 — Hardening, E2E, docs, report.** Security review (each §6 row verified), Playwright suite, schema Revision 8, README/AGENTS updates, graphify refresh, full regression (backend 213+new, web check+build), `PHASE_2_5A_AUTH_FRONTEND_FINAL_REPORT.md`.

**Definition of done** = spec §36: real login from React against the real .NET backend; reload survives; logout invalidates the real session; invite acceptance works; registration is config-gated and grants nothing; forgot/reset work without enumeration; change password works; expiry produces a clean redirect; 1-tenant and N-tenant states handled by server-authoritative membership; routes gated in UI while the backend stays authoritative; 401≠403 throughout; no unsafe token storage/logging; Phase 1.5 remains the only authorization foundation; automated tests + Playwright green; no CRM behaviour duplicated into the auth frontend.
