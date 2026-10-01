using System.Text.Json;
using Contracts;
using CRM.Customization;
using Xunit;

namespace CRM.Tests.Domain;

public sealed class CustomFieldFilterTests
{
    private static readonly TenantId Tenant = new(1);

    private static FieldDefinition Select(string key, bool multi = false) =>
        TestFields.Create(Tenant, key, key,
            multi ? FieldType.MultiSelect : FieldType.Select,
            config: new FieldConfig(Options: [new FieldOption("a", "A"), new FieldOption("old", "Old", IsDeprecated: true)]));

    [Fact]
    public void Builds_one_containment_document_with_the_stored_shape_of_each_type()
    {
        var vip = TestFields.Create(Tenant, "vip", "VIP", FieldType.Boolean);
        var json = CustomFieldFilter.ToContainmentJson([Select("region"), Select("tags", multi: true), vip],
            new Dictionary<string, string> { ["region"] = "a", ["tags"] = "a", ["vip"] = "true" });

        using var document = JsonDocument.Parse(json!);
        Assert.Equal("a", document.RootElement.GetProperty("region").GetString());
        Assert.Equal("a", document.RootElement.GetProperty("tags")[0].GetString());
        Assert.True(document.RootElement.GetProperty("vip").GetBoolean());
    }

    [Fact]
    public void Deprecated_fields_and_options_stay_filterable()
    {
        var region = Select("region") with { Status = FieldStatus.Deprecated };

        Assert.NotNull(CustomFieldFilter.ToContainmentJson([region], new Dictionary<string, string> { ["region"] = "old" }));
    }

    [Fact]
    public void No_filters_is_no_predicate_and_more_than_the_limit_is_refused()
    {
        Assert.Null(CustomFieldFilter.ToContainmentJson([], null));
        Assert.Null(CustomFieldFilter.ToContainmentJson([], new Dictionary<string, string>()));

        var many = Enumerable.Range(0, CustomFieldFilter.MaxFilters + 1).ToDictionary(i => $"f{i}", _ => "a");
        var exception = Assert.Throws<CustomFieldValidationException>(() => CustomFieldFilter.ToContainmentJson([], many));
        Assert.Equal("too_many_filters", Assert.Single(exception.Errors).Code);
    }

    [Fact]
    public void A_boolean_filter_takes_only_true_or_false()
    {
        var vip = TestFields.Create(Tenant, "vip", "VIP", FieldType.Boolean);
        var exception = Assert.Throws<CustomFieldValidationException>(() =>
            CustomFieldFilter.ToContainmentJson([vip], new Dictionary<string, string> { ["vip"] = "yes" }));
        Assert.Equal("invalid_value", Assert.Single(exception.Errors).Code);
    }
}
