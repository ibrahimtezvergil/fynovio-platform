using Contracts;
using SemanticCatalog.Domain;

namespace SemanticCatalog.Tests.Domain;

public sealed class ChangeSetTests
{
    private static readonly TenantId Tenant = new(1);
    private static readonly PrincipalRef Author = new("https://identity.test", "admin");

    private static ChangeSetItem Create(string key = "region", string label = "Region") =>
        ChangeSetItem.ForField(ChangeOperation.Create, null,
            new FieldChangeContent("crm", "opportunity", key, label, FieldType.Text, false, null, 0, 0));

    private static ChangeSet NewSet(long baseRevision = 0) => ChangeSet.Draft(Tenant, Author, baseRevision, [Create()]);

    private static ChangeSet InStatus(ChangeSetStatus status)
    {
        var set = NewSet();
        switch (status)
        {
            case ChangeSetStatus.Draft: break;
            case ChangeSetStatus.Validated: set.Validate(); break;
            case ChangeSetStatus.AwaitingApproval: set.Validate(); set.SubmitForApproval(); break;
            case ChangeSetStatus.Approved: set.Validate(); set.SubmitForApproval(); set.Approve(); break;
            case ChangeSetStatus.Published: set.Validate(); set.SubmitForApproval(); set.Approve(); set.Publish(1); break;
            case ChangeSetStatus.Activating: set.Validate(); set.SubmitForApproval(); set.Approve(); set.Publish(1); set.BeginActivation(); break;
            case ChangeSetStatus.Active: set.Validate(); set.SubmitForApproval(); set.Approve(); set.Publish(1); set.CompleteActivation(); break;
            case ChangeSetStatus.ActivationFailed: set.Validate(); set.SubmitForApproval(); set.Approve(); set.Publish(1); set.BeginActivation(); set.FailActivation("participant refused"); break;
            case ChangeSetStatus.Rejected: set.Validate(); set.SubmitForApproval(); set.Reject(); break;
            case ChangeSetStatus.Discarded: set.Discard(); break;
            case ChangeSetStatus.Superseded: set.Supersede(); break;
        }
        return set;
    }

    private static readonly Dictionary<ChangeSetStatus, ChangeSetStatus[]> Legal = new()
    {
        [ChangeSetStatus.Draft] = [ChangeSetStatus.Validated, ChangeSetStatus.Discarded, ChangeSetStatus.Superseded],
        [ChangeSetStatus.Validated] = [ChangeSetStatus.AwaitingApproval, ChangeSetStatus.Discarded, ChangeSetStatus.Superseded],
        [ChangeSetStatus.AwaitingApproval] = [ChangeSetStatus.Approved, ChangeSetStatus.Rejected, ChangeSetStatus.Discarded, ChangeSetStatus.Superseded],
        [ChangeSetStatus.Approved] = [ChangeSetStatus.Published, ChangeSetStatus.Superseded],
        [ChangeSetStatus.Published] = [ChangeSetStatus.Activating, ChangeSetStatus.Active],
        [ChangeSetStatus.Activating] = [ChangeSetStatus.Active, ChangeSetStatus.ActivationFailed],
        [ChangeSetStatus.ActivationFailed] = [ChangeSetStatus.Activating],
        [ChangeSetStatus.Active] = [],
        [ChangeSetStatus.Rejected] = [],
        [ChangeSetStatus.Discarded] = [],
        [ChangeSetStatus.Superseded] = []
    };

