using Contracts;

namespace CRM.Tests;

/// <summary>Her test kendi tenant'ıyla çalışır; testler arasında veri sızmasın diye
/// tenant id'leri artan bir sayaçtan gelir (AGENTS.md: testlerde sabit id yok).</summary>
public static class TestData
{
    private static long _nextTenantId = 1000;

    public static TenantId NextTenant() => new(Interlocked.Increment(ref _nextTenantId));

    public static PrincipalRef Seller { get; } = new("https://idp.local", "seller-1");

    public static EntityRef ProductRef(TenantId tenantId, long productId = 1) =>
        new(tenantId, "masterdata", "product", productId);
}
