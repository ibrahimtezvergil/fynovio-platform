using Contracts;
using MasterData.Domain;
using Xunit;

namespace MasterData.Tests.Domain;

/// <summary>Single-hop only, enforced as a domain guard — the resolution logic
/// (redirecting to an already-merged target's own canonical) lives in the MergeParty
/// application handler, not here; this guard is what makes that resolution mandatory
/// rather than optional. See docs/plans/2026-09-16-masterdata-party-foundation.md §5.</summary>
public sealed class PartyMergeTests
{
    [Fact]
    public void MergeInto_rejects_merging_into_self()
    {
        var tenant = TestData.NextTenant();
        var party = Party.Create(tenant, PartyType.Organization, "ABC AŞ");

        Assert.Throws<InvalidOperationException>(() => party.MergeInto(party));
    }

    [Fact]
    public void MergeInto_rejects_a_target_that_is_itself_a_tombstone()
    {
        var tenant = TestData.NextTenant();
        var a = Party.Create(tenant, PartyType.Organization, "A");
        var b = Party.Create(tenant, PartyType.Organization, "B");
        var c = Party.Create(tenant, PartyType.Organization, "C");
        b.MergeInto(a);

        Assert.Throws<InvalidOperationException>(() => c.MergeInto(b));
    }

    [Fact]
    public void MergeInto_sets_the_tombstone_flag()
    {
        var tenant = TestData.NextTenant();
        var a = Party.Create(tenant, PartyType.Organization, "A");
        var b = Party.Create(tenant, PartyType.Organization, "B");

        b.MergeInto(a);

        Assert.NotNull(b.MergedIntoPartyId);
    }
}
