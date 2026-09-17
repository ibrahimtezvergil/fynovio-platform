using Access.Application;
using Access.Domain.Identity;
using Access.Tests.Integration;
using Contracts;
using Xunit;

namespace Access.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class PrincipalResolverTests
{
    private readonly PostgresFixture _fixture;

    public PrincipalResolverTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task IsActiveTenantMemberAsync_true_for_an_active_member_of_that_tenant()
    {
        var tenant = TestData.NextTenant();
        var principal = new PrincipalRef("https://idp.local", "user-1");

        await using var seed = _fixture.CreateAdminContext();
        var account = Account.Create("user1@example.com", "User One");
        seed.Accounts.Add(account);
        await seed.SaveChangesAsync();
        seed.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));
        var membership = TenantMembership.Invite(tenant, account.Id);
        membership.Activate();
        seed.TenantMemberships.Add(membership);
        await seed.SaveChangesAsync();

        await using var context = _fixture.CreateAdminContext();
        var isMember = await new PrincipalResolver(context)
            .IsActiveTenantMemberAsync(principal, tenant);

        Assert.True(isMember);
    }

    [Fact]
    public async Task IsActiveTenantMemberAsync_false_for_a_principal_with_no_membership_in_that_tenant()
    {
        var tenant = TestData.NextTenant();
        var otherTenant = TestData.NextTenant();
        var principal = new PrincipalRef("https://idp.local", "user-2");

        await using var seed = _fixture.CreateAdminContext();
        var account = Account.Create("user2@example.com", "User Two");
        seed.Accounts.Add(account);
        await seed.SaveChangesAsync();
        seed.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));
        var membership = TenantMembership.Invite(otherTenant, account.Id);
        membership.Activate();
        seed.TenantMemberships.Add(membership);
        await seed.SaveChangesAsync();

        await using var context = _fixture.CreateAdminContext();
        var isMember = await new PrincipalResolver(context)
            .IsActiveTenantMemberAsync(principal, tenant);

        Assert.False(isMember);
    }
}
