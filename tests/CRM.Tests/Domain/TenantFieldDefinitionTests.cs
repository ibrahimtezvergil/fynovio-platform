using CRM.Customization;

namespace CRM.Tests.Domain;

public sealed class TenantFieldDefinitionTests
{
    private static readonly Contracts.TenantId Tenant = new(1);

    private static TenantFieldConfig Options(params TenantFieldOption[] options) => new(Options: options);

    private static TenantFieldOption O(string key, string label, bool isDeprecated = false) => new(key, label, isDeprecated);

    [Theory]
    [InlineData("budget")]
    [InlineData("lead_source_2")]
    public void A_valid_key_is_accepted(string key)
    {
        var field = TenantFieldDefinition.Create(Tenant, TenantFieldAggregateType.Opportunity, key, "Budget", TenantFieldValueType.Text);

        Assert.Equal(key, field.FieldName);
        Assert.Equal(TenantFieldStatus.Active, field.Status);
        Assert.Equal(TenantFieldOwnerScope.Tenant, field.OwnerScope);
        Assert.Equal(1, field.RowVersion);
    }

    [Theory]
    [InlineData("Budget")]
    [InlineData("1budget")]
    [InlineData("b")]
    [InlineData("lead-source")]
    [InlineData("")]
    public void An_invalid_key_is_rejected(string key) =>
        Assert.Throws<ArgumentException>(() => TenantFieldDefinition.Create(Tenant, TenantFieldAggregateType.Opportunity, key, "Budget", TenantFieldValueType.Text));

    [Fact]
    public void A_label_is_required_and_trimmed()
    {
        Assert.Throws<ArgumentException>(() => TenantFieldDefinition.Create(Tenant, TenantFieldAggregateType.Opportunity, "budget", "  ", TenantFieldValueType.Text));
        Assert.Equal("Budget", TenantFieldDefinition.Create(Tenant, TenantFieldAggregateType.Opportunity, "budget", "  Budget ", TenantFieldValueType.Text).Label);
    }

    [Fact]
    public void A_select_field_needs_valid_unique_options_with_at_least_one_active()
    {
        Assert.Throws<ArgumentException>(() => Create(TenantFieldValueType.Select, TenantFieldConfig.Empty));
        Assert.Throws<ArgumentException>(() => Create(TenantFieldValueType.Select, Options(O("web", "Web"), O("web", "Web 2"))));
        Assert.Throws<ArgumentException>(() => Create(TenantFieldValueType.Select, Options(O("Web", "Web"))));
        Assert.Throws<ArgumentException>(() => Create(TenantFieldValueType.Select, Options(O("web", "Web", isDeprecated: true))));

        var field = Create(TenantFieldValueType.MultiSelect, Options(O("web", "Web"), O("fair", "Fair")));
        Assert.Equal(2, field.Config.Options!.Count);
    }

    [Fact]
    public void Config_members_are_only_allowed_for_types_that_use_them()
    {
        Assert.Throws<ArgumentException>(() => Create(TenantFieldValueType.Text, Options(O("a1", "A"))));
        Assert.Throws<ArgumentException>(() => Create(TenantFieldValueType.Text, new(Scale: 2)));
        Assert.Throws<ArgumentException>(() => Create(TenantFieldValueType.Boolean, new(Min: 1)));
        Assert.Throws<ArgumentException>(() => Create(TenantFieldValueType.Number, new(MaxLength: 10)));
        Assert.Throws<ArgumentException>(() => Create(TenantFieldValueType.Number, new(Min: 5, Max: 1)));
        Assert.Throws<ArgumentException>(() => Create(TenantFieldValueType.Number, new(Min: 1.5m)));
        Assert.Throws<ArgumentException>(() => Create(TenantFieldValueType.Text, new(MaxLength: 2001)));
        Assert.Throws<ArgumentException>(() => Create(TenantFieldValueType.Decimal, new(Scale: 7)));
    }

    [Fact]
    public void A_decimal_field_defaults_to_two_decimal_places()
    {
        var field = Create(TenantFieldValueType.Decimal, TenantFieldConfig.Empty);

        Assert.Equal(2, field.Config.Scale);
    }

    [Fact]
    public void Update_changes_label_required_config_and_order_and_bumps_the_version()
    {
        var field = Create(TenantFieldValueType.Select, Options(O("web", "Web")));

        field.Update("Channel", isRequired: true, Options(O("web", "Website"), O("fair", "Fair")), sortOrder: 5);

        Assert.Equal("Channel", field.Label);
        Assert.True(field.IsRequired);
        Assert.Equal(5, field.SortOrder);
        Assert.Equal("Website", field.Config.Options!.Single(o => o.Key == "web").Label);
        Assert.Equal(2, field.RowVersion);
    }

    [Fact]
    public void Update_cannot_remove_an_option_but_can_deprecate_it()
    {
        var field = Create(TenantFieldValueType.Select, Options(O("web", "Web"), O("fair", "Fair")));

        Assert.Throws<ArgumentException>(() => field.Update("Source", false, Options(O("web", "Web")), 0));

        field.Update("Source", false, Options(O("web", "Web"), O("fair", "Fair", isDeprecated: true)), 0);
        Assert.True(field.Config.Options!.Single(o => o.Key == "fair").IsDeprecated);
    }

    [Fact]
    public void Update_cannot_change_a_decimal_scale()
    {
        var field = Create(TenantFieldValueType.Decimal, new(Scale: 2));

        Assert.Throws<ArgumentException>(() => field.Update("Rate", false, new(Scale: 3), 0));
    }

    [Fact]
    public void Deprecate_and_reactivate_are_explicit_transitions()
    {
        var field = Create(TenantFieldValueType.Text, TenantFieldConfig.Empty);

        field.Deprecate();
        Assert.Equal(TenantFieldStatus.Deprecated, field.Status);
        Assert.Throws<InvalidOperationException>(field.Deprecate);

        field.Reactivate();
        Assert.Equal(TenantFieldStatus.Active, field.Status);
        Assert.Throws<InvalidOperationException>(field.Reactivate);
        Assert.Equal(3, field.RowVersion);
    }

    private static TenantFieldDefinition Create(TenantFieldValueType type, TenantFieldConfig config) =>
        TenantFieldDefinition.Create(Tenant, TenantFieldAggregateType.Opportunity, "field_a", "Field A", type, config: config);
}
