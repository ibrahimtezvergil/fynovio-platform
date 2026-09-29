using Contracts;
using MasterData.Application;
using MasterData.Domain;
using Xunit;

namespace MasterData.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class PartySearchTests
{
    private readonly PostgresFixture _fixture;

    public PartySearchTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<TenantId> SeedAsync(params (PartyType Type, string Name, string? Surname, string? Email)[] parties)
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        foreach (var (type, name, surname, email) in parties)
            seed.Parties.Add(Party.Create(tenant, type, name, surname, null, email));
        await seed.SaveChangesAsync();
        return tenant;
    }

    private async Task<PartyDirectory> AdminDirectoryAsync() => new(_fixture.CreateAdminContext());

    private static readonly (PartyType, string, string?, string?)[] Sample =
    [
        (PartyType.Organization, "Acme Corp", null, "info@acme.test"),
        (PartyType.Person, "Ada", "Lovelace", "ada@analytical.test"),
        (PartyType.Person, "Grace", "Hopper", null),
        (PartyType.Organization, "100%_Literal", null, null)
    ];

    private static List<string> Names(IEnumerable<PartyDirectoryEntry> entries) => entries.Select(e => e.Name).ToList();

    [Fact]
    public async Task Matches_name_surname_full_name_and_email_case_insensitively()
    {
        var tenant = await SeedAsync(Sample);
        var directory = await AdminDirectoryAsync();

        Assert.Equal(["Acme Corp"], Names(await directory.SearchPartiesAsync(tenant, "ACME", 10)));
        Assert.Equal(["Ada"], Names(await directory.SearchPartiesAsync(tenant, "lovel", 10)));
        Assert.Equal(["Ada"], Names(await directory.SearchPartiesAsync(tenant, "ada love", 10)));
        Assert.Equal(["Ada"], Names(await directory.SearchPartiesAsync(tenant, "analytical.test", 10)));
        Assert.Empty(await directory.SearchPartiesAsync(tenant, "nobody-here", 10));
    }

    [Fact]
    public async Task Matches_a_phone_on_its_digits_and_returns_it()
    {
        var tenant = TestData.NextTenant();
        await using (var seed = _fixture.CreateAdminContext())
        {
            seed.Parties.Add(Party.Create(tenant, PartyType.Organization, "Acme Corp", null, "+90 (532) 111-22 33", null));
            seed.Parties.Add(Party.Create(tenant, PartyType.Organization, "Other Ltd", null, "0212 999 88 77", null));
            await seed.SaveChangesAsync();
        }
        var directory = await AdminDirectoryAsync();

        Assert.Equal(["Acme Corp"], Names(await directory.SearchPartiesAsync(tenant, "0532 111", 10)));
        Assert.Equal(["Acme Corp"], Names(await directory.SearchPartiesAsync(tenant, "(532) 111-22", 10)));
        var byDigits = Assert.Single(await directory.SearchPartiesAsync(tenant, "532111", 10));
        Assert.Equal("Acme Corp", byDigits.Name);
        Assert.Equal("+90 (532) 111-22 33", byDigits.Phone);
        Assert.Equal(["Other Ltd"], Names(await directory.SearchPartiesAsync(tenant, "999 88", 10)));
        Assert.Equal(["Other Ltd"], Names(await directory.SearchPartiesAsync(tenant, "0212", 10)));
    }

    [Fact]
    public async Task An_all_digit_query_also_finds_the_party_with_that_customer_number()
    {
        var tenant = await SeedAsync(Sample);
        var directory = await AdminDirectoryAsync();
        var acme = Assert.Single(await directory.SearchPartiesAsync(tenant, "acme", 10));

        var byNumber = await directory.SearchPartiesAsync(tenant, acme.PartyRef.PartyId.ToString(), 10);

        Assert.Contains(byNumber, entry => entry.PartyRef.PartyId == acme.PartyRef.PartyId);
        Assert.Empty(await directory.SearchPartiesAsync(tenant, "999999999", 10));
    }

    [Fact]
    public async Task Wildcards_are_literal()
    {
        var tenant = await SeedAsync(Sample);
        var directory = await AdminDirectoryAsync();

        Assert.Equal(["100%_Literal"], Names(await directory.SearchPartiesAsync(tenant, "%_", 10)));
        Assert.Empty(await directory.SearchPartiesAsync(tenant, "%zzz", 10));
        Assert.Equal(["100%_Literal"], Names(await directory.SearchPartiesAsync(tenant, @"0%_L", 10)));
    }

    [Fact]
    public async Task A_blank_query_lists_alphabetically_and_take_is_clamped()
    {
        var tenant = await SeedAsync(Sample);
        var directory = await AdminDirectoryAsync();

        Assert.Equal(["100%_Literal", "Acme Corp", "Ada", "Grace"], Names(await directory.SearchPartiesAsync(tenant, null, 10)));
        Assert.Equal(4, (await directory.SearchPartiesAsync(tenant, "   ", 10)).Count);
        Assert.Equal(2, (await directory.SearchPartiesAsync(tenant, null, 2)).Count);
        Assert.Single(await directory.SearchPartiesAsync(tenant, null, 0));
        Assert.Equal(4, (await directory.SearchPartiesAsync(tenant, null, 10_000)).Count);
    }

    [Fact]
    public async Task Results_carry_the_display_fields_and_a_ref_in_the_searched_tenant()
    {
        var tenant = await SeedAsync(Sample);
        var directory = await AdminDirectoryAsync();

        var ada = Assert.Single(await directory.SearchPartiesAsync(tenant, "Ada", 5));

        Assert.Equal(("Ada", "Lovelace", "ada@analytical.test", PartyType.Person), (ada.Name, ada.Surname, ada.Email, ada.PartyType));
        Assert.Equal(tenant, ada.PartyRef.TenantId);
        Assert.True(ada.PartyRef.PartyId > 0);
    }

    [Fact]
    public async Task Never_returns_another_tenants_parties()
    {
        var tenantA = await SeedAsync((PartyType.Organization, "Shared Name", null, null));
        var tenantB = await SeedAsync((PartyType.Organization, "Shared Name Two", null, null));
        var directory = await AdminDirectoryAsync();

        var inA = await directory.SearchPartiesAsync(tenantA, "Shared", 10);

        Assert.Equal(["Shared Name"], Names(inA));
        Assert.All(inA, e => Assert.Equal(tenantA, e.PartyRef.TenantId));
        Assert.NotEqual(tenantA, tenantB);
    }

    [Fact]
    public async Task A_merged_party_is_never_offered_only_its_survivor()
    {
        var tenant = TestData.NextTenant();
        long survivorId, duplicateId;
        await using (var seed = _fixture.CreateAdminContext())
        {
            var survivor = Party.Create(tenant, PartyType.Organization, "Globex");
            var duplicate = Party.Create(tenant, PartyType.Organization, "Globex (duplicate)");
            seed.Parties.AddRange(survivor, duplicate);
            await seed.SaveChangesAsync();
            survivorId = survivor.Id;
            duplicateId = duplicate.Id;
        }

        await using (var context = _fixture.CreateAdminContext())
            await new MergePartyHandler(context).HandleAsync(new MergePartyCommand(tenant, duplicateId, survivorId, TestData.Operator, "merge-search", Guid.NewGuid()));

        var found = await (await AdminDirectoryAsync()).SearchPartiesAsync(tenant, "Globex", 10);

        Assert.Equal([survivorId], found.Select(e => e.PartyRef.PartyId));
    }

    /// <summary>Regression: `masterdata.parties` is RLS-protected, and the directory used to query it without ever setting
    /// `app.tenant_id` — so as the unprivileged runtime role it saw no rows (the admin-connection tests hid this).</summary>
    [Fact]
    public async Task Every_directory_read_works_as_the_unprivileged_runtime_role()
    {
        var tenant = await SeedAsync(Sample);
        var other = await SeedAsync((PartyType.Organization, "Acme Elsewhere", null, null));
        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        var directory = new PartyDirectory(context);

        var search = await directory.SearchPartiesAsync(tenant, "acme", 10);
        Assert.Equal(["Acme Corp"], Names(search));

        var acme = search.Single().PartyRef;
        Assert.Equal("Acme Corp", (await directory.GetPartyAsync(acme))?.Name);
        Assert.Single(await directory.GetPartiesAsync([acme]));
        Assert.Equal(acme, await new PartyIdentityResolver(context).ResolveAsync(acme));

        // Another tenant's ref, asked under that tenant's own context, is answered only for that tenant.
        Assert.Empty(await directory.SearchPartiesAsync(TestData.NextTenant(), "acme", 10));
        Assert.NotEqual(tenant, other);
    }

    [Fact]
    public async Task A_guessed_party_id_from_another_tenant_resolves_to_nothing_as_the_runtime_role()
    {
        var tenantA = await SeedAsync((PartyType.Organization, "Only In A", null, null));
        var tenantB = await SeedAsync((PartyType.Organization, "Only In B", null, null));
        long idInA;
        await using (var admin = _fixture.CreateAdminContext())
            idInA = (await new PartyDirectory(admin).SearchPartiesAsync(tenantA, "Only", 5)).Single().PartyRef.PartyId;

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        var directory = new PartyDirectory(context);

        Assert.Null(await directory.GetPartyAsync(new PartyRef(tenantB, idInA)));
        Assert.Null(await new PartyIdentityResolver(context).ResolveAsync(new PartyRef(tenantB, idInA)));
    }
}
