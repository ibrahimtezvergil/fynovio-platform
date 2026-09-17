using Access.Domain.Authorization;
using Access.Domain.Identity;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Tests.Integration;

/// <summary>Real-Postgres proofs of constraints that the domain layer relies on but
/// does not itself enforce: the composite tenant-safe FK on RoleAssignment.RoleId
/// (C4), tenant-scoped uniqueness (H1), and RowVersion-based optimistic concurrency
/// (H2). Uses the admin (superuser) connection deliberately — these are DB
/// constraints, not RLS policies, and superusers/table owners do not bypass FKs,
/// unique indexes, or concurrency tokens the way they bypass RLS.</summary>
public sealed class AccessConstraintTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public AccessConstraintTests(PostgresFixture fixture) => _fixture = fixture;

    /// <summary>C4: RoleAssignment.Grant takes a bare `roleId` — nothing in the domain
    /// layer stops a caller from passing a role that belongs to a different tenant
    /// than the assignment's own TenantId. The composite FK
    /// (RoleAssignmentConfiguration.cs: HasForeignKey(tenant_id, role_id) ->
    /// Role(tenant_id, id)) is the actual backstop, and it must fail here.</summary>
    [Fact]
    public async Task RoleAssignment_insert_with_cross_tenant_role_reference_violates_composite_fk()
    {
        var tenantA = new TenantId(800);
        var tenantB = new TenantId(801);

        long roleBId;
        long accountId;
        await using (var seed = _fixture.CreateAdminContext())
        {
            var roleB = Role.Create(tenantB, "role_b_only", "Role B Only");
            seed.Roles.Add(roleB);
            var account = Account.Create("cross-tenant-fk@test.local", "cross-tenant-fk-subject");
            seed.Accounts.Add(account);
            await seed.SaveChangesAsync();
            roleBId = roleB.Id;
            accountId = account.Id;
        }

        await using var context = _fixture.CreateAdminContext();
        var assignment = RoleAssignment.Grant(tenantA, accountId, roleBId, accountId, RoleAssignment.SourceManual);
        context.RoleAssignments.Add(assignment);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    /// <summary>H1: same Role Key must fail within one tenant (unique index on
    /// (tenant_id, key)) but succeed across different tenants — the frozen tenant
    /// isolation model, not a global namespace.</summary>
    [Fact]
    public async Task Role_key_is_unique_per_tenant_but_reusable_across_tenants()
    {
        var tenantA = new TenantId(810);
        var tenantB = new TenantId(811);

        await using (var context = _fixture.CreateAdminContext())
        {
            context.Roles.Add(Role.Create(tenantA, "sales_rep", "Sales Rep"));
            await context.SaveChangesAsync();
        }

        await using (var duplicate = _fixture.CreateAdminContext())
        {
            duplicate.Roles.Add(Role.Create(tenantA, "sales_rep", "Sales Rep Duplicate"));
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
        }

        await using (var otherTenant = _fixture.CreateAdminContext())
        {
            otherTenant.Roles.Add(Role.Create(tenantB, "sales_rep", "Sales Rep In Tenant B"));
            await otherTenant.SaveChangesAsync();
        }
    }

    /// <summary>H2: two contexts load the same RoleAssignment, both revoke it
    /// in-memory, the first save wins and (via RowVersionInterceptor) bumps
    /// row_version; the second save carries the now-stale original row_version and
    /// must fail with a concurrency conflict rather than silently overwriting.</summary>
    [Fact]
    public async Task Concurrent_revoke_of_the_same_assignment_raises_optimistic_concurrency_conflict()
    {
        var tenantId = new TenantId(820);
        long assignmentId;

        await using (var seed = _fixture.CreateAdminContext())
        {
            var role = Role.Create(tenantId, "concurrency_role", "Concurrency Role");
            seed.Roles.Add(role);
            var account = Account.Create("concurrency@test.local", "concurrency-subject");
            seed.Accounts.Add(account);
            await seed.SaveChangesAsync();

            var assignment = RoleAssignment.Grant(tenantId, account.Id, role.Id, account.Id, RoleAssignment.SourceManual);
            seed.RoleAssignments.Add(assignment);
            await seed.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        await using var contextOne = _fixture.CreateAdminContext();
        await using var contextTwo = _fixture.CreateAdminContext();

        var assignmentOne = await contextOne.RoleAssignments.SingleAsync(a => a.Id == assignmentId);
        var assignmentTwo = await contextTwo.RoleAssignments.SingleAsync(a => a.Id == assignmentId);

        Assert.Equal(1, assignmentOne.RowVersion);
        Assert.Equal(1, assignmentTwo.RowVersion);

        assignmentOne.Revoke();
        await contextOne.SaveChangesAsync();

        assignmentTwo.Revoke();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextTwo.SaveChangesAsync());
    }
}
