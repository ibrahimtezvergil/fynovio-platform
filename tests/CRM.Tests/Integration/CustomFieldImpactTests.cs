using Contracts;
using CRM.Application;
using CRM.Domain;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>The data half of "what breaks if I deprecate this field": CRM counts the opportunities that hold a value, and
/// reads the definition through the catalog's reader (a stub here; the real reader is proven in SemanticCatalog.Tests).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class CustomFieldImpactTests(PostgresFixture fixture)
{
    private static readonly PrincipalRef Administrator = new("https://identity.test", "crm-settings-admin");

    [Fact]
    public async Task Impact_counts_opportunities_with_the_field_in_this_tenant_only()
    {
        var tenant = TestData.NextTenant();
        var reader = new StubDefinitionReader();
        var definition = reader.Add(TestFields.Create(tenant, "impact_field", "Impact Field", FieldType.Text));

        await using (var context = fixture.CreateAdminContext())
        {
            context.Opportunities.AddRange(
                Opportunity.Create(tenant, new PartyRef(tenant, 1), TestData.Seller, "USD", 100m, customFields: """{"impact_field": "value1"}"""),
                Opportunity.Create(tenant, new PartyRef(tenant, 2), TestData.Seller, "USD", 200m, customFields: """{"other_field": "value2"}"""),
                Opportunity.Create(tenant, new PartyRef(tenant, 3), TestData.Seller, "USD", 300m, customFields: """{"impact_field": "value3"}"""));
            await context.SaveChangesAsync();
        }

        // Another tenant's opportunity holding the same key must not be counted.
        var otherTenant = TestData.NextTenant();
        await using (var context = fixture.CreateAdminContext())
        {
            context.Opportunities.Add(Opportunity.Create(otherTenant, new PartyRef(otherTenant, 1), TestData.Seller, "USD", 1m, customFields: """{"impact_field": "x"}"""));
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateAdminContext())
        {
            var impact = await new GetCustomFieldImpactHandler(context, StubAuthorizer.AlwaysAllow, reader)
                .HandleAsync(new GetCustomFieldImpactQuery(tenant, Administrator, definition.Id, Guid.NewGuid()));

            Assert.Equal(definition.Id, impact.DefinitionId);
            Assert.Equal("impact_field", impact.FieldName);
            Assert.Equal(2, impact.OpportunitiesWithValue);
        }
    }

    [Fact]
    public async Task Impact_is_gated_by_settings_update_authorization()
    {
        var tenant = TestData.NextTenant();
        var reader = new StubDefinitionReader();
        var definition = reader.Add(TestFields.Create(tenant, "auth_field", "Auth Field", FieldType.Text));

        await using var context = fixture.CreateAdminContext();
        await Assert.ThrowsAsync<OpportunityAuthorizationDeniedException>(() =>
            new GetCustomFieldImpactHandler(context, StubAuthorizer.AlwaysDeny, reader)
                .HandleAsync(new GetCustomFieldImpactQuery(tenant, Administrator, definition.Id, Guid.NewGuid())));
    }

    [Fact]
    public async Task Impact_of_an_unknown_or_foreign_definition_is_not_found()
    {
        var tenant = TestData.NextTenant();
        var other = TestData.NextTenant();
        var reader = new StubDefinitionReader();
        var foreign = reader.Add(TestFields.Create(other, "foreign_field", "Foreign", FieldType.Text));

        await using var context = fixture.CreateAdminContext();
        var handler = new GetCustomFieldImpactHandler(context, StubAuthorizer.AlwaysAllow, reader);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.HandleAsync(new GetCustomFieldImpactQuery(tenant, Administrator, foreign.Id, Guid.NewGuid())));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.HandleAsync(new GetCustomFieldImpactQuery(tenant, Administrator, 987_654, Guid.NewGuid())));
    }

    [Fact]
    public async Task Impact_also_lists_the_active_views_that_place_the_field()
    {
        var tenant = TestData.NextTenant();
        var reader = new StubDefinitionReader();
        var definition = reader.Add(TestFields.Create(tenant, "region", "Region", FieldType.Text));
        reader.DependentViews.Add(new DependentView(5, "pipeline_review", "Pipeline review"));

        await using var context = fixture.CreateAdminContext();
        var impact = await new GetCustomFieldImpactHandler(context, StubAuthorizer.AlwaysAllow, reader)
            .HandleAsync(new GetCustomFieldImpactQuery(tenant, Administrator, definition.Id, Guid.NewGuid()));

        Assert.Equal([new DependentView(5, "pipeline_review", "Pipeline review")], impact.DependentViews);
        Assert.Equal(0, impact.OpportunitiesWithValue);
    }
}
