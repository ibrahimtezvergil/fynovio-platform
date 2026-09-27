using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests.Integration;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class SetPipelineLifecycleHandlerTests
{
    private static readonly PrincipalRef Administrator = new("https://identity.test", "crm-settings-admin");
    private readonly PostgresFixture _fixture;

    public SetPipelineLifecycleHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task HandleAsync_rejects_archiving_the_tenants_only_active_pipeline_even_with_no_CrmSettings_row()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        var definition = PipelineDefinition.Create(tenant, "Sales");
        seed.PipelineDefinitions.Add(definition);
        await seed.SaveChangesAsync();
        // deliberately no CrmSettings row for this tenant — the pre-fix gap this test closes

        await using var context = _fixture.CreateAdminContext();
        var handler = new SetPipelineLifecycleHandler(context, StubAuthorizer.AlwaysAllow);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new SetPipelineLifecycleCommand(tenant, Administrator, definition.Id, definition.RowVersion,
                IsActive: false, Archive: true, Restore: false, "archive-only-pipeline", Guid.NewGuid())));
    }
}
