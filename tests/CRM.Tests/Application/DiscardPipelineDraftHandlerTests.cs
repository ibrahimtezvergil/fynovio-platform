using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class DiscardPipelineDraftHandlerTests
{
    private readonly PostgresFixture _fixture;

    public DiscardPipelineDraftHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    private static readonly PrincipalRef Administrator = new("https://identity.test", "admin");

    private async Task<(TenantId Tenant, CreatePipelineDraftResult Draft)> SeedDraftAsync()
    {
        var tenant = TestData.NextTenant();
        await using var context = _fixture.CreateAdminContext();
        var draft = await new CreatePipelineDraftHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(new CreatePipelineDraftCommand(tenant, Administrator, null,
            "Sales", 0, 0, [new("Entry", 1, true, true)], false, [], Guid.NewGuid().ToString(), Guid.NewGuid()));
        return (tenant, draft);
    }

    private DiscardPipelineDraftCommand Discard(TenantId tenant, CreatePipelineDraftResult draft, string key) =>
        new(tenant, Administrator, draft.PipelineDefinitionId, draft.VersionId, key, Guid.NewGuid());

    [Fact]
    public async Task Archives_the_draft_and_replays_the_same_key()
    {
        var (tenant, draft) = await SeedDraftAsync();
        await using var context = _fixture.CreateAdminContext();
        var handler = new DiscardPipelineDraftHandler(context, StubAuthorizer.AlwaysAllow);

        var first = await handler.HandleAsync(Discard(tenant, draft, "discard-1"));
        var replay = await handler.HandleAsync(Discard(tenant, draft, "discard-1"));

        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        await using var read = _fixture.CreateAdminContext();
        Assert.Equal(PipelineVersionStatus.Archived, (await read.PipelineDefinitionVersions.SingleAsync(v => v.TenantId == tenant && v.Id == draft.VersionId)).Status);
    }

    [Fact]
    public async Task Refuses_a_version_that_is_no_longer_a_draft()
    {
        var (tenant, draft) = await SeedDraftAsync();
        await using var context = _fixture.CreateAdminContext();
        var handler = new DiscardPipelineDraftHandler(context, StubAuthorizer.AlwaysAllow);
        await handler.HandleAsync(Discard(tenant, draft, "discard-a"));

        await Assert.ThrowsAsync<CrmSettingsConcurrencyConflictException>(() => handler.HandleAsync(Discard(tenant, draft, "discard-b")));
    }

    [Fact]
    public async Task Denies_a_caller_without_the_settings_grant()
    {
        var (tenant, draft) = await SeedDraftAsync();
        await using var context = _fixture.CreateAdminContext();

        await Assert.ThrowsAnyAsync<Exception>(() => new DiscardPipelineDraftHandler(context, StubAuthorizer.AlwaysDeny).HandleAsync(Discard(tenant, draft, "discard-denied")));
        await using var read = _fixture.CreateAdminContext();
        Assert.Equal(PipelineVersionStatus.Draft, (await read.PipelineDefinitionVersions.SingleAsync(v => v.TenantId == tenant && v.Id == draft.VersionId)).Status);
    }
}
