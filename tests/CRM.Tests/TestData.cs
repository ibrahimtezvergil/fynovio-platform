using Contracts;
using MasterData.Domain;
using MasterData.Persistence;

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

    /// <summary>Seeds a Party to the MasterData schema and returns a PartyRef pointing to it.
    /// The party is created as an Organization type. Used for CRM tests that need to
    /// reference a party before creating an Opportunity.</summary>
    public static async Task<PartyRef> CreatePartyAsync(
        MasterDataDbContext context,
        TenantId tenantId,
        string name,
        string? surname = null,
        string? phone = null,
        string? email = null)
    {
        var party = Party.Create(tenantId, PartyType.Organization, name, surname, phone, email);
        context.Parties.Add(party);
        await context.SaveChangesAsync();
        return new PartyRef(tenantId, party.Id);
    }
}
