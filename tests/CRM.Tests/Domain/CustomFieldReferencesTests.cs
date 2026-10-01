using System.Text.Json;
using Contracts;
using CRM.Customization;
using Xunit;

namespace CRM.Tests.Domain;

public sealed class CustomFieldReferencesTests
{
    private static readonly TenantId Tenant = new(1);
    private static readonly ActorContext Actor = new(Tenant, new PrincipalRef("https://idp.local", "seller-1"), Guid.NewGuid());
    private static readonly FieldTarget Party = new("masterdata", "party");

    private static FieldDefinition Reference(string key = "account", bool deprecated = false) =>
        TestFields.Create(Tenant, key, key, FieldType.Reference, config: new FieldConfig(Target: Party)) with { Status = deprecated ? FieldStatus.Deprecated : FieldStatus.Active };

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Theory]
    [InlineData("""{"account": 42}""", """{"account":42}""")]
    public void A_positive_whole_number_is_the_canonical_value(string input, string expected) =>
        Assert.Equal(expected, CustomFieldValues.Normalize([Reference()], Json(input), null));

    [Theory]
    [InlineData("""{"account": "42"}""")]
    [InlineData("""{"account": 0}""")]
    [InlineData("""{"account": -3}""")]
    [InlineData("""{"account": 4.5}""")]
    [InlineData("""{"account": true}""")]
    [InlineData("""{"account": {"id": 4}}""")]
    [InlineData("""{"account": [4]}""")]
    public void Anything_else_is_the_wrong_shape(string input)
    {
        var exception = Assert.Throws<CustomFieldValidationException>(() => CustomFieldValues.Normalize([Reference()], Json(input), null));

        Assert.Equal("invalid_type", Assert.Single(exception.Errors).Code);
    }

    [Fact]
    public void A_reference_cannot_be_filtered_on()
    {
        Assert.False(CustomFieldFilter.IsFilterable(FieldType.Reference));
        var exception = Assert.Throws<CustomFieldValidationException>(() =>
            CustomFieldFilter.ToContainmentJson([Reference()], new Dictionary<string, string> { ["account"] = "42" }));
        Assert.Equal("not_filterable", Assert.Single(exception.Errors).Code);
    }

    [Fact]
    public void Only_new_or_different_references_count_as_changed()
    {
        var definitions = new[] { Reference("account"), Reference("partner"), Reference("legacy", deprecated: true) };

        var changed = CustomFieldReferences.Changed(definitions, """{"account":1,"partner":2,"legacy":9}""", """{"account":1,"partner":3,"legacy":9}""");

        Assert.Equal(new Dictionary<string, long> { ["partner"] = 3 }, changed);
        Assert.Equal(new Dictionary<string, long> { ["account"] = 1, ["partner"] = 2 }, CustomFieldReferences.Changed(definitions, null, """{"account":1,"partner":2}"""));
        Assert.Empty(CustomFieldReferences.Changed(definitions, """{"account":1}""", """{}"""));
    }

    [Fact]
    public async Task An_accessible_reference_is_accepted_and_asked_for_in_one_call()
    {
        var directory = StubLinkTargetDirectory.EveryPartyAccessible;

        await CustomFieldReferences.VerifyAsync(directory, Actor, [Reference("account"), Reference("partner")], null, """{"account":5,"partner":6}""", default);

        var call = Assert.Single(directory.Calls);
        Assert.Equal(["masterdata/party/5@1", "masterdata/party/6@1"], call.Select(r => r.ToString()).Order());
    }

    [Fact]
    public async Task An_unavailable_reference_is_refused_per_field_and_reveals_nothing_about_why()
    {
        var directory = new StubLinkTargetDirectory(r => r.Id == 5 ? new LinkTargetResolution.Accessible("Acme") : LinkTargetResolution.Unavailable.Instance);

        var exception = await Assert.ThrowsAsync<CustomFieldValidationException>(() =>
            CustomFieldReferences.VerifyAsync(directory, Actor, [Reference("account"), Reference("partner")], null, """{"account":5,"partner":6}""", default));

        var error = Assert.Single(exception.Errors);
        Assert.Equal(("partner", "invalid_reference"), (error.Field, error.Code));
    }

    [Fact]
    public async Task An_unchanged_reference_is_not_asked_about_even_when_it_is_no_longer_visible()
    {
        var directory = StubLinkTargetDirectory.None;   // everything unavailable

        await CustomFieldReferences.VerifyAsync(directory, Actor, [Reference()], """{"account":5}""", """{"account":5}""", default);

        Assert.Empty(directory.Calls);
    }

    [Fact]
    public async Task Hydration_asks_once_for_many_records_and_never_leaks_a_label_that_is_not_available()
    {
        var directory = new StubLinkTargetDirectory(r => r.Id == 5 ? new LinkTargetResolution.Accessible("Acme") : LinkTargetResolution.Unavailable.Instance);

        var hydrated = await CustomFieldReferences.HydrateAsync(directory, Actor, [Reference("account"), Reference("legacy", deprecated: true)],
            [(10, """{"account":5}"""), (11, """{"account":6,"legacy":5}"""), (12, null), (13, """{"other":1}""")], default);

        Assert.Single(directory.Calls);
        Assert.Equal(new CustomFieldReferenceDto(5, true, "Acme"), hydrated[10]["account"]);
        Assert.Equal(new CustomFieldReferenceDto(6, false, null), hydrated[11]["account"]);
        Assert.Equal(new CustomFieldReferenceDto(5, true, "Acme"), hydrated[11]["legacy"]);
        Assert.False(hydrated.ContainsKey(12));
        Assert.False(hydrated.ContainsKey(13));
    }

    [Fact]
    public async Task Hydration_makes_no_call_when_there_is_nothing_to_resolve()
    {
        var directory = StubLinkTargetDirectory.EveryPartyAccessible;

        Assert.Empty(await CustomFieldReferences.HydrateAsync(directory, Actor, [Reference()], [(1, """{"x":1}""")], default));
        Assert.Empty(await CustomFieldReferences.HydrateAsync(directory, Actor, [TestFields.Create(Tenant, "note", "Note", FieldType.Text)], [(1, """{"note":"x"}""")], default));
        Assert.Empty(directory.Calls);
    }
}
