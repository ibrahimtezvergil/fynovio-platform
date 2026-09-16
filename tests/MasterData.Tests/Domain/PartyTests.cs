using Contracts;
using MasterData.Domain;
using Xunit;

namespace MasterData.Tests.Domain;

public sealed class PartyTests
{
    [Fact]
    public void Create_rejects_an_empty_name()
    {
        var tenant = TestData.NextTenant();

        Assert.Throws<ArgumentException>(() =>
            Party.Create(tenant, PartyType.Person, "  "));
    }

    [Fact]
    public void Create_rejects_a_surname_on_an_organization()
    {
        var tenant = TestData.NextTenant();

        Assert.Throws<ArgumentException>(() =>
            Party.Create(tenant, PartyType.Organization, "ABC AŞ", surname: "Yılmaz"));
    }

    [Fact]
    public void Create_allows_a_surname_on_a_person()
    {
        var tenant = TestData.NextTenant();

        var party = Party.Create(tenant, PartyType.Person, "Ahmet", surname: "Yılmaz");

        Assert.Equal("Yılmaz", party.Surname);
    }
}
