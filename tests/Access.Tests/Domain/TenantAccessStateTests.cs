using Access.Domain.Authorization;
using Contracts;

namespace Access.Tests.Domain;

public sealed class TenantAccessStateTests
{
    [Fact]
    public void BumpRevision_increments_monotonically()
    {
        var state = TenantAccessState.Initialize(new TenantId(1));
        state.BumpRevision();
        state.BumpRevision();

        Assert.Equal(2, state.Revision);
    }

    [Fact]
    public void Initialize_starts_at_zero()
    {
        var state = TenantAccessState.Initialize(new TenantId(1));
        Assert.Equal(0, state.Revision);
    }
}
