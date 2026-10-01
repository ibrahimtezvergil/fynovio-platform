using Contracts;
using SemanticCatalog.Application;
using SemanticCatalog.Domain;
using Xunit;

namespace SemanticCatalog.Tests.Integration;

/// <summary>The catalog evaluates CRM's settings actions itself (it cannot call CRM). These pin the exact action keys and
/// resource descriptor CRM's settings handlers use (`GetCrmSettingsHandler.AuthorizeAsync`: `CrmSettings`, no id, no
/// owner), so a drift on either side fails here instead of quietly changing who may edit definitions.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class CatalogAuthorizationAgreementTests(PostgresFixture fixture)
{
    private static readonly PrincipalRef Administrator = new("https://identity.test", "crm-settings-admin");

    [Fact]
    public void The_crm_owner_maps_to_the_crm_settings_actions_and_resource()
    {
        var owner = CatalogOwners.Require("crm");

        Assert.Equal("crm.settings.read", owner.ReadAction);
        Assert.Equal("crm.settings.update", owner.WriteAction);
        Assert.Equal("CrmSettings", owner.ResourceType);
        Assert.Throws<ArgumentException>(() => CatalogOwners.Require("sales"));
    }

    [Fact]
    public async Task Writes_ask_for_settings_update_and_reads_for_settings_read_on_the_settings_resource()
    {
        var tenant = TestTenants.Next();
        var recorder = new RecordingAuthorizer();
        await using var context = fixture.CreateAdminContext();

        await new ManageFieldDefinitionHandler(context, recorder).HandleAsync(new ManageFieldDefinitionCommand(
            tenant, Administrator, FieldOperation.Create, null, 0, "crm", "opportunity", "asked", "Asked", FieldType.Text, false, null, 0, "agree-write", Guid.NewGuid()));
        await new ListFieldDefinitionsHandler(context, recorder).HandleAsync(
            new ListFieldDefinitionsQuery(tenant, Administrator, "crm", "opportunity", Guid.NewGuid()));

        Assert.Collection(recorder.Requests,
            write => Assert.Equal(("crm.settings.update", "CrmSettings", null, null), (write.Action.Value, write.Resource.ResourceType, write.Resource.Id, write.Resource.OwnerPrincipal)),
            read => Assert.Equal(("crm.settings.read", "CrmSettings", null, null), (read.Action.Value, read.Resource.ResourceType, read.Resource.Id, read.Resource.OwnerPrincipal)));
    }
}
