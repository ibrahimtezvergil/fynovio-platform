using Access.Domain.Authorization;
using Contracts;

namespace Access.Tests.Domain;

public sealed class RoleAssignmentTests
{
    [Fact]
    public void Revoke_twice_throws()
    {
        var assignment = RoleAssignment.Grant(new TenantId(1), accountId: 1, roleId: 1, grantedByAccountId: 2, RoleAssignment.SourceManual);
        assignment.Revoke();

        Assert.Throws<InvalidOperationException>(() => assignment.Revoke());
    }

    [Fact]
    public void IsActiveAt_false_after_revocation()
    {
        var validFrom = DateTimeOffset.UtcNow.AddDays(-1);
        var assignment = RoleAssignment.Grant(new TenantId(1), 1, 1, 2, RoleAssignment.SourceManual, validFrom: validFrom);
        assignment.Revoke(validFrom.AddHours(1));

        Assert.True(assignment.IsActiveAt(validFrom.AddMinutes(30)));
        Assert.False(assignment.IsActiveAt(validFrom.AddHours(2)));
    }

    [Fact]
    public void Grant_rejects_invalid_source()
    {
        Assert.Throws<ArgumentException>(() => RoleAssignment.Grant(new TenantId(1), 1, 1, 2, "bogus"));
    }

    [Fact]
    public void Revoke_before_valid_from_throws()
    {
        var validFrom = DateTimeOffset.UtcNow;
        var assignment = RoleAssignment.Grant(new TenantId(1), 1, 1, 2, RoleAssignment.SourceManual, validFrom: validFrom);

        Assert.Throws<ArgumentOutOfRangeException>(() => assignment.Revoke(validFrom.AddMinutes(-1)));
    }
}
