using Contracts;

namespace MasterData.Tests;

public static class TestData
{
    private static long _nextTenantId = 1000;

    public static TenantId NextTenant() => new(Interlocked.Increment(ref _nextTenantId));

    public static PrincipalRef Operator { get; } = new("https://idp.local", "masterdata-operator-1");
}
