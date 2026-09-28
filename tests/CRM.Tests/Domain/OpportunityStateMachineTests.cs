using Contracts;
using CRM.Domain;
using Xunit;

namespace CRM.Tests.Domain;

public sealed class OpportunityStateMachineTests
{
    private static Opportunity NewDraftOpportunity()
    {
        var tenant = TestData.NextTenant();
        return Opportunity.Create(tenant, new PartyRef(tenant, 1), TestData.Seller, "TRY", estimatedAmount: 1000m);
    }

    [Fact]
    public void Create_starts_in_draft()
    {
        var opportunity = NewDraftOpportunity();

        Assert.Equal(OpportunityStatus.Draft, opportunity.Status);
        Assert.Null(opportunity.ExpiryDate);
    }

    [Fact]
    public void Archive_and_restore_preserve_lifecycle_and_stage()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: 42, pipelineStageId: 7);
        var beforeArchiveVersion = opportunity.RowVersion;

        opportunity.Archive(confirmOpenOpportunity: true);

        Assert.True(opportunity.IsArchived);
        Assert.Equal(OpportunityStatus.Open, opportunity.Status);
        Assert.Equal(7, opportunity.PipelineStageId);
        Assert.True(opportunity.RowVersion > beforeArchiveVersion);

        opportunity.Restore();

        Assert.False(opportunity.IsArchived);
        Assert.Null(opportunity.ArchivedAt);
        Assert.Equal(OpportunityStatus.Open, opportunity.Status);
        Assert.Equal(7, opportunity.PipelineStageId);
    }

    [Fact]
    public void Open_archive_requires_confirmation_and_terminal_records_cannot_be_archived()
    {
        var open = NewDraftOpportunity();
        open.Open(DateTimeOffset.UtcNow.AddDays(7), null, null);
        Assert.Throws<InvalidOperationException>(() => open.Archive(confirmOpenOpportunity: false));

        var won = WonOpportunity();
        Assert.Throws<InvalidOperationException>(() => won.Archive(confirmOpenOpportunity: true));
    }

    [Fact]
    public void Create_rejects_a_currency_that_is_not_three_letters()
    {
        var tenant = TestData.NextTenant();

        Assert.Throws<ArgumentException>(() =>
            Opportunity.Create(tenant, new PartyRef(tenant, 1), TestData.Seller, "TRYX", 1000m));
    }

    [Fact]
    public void Open_requires_a_future_expiry_date()
    {
        var opportunity = NewDraftOpportunity();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            opportunity.Open(DateTimeOffset.UtcNow.AddDays(-1), pipelineDefinitionVersionId: null, pipelineStageId: null));
    }

    [Fact]
    public void Open_moves_to_open_and_stamps_opened_date()
    {
        var opportunity = NewDraftOpportunity();

        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: null, pipelineStageId: null);

        Assert.Equal(OpportunityStatus.Open, opportunity.Status);
        Assert.NotNull(opportunity.OpenedDate);
        Assert.NotNull(opportunity.ExpiryDate);
    }

    [Fact]
    public void Win_is_rejected_while_draft()
    {
        var opportunity = NewDraftOpportunity();

        Assert.Throws<InvalidOperationException>(() => opportunity.Win());
    }

    [Fact]
    public void Win_is_rejected_without_an_active_required_line()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 100m, isOptional: true);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: null, pipelineStageId: null);

        Assert.Throws<InvalidOperationException>(() => opportunity.Win());
    }

    [Fact]
    public void Win_succeeds_with_one_active_required_line()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 2, unitPrice: 50m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: null, pipelineStageId: null);

        opportunity.Win();

        Assert.Equal(OpportunityStatus.Won, opportunity.Status);
        Assert.NotNull(opportunity.WonDate);
    }

    [Fact]
    public void Lose_requires_a_reason()
    {
        var opportunity = NewDraftOpportunity();

        Assert.Throws<ArgumentException>(() => opportunity.Lose("  "));
    }

    [Fact]
    public void Lose_from_draft_is_allowed()
    {
        var opportunity = NewDraftOpportunity();

        opportunity.Lose("müşteri vazgeçti");

        Assert.Equal(OpportunityStatus.Lost, opportunity.Status);
        Assert.NotNull(opportunity.LostDate);
    }

    [Fact]
    public void AddLine_is_rejected_after_open()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: null, pipelineStageId: null);

        Assert.Throws<InvalidOperationException>(() =>
            opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 10m));
    }

    // CRM_Phase1_Test_Coverage_Verification_Report.pdf F-09: full Draft/Open/Won/Lost x
    // command transition matrix. Legal paths and the guards already exercised above; the
    // remaining terminal-state and re-entrancy rejections that had no test:

    [Fact]
    public void AddLine_is_rejected_after_won()
    {
        var opportunity = WonOpportunity();

        Assert.Throws<InvalidOperationException>(() =>
            opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 10m));
    }

    [Fact]
    public void AddLine_is_rejected_after_lost()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.Lose("müşteri vazgeçti");

        Assert.Throws<InvalidOperationException>(() =>
            opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 10m));
    }

    [Fact]
    public void Open_is_rejected_when_already_open()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: null, pipelineStageId: null);

        Assert.Throws<InvalidOperationException>(() => opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: null, pipelineStageId: null));
    }

    [Fact]
    public void Open_is_rejected_when_won()
    {
        var opportunity = WonOpportunity();

        Assert.Throws<InvalidOperationException>(() => opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: null, pipelineStageId: null));
    }

    [Fact]
    public void Open_is_rejected_when_lost()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.Lose("müşteri vazgeçti");

        Assert.Throws<InvalidOperationException>(() => opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: null, pipelineStageId: null));
    }

    [Fact]
    public void Win_is_rejected_when_already_won()
    {
        var opportunity = WonOpportunity();

        Assert.Throws<InvalidOperationException>(() => opportunity.Win());
    }

    [Fact]
    public void Win_is_rejected_when_lost()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.Lose("müşteri vazgeçti");

        Assert.Throws<InvalidOperationException>(() => opportunity.Win());
    }

    [Fact]
    public void Lose_is_rejected_when_already_won()
    {
        var opportunity = WonOpportunity();

        Assert.Throws<InvalidOperationException>(() => opportunity.Lose("çok geç"));
    }

    [Fact]
    public void Lose_is_rejected_when_already_lost()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.Lose("müşteri vazgeçti");

        Assert.Throws<InvalidOperationException>(() => opportunity.Lose("ikinci kez"));
    }

    [Fact]
    public void CancelLine_after_lost_is_rejected()
    {
        var opportunity = NewDraftOpportunity();
        var line = opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 100m);
        opportunity.Lose("müşteri vazgeçti");

        Assert.Throws<InvalidOperationException>(() => opportunity.CancelLine(line, "geç kaldı"));
    }

    [Fact]
    public void Reassign_changes_the_assigned_principal()
    {
        var opportunity = NewDraftOpportunity();
        var newOwner = new PrincipalRef("https://idp.local", "seller-2");

        opportunity.Reassign(newOwner);

        Assert.Equal(newOwner, opportunity.AssignedPrincipal);
    }

    [Fact]
    public void Reassign_is_rejected_once_won()
    {
        var tenant = TestData.NextTenant();
        var opportunity = Opportunity.Create(tenant, new PartyRef(tenant, 1), TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: null, pipelineStageId: null);
        opportunity.Win();

        Assert.Throws<InvalidOperationException>(() => opportunity.Reassign(new PrincipalRef("https://idp.local", "seller-2")));
    }

    [Fact]
    public void Reassign_is_rejected_once_lost()
    {
        var tenant = TestData.NextTenant();
        var opportunity = Opportunity.Create(tenant, new PartyRef(tenant, 1), TestData.Seller, "TRY", 1000m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: null, pipelineStageId: null);
        opportunity.Lose("reason");

        Assert.Throws<InvalidOperationException>(() => opportunity.Reassign(new PrincipalRef("https://idp.local", "seller-2")));
    }

    [Fact]
    public void ChangeStage_only_while_open()
    {
        var opportunity = NewDraftOpportunity();

        Assert.Throws<InvalidOperationException>(() => opportunity.ChangeStage(pipelineStageId: 99));
    }

    [Fact]
    public void ChangeStage_sets_the_new_stage_while_open()
    {
        var tenant = TestData.NextTenant();
        var opportunity = Opportunity.Create(tenant, new PartyRef(tenant, 1), TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: 10, pipelineStageId: 20);

        opportunity.ChangeStage(pipelineStageId: 30);

        Assert.Equal(30, opportunity.PipelineStageId);
        Assert.Equal(10, opportunity.PipelineDefinitionVersionId); // unchanged — same version, different stage
    }

    private static Opportunity WonOpportunity()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 100m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: null, pipelineStageId: null);
        opportunity.Win();
        return opportunity;
    }

    [Fact]
    public void Win_sets_the_won_stage_and_records_where_it_closed_from()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 100m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: 1, pipelineStageId: 7);

        opportunity.Win(wonStageId: 99);

        Assert.Equal(99, opportunity.PipelineStageId);
        Assert.Equal(7, opportunity.ClosedFromStageId);
    }

    [Fact]
    public void Win_without_a_stage_argument_leaves_the_pipeline_stage_untouched()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 100m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: 1, pipelineStageId: 7);

        opportunity.Win();

        Assert.Equal(7, opportunity.PipelineStageId);
        // ClosedFromStageId is captured unconditionally on every Win/Lose call, regardless of
        // whether a new stage overwrites the live one — "where it was when it closed" is
        // still 7 here, not null. (A prior code-quality-review suggestion assumed this should
        // be null; verified against Win's actual unconditional `ClosedFromStageId =
        // PipelineStageId` line and confirmed that assumption was wrong.)
        Assert.Equal(7, opportunity.ClosedFromStageId);
    }

    [Fact]
    public void Lose_from_open_sets_the_lost_stage_and_records_where_it_closed_from()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: 1, pipelineStageId: 7);

        opportunity.Lose("müşteri vazgeçti", lostStageId: 100);

        Assert.Equal(100, opportunity.PipelineStageId);
        Assert.Equal(7, opportunity.ClosedFromStageId);
    }

    [Fact]
    public void Lose_from_draft_leaves_both_stage_fields_null()
    {
        var opportunity = NewDraftOpportunity();

        opportunity.Lose("hiç açılmadı");

        Assert.Null(opportunity.PipelineStageId);
        Assert.Null(opportunity.ClosedFromStageId);
    }

    [Fact]
    public void Lose_rejects_a_stage_id_for_an_opportunity_that_was_never_open()
    {
        var opportunity = NewDraftOpportunity();

        Assert.Throws<ArgumentException>(() => opportunity.Lose("hiç açılmadı", lostStageId: 100));
    }
}
