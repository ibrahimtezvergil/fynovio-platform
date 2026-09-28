using Access.Application;
using Access.Domain.Authorization;
using Access.Domain.Identity;
using Contracts;
using CRM.Application;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>The gap named directly by PHASE_2_OPPORTUNITY_COMMANDS_API_PLAN.md §18
/// ("tests/CRM.Tests/Integration/OpportunityAuthorizationTests.cs (or similar) —
/// coarse/OwnedBy authorization integration coverage") — every other CRM handler test
/// uses StubAuthorizer, a fixed-answer fake; these tests run a real CRM command through
/// the real Access.Application.AccessAuthorizer, wired to real Account/RoleAssignment/
/// PermissionSet data, proving Phase 1.5's actual PDP authorizes/denies a real Opportunity
/// correctly. Also the end-to-end proof of the 2026-09-19 AuthorizationDenialStage
/// architecture delta: a tenant-wide grant allows, an owner-relation grant allows only the
/// caller's own record and denies with DenialStage.Record on someone else's (never
/// Coarse), and zero grant denies with DenialStage.Coarse — see
/// docs/architecture-analysis/2026-09-19-crm-phase2-authorization-delta-and-entry-stage-resolution.md.
/// The authorizer itself runs against the unprivileged `fynovio_app` runtime role, not the
/// migration superuser — RLS is inert on the superuser connection, so these results would be
/// meaningless proof of the real PDP without it (2026-09-20 PHASE_1_5_RUNTIME_RLS_PDP_DELTA;
/// seeding still uses the admin connection, only PDP evaluation uses the runtime role).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class OpportunityAuthorizationTests
{
    private const string WinActionKey = "crm.opportunity.win";
    private readonly PostgresFixture _fixture;

    public OpportunityAuthorizationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Tenant_wide_grant_allows_winning_any_opportunity_in_the_tenant()
    {
        var owner = FreshPrincipal();
        var caller = FreshPrincipal();
        var (tenant, opportunityId) = await SeedOpenOpportunityAsync(owner);
        await GrantAsync(tenant, caller, relation: null);

        var authorizer = await CreateAuthorizerAsync();
        var command = new WinOpportunityCommand(tenant, opportunityId, caller, ExpectedVersion: 3, "key-1", Guid.NewGuid());

        await using var crm = _fixture.CreateAdminContext();
        var result = await new WinOpportunityHandler(crm, authorizer).HandleAsync(command);

        Assert.False(result.Replayed);
    }

    [Fact]
    public async Task Owner_relation_grant_allows_winning_the_callers_own_opportunity()
    {
        var owner = FreshPrincipal();
        var (tenant, opportunityId) = await SeedOpenOpportunityAsync(owner);
        await GrantAsync(tenant, owner, relation: PermissionSetItem.OwnerRelation);

        var authorizer = await CreateAuthorizerAsync();
        var command = new WinOpportunityCommand(tenant, opportunityId, owner, ExpectedVersion: 3, "key-1", Guid.NewGuid());

        await using var crm = _fixture.CreateAdminContext();
        var result = await new WinOpportunityHandler(crm, authorizer).HandleAsync(command);

        Assert.False(result.Replayed);
    }

    /// <summary>The record-level case the 2026-09-19 architecture delta exists for: the
    /// caller genuinely holds a grant for this action, it just doesn't cover this record.
    /// DenialStage must be Record, never Coarse — a PEP (CrmProblemDetailsExceptionHandler)
    /// collapses this to the same 404 a missing opportunity gets, never 403.</summary>
    [Fact]
    public async Task Owner_relation_grant_on_someone_elses_opportunity_is_denied_at_record_level()
    {
        var owner = FreshPrincipal();
        var caller = FreshPrincipal();
        var (tenant, opportunityId) = await SeedOpenOpportunityAsync(owner);
        await GrantAsync(tenant, caller, relation: PermissionSetItem.OwnerRelation);

        var authorizer = await CreateAuthorizerAsync();
        var command = new WinOpportunityCommand(tenant, opportunityId, caller, ExpectedVersion: 3, "key-1", Guid.NewGuid());

        await using var crm = _fixture.CreateAdminContext();
        var exception = await Assert.ThrowsAsync<OpportunityAuthorizationDeniedException>(() =>
            new WinOpportunityHandler(crm, authorizer).HandleAsync(command));

        Assert.Equal(AuthorizationDenialStage.Record, exception.DenialStage);
        Assert.Equal(opportunityId, exception.OpportunityId);

        var untouched = await crm.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunityId);
        Assert.Equal(OpportunityStatus.Open, untouched.Status);
    }

    [Fact]
    public async Task Zero_grant_for_the_action_is_denied_at_coarse_level()
    {
        var owner = FreshPrincipal();
        var caller = FreshPrincipal();
        var (tenant, opportunityId) = await SeedOpenOpportunityAsync(owner);
        // No GrantAsync call at all — caller is a recognized principal (still needs an
        // Account/ExternalIdentity to be "recognized" rather than "unrecognized") with
        // zero effective grants for this action.
        await using var access = _fixture.CreateAccessContext();
        var account = Account.Create($"{caller.Subject}@test.local", caller.Subject);
        access.Accounts.Add(account);
        await access.SaveChangesAsync();
        access.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, caller));
        access.TenantMemberships.Add(TenantMembership.Invite(tenant, account.Id));
        access.TenantAccessStates.Add(TenantAccessState.Initialize(tenant));
        await access.SaveChangesAsync();
        await EnsureActionRegisteredAsync(access);

        var authorizer = await CreateAuthorizerAsync();
        var command = new WinOpportunityCommand(tenant, opportunityId, caller, ExpectedVersion: 3, "key-1", Guid.NewGuid());

        await using var crm = _fixture.CreateAdminContext();
        var exception = await Assert.ThrowsAsync<OpportunityAuthorizationDeniedException>(() =>
            new WinOpportunityHandler(crm, authorizer).HandleAsync(command));

        Assert.Equal(AuthorizationDenialStage.Coarse, exception.DenialStage);
    }

    private static PrincipalRef FreshPrincipal() => new("https://idp.local", $"auth-test-{Guid.NewGuid():N}");

    private async Task<(TenantId TenantId, long OpportunityId)> SeedOpenOpportunityAsync(PrincipalRef owner)
    {
        var tenant = TestData.NextTenant();
        await using var seedCrm = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");

        var (versionId, stageId) = await TestData.CreatePublishedPipelineWithEntryStageAsync(seedCrm, tenant);
        var opportunity = Opportunity.Create(tenant, partyRef, owner, "TRY", 100m);
        opportunity.AddLine(TestData.ProductRef(tenant), quantity: 1, unitPrice: 100m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), versionId, stageId);
        seedCrm.Opportunities.Add(opportunity);
        await seedCrm.SaveChangesAsync();

        return (tenant, opportunity.Id);
    }

    /// <summary>Grants `WinActionKey` to `principal` in `tenant`, optionally scoped by
    /// `relation` (null = tenant-wide, `PermissionSetItem.OwnerRelation` = owner-scoped).
    /// Mirrors Access.Tests' AccessTestFixture.GivenGrantAsync, adapted to CRM.Tests'
    /// shared PostgresFixture container instead of a per-class fixture.</summary>
    private async Task GrantAsync(TenantId tenant, PrincipalRef principal, string? relation)
    {
        await using var access = _fixture.CreateAccessContext();

        var account = Account.Create($"{principal.Subject}@test.local", principal.Subject);
        access.Accounts.Add(account);
        await access.SaveChangesAsync();

        access.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));
        access.TenantMemberships.Add(TenantMembership.Invite(tenant, account.Id));
        await EnsureActionRegisteredAsync(access);

        var suffix = "t" + Guid.NewGuid().ToString("N")[..7];
        var permissionSet = PermissionSet.Create(tenant, $"crm_test_permission_set_{suffix}", "CRM Test Permission Set");
        permissionSet.Grant(WinActionKey, relation);
        access.PermissionSets.Add(permissionSet);

        var role = Role.Create(tenant, $"crm_test_role_{suffix}", $"CRM Test Role {suffix}");
        access.Roles.Add(role);
        await access.SaveChangesAsync();

        access.RolePermissionSets.Add(RolePermissionSet.Create(tenant, role.Id, permissionSet.Id));
        access.RoleAssignments.Add(RoleAssignment.Grant(tenant, account.Id, role.Id, account.Id, RoleAssignment.SourceBootstrap));

        if (!await access.TenantAccessStates.AnyAsync(s => s.TenantId == tenant))
            access.TenantAccessStates.Add(TenantAccessState.Initialize(tenant));

        await access.SaveChangesAsync();
    }

    /// <summary>Idempotent: several tests in this file grant the same real action key
    /// (`crm.opportunity.win`), and `ActionRegistryEntry` is a global, non-tenant-scoped
    /// catalog row — inserting it twice would violate its unique key.</summary>
    private static async Task EnsureActionRegisteredAsync(Access.Persistence.AccessDbContext access)
    {
        if (!await access.Actions.AnyAsync(a => a.ActionKey == WinActionKey))
        {
            access.Actions.Add(ActionRegistryEntry.Create(WinActionKey, "CRM", nameof(Opportunity)));
            await access.SaveChangesAsync();
        }
    }

    private async Task<AccessAuthorizer> CreateAuthorizerAsync()
    {
        var runtimeConnectionString = await _fixture.RuntimeConnectionStringAsync();
        var accessContext = PostgresFixture.CreateAccessContext(runtimeConnectionString);
        var actionCatalog = new AccessActionCatalogService(accessContext);
        return new AccessAuthorizer(accessContext, new PrincipalResolver(accessContext), actionCatalog);
    }
}
