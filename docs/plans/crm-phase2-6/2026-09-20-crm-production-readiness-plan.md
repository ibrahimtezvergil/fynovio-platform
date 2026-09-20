# Phase 2.6 — CRM Production Readiness & Reference Queries: implementation plan

Owner scope (2026-09-20): OD2 → OD1 → Party search (G2) → Product search (G3) only if the flow truly needs it → tests → final report. Follows the Phase 2.5B report's two open owner decisions.

## Verified starting point
- `main` = `f0fbf82`, clean, pushed. Branch `feat/phase-2-6-crm-production-readiness`.
- No production path gives any user a `crm.*` grant: `BootstrapTenantAccessHandler` copies only `AccessActionCatalog`; `CrmDevSeed` is Development-only.
- `ReassignOpportunityHandler` accepts any `PrincipalRef` (no eligibility check). No member/assignee query exists.
- `IPartyDirectory` has get-by-ref only; no search. No party is seeded and no HTTP path creates one.
- **No Product master exists.** `AddOpportunityLine` builds `EntityRef(tenant, "masterdata", "product", id)` against a module that owns no Product.

## Binding boundaries (nothing here needs a new owner decision)
1. **Frozen invariant kept (owner-decisions-final §3):** *no automatic template reconciler*. Module enablement is an **explicit, operator-invoked, idempotent copy** of the *current* template version into tenant-local `Role`/`PermissionSet` rows (`origin=system_template` + provenance). A tenant already enabled at version N is reported `AlreadyEnabled` and is **never modified** by a later template change. An "upgrade to version N+1" operation is *not* built — it would be a new decision.
2. **Module boundaries:** manifest types live in `Contracts`; CRM exposes its own manifest; **Host** composes the union and passes it to Access (same pattern as `AccessActionCatalogSeeder`). Access never references CRM. No parallel permission system: enablement writes ordinary `Role`/`PermissionSet`/`RolePermissionSet`/`RoleAssignment` rows that the existing PDP already reads.
3. **No `crm.*` wildcard:** template permission sets list explicit action keys (from `CrmActionKeys` constants); enablement fails closed if a referenced key is not an active registry entry.
4. Every provisioning write bumps `TenantAccessRevision`, writes evidence + outbox in the same transaction, runs under the tenant GUC as the unprivileged runtime role.

## Design (one line each)
- **OD2** `ModuleCapabilityManifest(moduleKey, version, permissionSets[], roles[])` → `EnableTenantModuleHandler` (Access) → `access.tenant_module_enablements` (PK tenant+module, RLS) + `origin_module_key`/`origin_version` on roles/permission sets + `RoleAssignment` source `module_enablement`. Roles marked `GrantToTenantAdministrators` are assigned to the tenant's *current* administrators only. Operator entry points: `enable-tenant-module` command and `--modules` on `bootstrap-tenant-admin`. `CrmDevSeed` roles are **deleted** — the dev seed calls the same handler; only the pipeline seed stays dev-only.
- **CRM v1 template:** permission sets `crm_opportunity_read`, `crm_opportunity_write` (incl. party reference search), `crm_opportunity_reassign`; roles `crm_viewer`, `crm_sales_representative`, `crm_manager` (+ admins).
- **OD1** `IAuthorizedPrincipalDirectory` (Contracts, implemented in `Access.Application`): active tenant members whose grants would permit **all** required actions *as owner*. CRM declares the requirement — `crm.opportunity.read` + `crm.opportunity.change_stage` (an assignee must at least see the record and move it; a read-only user is not assignable). `GET /opportunities/{id}/assignable-principals` is gated by `crm.opportunity.reassign` on that record; `ReassignOpportunityHandler` **re-validates the target** server-side (`principal_not_assignable`, 422). The directory restates the PDP's grant rule set-based; `AuthorizedPrincipalDirectoryTests` proves it agrees with `AccessAuthorizer` for every member of a grant matrix (tenant-wide, owner-relation, split across roles, expired, future-dated, disabled/invited members, foreign issuer, other tenant) and was mutation-checked — a PDP change that the directory does not follow fails that test. Team/org/territory/record-policy fact providers do **not exist** in Phase 1.5 (gap-closure §4 declined to reserve them): the query consumes whatever the PDP evaluates, so richer rules will flow in when the PDP gains them; the extension point is the directory implementation, guarded by that test.
- **G2** `IPartySearch` (Contracts, implemented by MasterData's `PartyDirectory`): tenant-scoped, escaped `ILIKE`, capped, canonical (non-merged) parties only. `GET /crm/references/parties` gated by the new CRM action `crm.reference.party.search` (MasterData has no authorization layer yet — recorded as limitation L-3; the same endpoint also resolves `?ids=` for display names). UI: searchable party picker replaces the numeric field. Dev seed adds sample parties through the real `CreatePartyHandler`. **Found while doing this:** `PartyDirectory`/`PartyIdentityResolver` never set `app.tenant_id`, so under the runtime role (RLS) they returned nothing — hidden because every existing test used the admin connection; fixed and covered by runtime-role tests.
- **G3** **Not done — by design.** No Product master exists; inventing a CRM-owned product catalogue would violate the boundary you set. Numeric `productId` input stays, with an explicit "no product catalogue yet" notice; the dangling `masterdata/product` reference is documented.

## Discovered, out of scope (reported, not built)
- P1 No production path creates a **pipeline** (CRM data): a real tenant cannot Open/Stage until one exists. Needs a business decision on the stage template.
- P2 No HTTP/production path creates **Parties**; `CreateOpportunity` does not verify the party exists (would change the Phase 2 handler contract).
- P3 `AccessActionCatalogSeeder` deprecates keys missing from its manifest — the Host union is now built from one place and covered by a test.

## Steps (checkbox ticked only after its commit; see CLAUDE.md)
- [x] T1 Contracts manifest types + Access provenance columns + `tenant_module_enablements` (+RLS migration) + `EnableTenantModuleHandler` + bootstrap provenance; Access.Tests
  → Commit: `e01c3b7` "feat(access): versioned module capability templates and explicit tenant module enablement"
- [x] T2 CRM manifest/`CrmActionKeys`, Host module composition (single union for the action seeder), operator command, DevSeeder switched to the general path (`CrmDevSeed` roles removed); tests
  → Commit: `f734c73` "feat(crm): production CRM access via module enablement; dev seed uses the same path"
- [x] T3 OD1 directory + CRM assignable-principals query/endpoint + Reassign target validation; tests
  → Commit: `d60abfc` "feat(crm): authorization-aware Assignable Principals query; Reassign re-validates its target"
- [x] T4 G2 party search (Contracts, MasterData, CRM handler, endpoint, dev parties); tests
  → Commit: `519b9be` "feat(masterdata,crm): tenant-safe party search and reference lookup"
- [x] T5 Frontend: party picker, assignee picker, Reassign dialog, G3 notice; vitest
  → Commit: `39a5531` "feat(web): customer picker and server-driven Reassign for Opportunities"
  Deviation: built on the existing `AsyncCombobox` (`common/inputs`) instead of a new combobox primitive; the planned `useAssignablePrincipals`/`usePartySearch` hooks became `useAssigneeSearcher`/`usePartySearcher` (searchers over the tenant-rooted query cache) because `AsyncCombobox` owns its debounce/abort.
- [x] T6 Playwright E2E updates, docs (README, schema doc, AGENTS status), final report, graphify refresh
  → Commits: `ecf6886` "test(e2e): Reassign, customer picker and reference-query scenarios against the real API"; docs/report commit and `chore(graphify)` refresh follow (see final report §8).
