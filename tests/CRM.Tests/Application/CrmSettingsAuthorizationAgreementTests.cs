using Contracts;
using CRM.Application;
using CRM.Tests.Integration;
using Xunit;

namespace CRM.Tests.Application;

/// <summary>Half of an agreement test (the other half is SemanticCatalog.Tests' CatalogAuthorizationAgreementTests): the
/// Semantic Catalog evaluates these same settings actions on CRM's behalf, so the action keys and the resource descriptor
/// are pinned on both sides and a drift fails a test instead of silently changing who may edit field definitions.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class CrmSettingsAuthorizationAgreementTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Crm_settings_reads_ask_for_settings_read_on_the_settings_resource()
    {
        var recorder = new RecordingAuthorizer();
        await using var context = fixture.CreateAdminContext();

        await new GetCrmSettingsHandler(context, recorder).HandleAsync(
            new GetCrmSettingsQuery(TestData.NextTenant(), TestData.Seller, Guid.NewGuid()));

        var request = Assert.Single(recorder.Requests);
        Assert.Equal(("crm.settings.read", "CrmSettings", (long?)null, (PrincipalRef?)null),
            (request.Action.Value, request.Resource.ResourceType, request.Resource.Id, request.Resource.OwnerPrincipal));
        Assert.Equal("crm.settings.update", CrmActionKeys.SettingsUpdate);
    }

    private sealed class RecordingAuthorizer : IAuthorizer
    {
        public List<AuthorizationRequest> Requests { get; } = [];

        public Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new AuthorizationDecision(AuthorizationEffect.Allow, "recorded", Guid.NewGuid(), 0, AuthorizationDenialStage.None));
        }
    }
}