    public static TheoryData<ChangeSetStatus, ChangeSetStatus> EveryPair()
    {
        var data = new TheoryData<ChangeSetStatus, ChangeSetStatus>();
        foreach (var from in Enum.GetValues<ChangeSetStatus>())
            foreach (var to in Enum.GetValues<ChangeSetStatus>())
                data.Add(from, to);
        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPair))]
    public void Exactly_the_documented_moves_are_legal_and_every_other_one_is_refused(ChangeSetStatus from, ChangeSetStatus to)
    {
        var legal = Legal[from].Contains(to);

        Assert.Equal(legal, ChangeSet.CanMove(from, to));
    }

    [Fact]
    public void The_one_item_flow_walks_to_active_and_records_each_move()
    {
        var set = NewSet();

        set.Validate();
        set.SubmitForApproval();
        set.Approve();
        set.Publish(4);
        set.CompleteActivation();

        Assert.Equal(ChangeSetStatus.Active, set.Status);
        Assert.Equal(4, set.PublishedRevision);
        Assert.Equal(
            ["Draft->Validated", "Validated->AwaitingApproval", "AwaitingApproval->Approved", "Approved->Published", "Published->Active"],
            set.Transitions.Select(t => $"{t.From}->{t.To}"));
        Assert.Equal(6, set.RowVersion);
    }

    [Theory]
    [InlineData(ChangeSetStatus.Draft)]
    [InlineData(ChangeSetStatus.Validated)]
    [InlineData(ChangeSetStatus.AwaitingApproval)]
    public void A_set_that_was_not_approved_cannot_be_published(ChangeSetStatus status)
    {
        var set = InStatus(status);

        Assert.Throws<InvalidOperationException>(() => set.Publish(1));
        Assert.Null(set.PublishedRevision);
    }

    [Fact]
    public void An_activation_that_failed_can_be_retried_but_an_active_set_is_final()
    {
        var failed = InStatus(ChangeSetStatus.ActivationFailed);
        Assert.Equal("participant refused", failed.FailureReason);
        failed.BeginActivation();
        failed.CompleteActivation();
        Assert.Equal(ChangeSetStatus.Active, failed.Status);

        Assert.Throws<InvalidOperationException>(failed.Supersede);
        Assert.Throws<InvalidOperationException>(failed.Discard);
    }

    [Fact]
    public void A_published_set_can_no_longer_be_superseded_or_discarded()
    {
        var set = InStatus(ChangeSetStatus.Published);

        Assert.Throws<InvalidOperationException>(set.Supersede);
        Assert.Throws<InvalidOperationException>(set.Discard);
    }

    [Fact]
    public void The_content_hash_is_the_same_for_the_same_logical_change_and_ignores_who_when_and_which_base()
    {
        var first = ChangeSet.Draft(Tenant, Author, 0, [Create()]);
        var second = ChangeSet.Draft(new TenantId(99), new PrincipalRef("https://other", "someone"), 17, [Create()]);

        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.Matches("^[0-9a-f]{64}$", first.ContentHash);
    }

    [Fact]
    public void The_content_hash_changes_with_any_change_of_content_or_order()
    {
        var baseline = ChangeSet.Draft(Tenant, Author, 0, [Create("region", "Region"), Create("tier", "Tier")]);

        Assert.NotEqual(baseline.ContentHash, ChangeSet.Draft(Tenant, Author, 0, [Create("region", "Regions"), Create("tier", "Tier")]).ContentHash);
        Assert.NotEqual(baseline.ContentHash, ChangeSet.Draft(Tenant, Author, 0, [Create("tier", "Tier"), Create("region", "Region")]).ContentHash);
        Assert.NotEqual(baseline.ContentHash, ChangeSet.Draft(Tenant, Author, 0, [Create("region", "Region")]).ContentHash);
    }

    [Fact]
    public void A_set_needs_items_a_non_negative_base_and_no_two_items_for_one_definition()
    {
        Assert.Throws<ArgumentException>(() => ChangeSet.Draft(Tenant, Author, 0, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChangeSet.Draft(Tenant, Author, -1, [Create()]));
        Assert.Throws<ArgumentException>(() => ChangeSet.Draft(Tenant, Author, 0, [Create("region"), Create("region", "Again")]));
        var update = new FieldChangeContent("crm", "opportunity", "region", "Region", null, false, null, 0, 1);
        Assert.Throws<ArgumentException>(() => ChangeSet.Draft(Tenant, Author, 0,
            [ChangeSetItem.ForField(ChangeOperation.Update, 5, update), ChangeSetItem.ForField(ChangeOperation.Deprecate, 5, update)]));
    }

    [Fact]
    public void An_item_needs_a_target_exactly_when_it_changes_an_existing_definition()
    {
        var content = new FieldChangeContent("crm", "opportunity", "region", "Region", FieldType.Text, false, null, 0, 1);

        Assert.Throws<ArgumentException>(() => ChangeSetItem.ForField(ChangeOperation.Create, 5, content));
        Assert.Throws<ArgumentException>(() => ChangeSetItem.ForField(ChangeOperation.Update, null, content));
        Assert.Throws<ArgumentException>(() => ChangeSetItem.ForField(ChangeOperation.Deprecate, 0, content));
    }
}
