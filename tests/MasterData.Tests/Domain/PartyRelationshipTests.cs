using Contracts;
using MasterData.Domain;
using Xunit;

namespace MasterData.Tests.Domain;

public sealed class PartyRelationshipTests
{
    [Fact]
    public void Create_starts_active()
    {
        var tenant = TestData.NextTenant();

        var relationship = PartyRelationship.Create(tenant, fromPartyId: 1, toPartyId: 2, PartyRelationshipType.WorksFor);

        Assert.Equal(PartyRelationshipStatus.Active, relationship.Status);
        Assert.Null(relationship.EndedAt);
    }

    [Fact]
    public void Create_rejects_a_relationship_with_self()
    {
        var tenant = TestData.NextTenant();

        Assert.Throws<ArgumentException>(() =>
            PartyRelationship.Create(tenant, fromPartyId: 1, toPartyId: 1, PartyRelationshipType.WorksFor));
    }

    [Fact]
    public void End_requires_not_already_ended()
    {
        var tenant = TestData.NextTenant();
        var relationship = PartyRelationship.Create(tenant, 1, 2, PartyRelationshipType.WorksFor);
        relationship.End();

        Assert.Throws<InvalidOperationException>(() => relationship.End());
    }

    [Fact]
    public void End_sets_ended_at()
    {
        var tenant = TestData.NextTenant();
        var relationship = PartyRelationship.Create(tenant, 1, 2, PartyRelationshipType.WorksFor);

        relationship.End();

        Assert.NotNull(relationship.EndedAt);
        Assert.Equal(PartyRelationshipStatus.Ended, relationship.Status);
    }
}
