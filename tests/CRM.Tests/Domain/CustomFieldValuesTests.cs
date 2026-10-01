using System.Text.Json;
using CRM.Customization;

namespace CRM.Tests.Domain;

public sealed class CustomFieldValuesTests
{
    private static readonly Contracts.TenantId Tenant = new(1);

    private static TenantFieldDefinition Field(string key, TenantFieldValueType type, bool required = false, TenantFieldConfig? config = null, int sortOrder = 0) =>
        TenantFieldDefinition.Create(Tenant, TenantFieldAggregateType.Opportunity, key, key, type, required, config, sortOrder);

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static CustomFieldValidationException Invalid(IReadOnlyCollection<TenantFieldDefinition> definitions, string json, string? existing = null) =>
        Assert.Throws<CustomFieldValidationException>(() => CustomFieldValues.Normalize(definitions, Json(json), existing));

    [Fact]
    public void Every_type_accepts_its_canonical_value()
    {
        var definitions = new[]
        {
            Field("note", TenantFieldValueType.Text),
            Field("story", TenantFieldValueType.LongText),
            Field("seats", TenantFieldValueType.Number, config: new(Min: 1, Max: 500)),
            Field("rate", TenantFieldValueType.Decimal, config: new(Scale: 2)),
            Field("vip", TenantFieldValueType.Boolean),
            Field("visit", TenantFieldValueType.Date),
            Field("source", TenantFieldValueType.Select, config: new(Options: [new("web", "Web"), new("fair", "Fair")])),
            Field("tags", TenantFieldValueType.MultiSelect, config: new(Options: [new("a1", "A"), new("b1", "B")])),
            Field("contact", TenantFieldValueType.Email),
            Field("mobile", TenantFieldValueType.Phone),
            Field("site", TenantFieldValueType.Url)
        };

        var result = CustomFieldValues.Normalize(definitions, Json("""
            {"note":"hi","story":"long","seats":12,"rate":10.25,"vip":true,"visit":"2026-10-01","source":"web",
             "tags":["a1","b1"],"contact":"a@b.co","mobile":"+90 532 000 00 00","site":"https://fynovio.com"}
            """), null);

        var values = CustomFieldValues.Parse(result);
        Assert.Equal(11, values.Count);
        Assert.Equal(12, values["seats"].GetInt64());
        Assert.Equal(10.25m, values["rate"].GetDecimal());
        Assert.Equal(2, values["tags"].GetArrayLength());
    }

    [Theory]
    [InlineData(TenantFieldValueType.Text, "12")]
    [InlineData(TenantFieldValueType.Number, "\"12\"")]
    [InlineData(TenantFieldValueType.Number, "1.5")]
    [InlineData(TenantFieldValueType.Boolean, "\"yes\"")]
    [InlineData(TenantFieldValueType.Date, "\"01.10.2026\"")]
    [InlineData(TenantFieldValueType.Date, "\"2026-02-30\"")]
    [InlineData(TenantFieldValueType.MultiSelect, "\"a1\"")]
    public void A_value_of_the_wrong_shape_is_rejected(TenantFieldValueType type, string value)
    {
        var config = type == TenantFieldValueType.MultiSelect ? new TenantFieldConfig(Options: [new("a1", "A")]) : null;
        var error = Invalid([Field("field_a", type, config: config)], $$"""{"field_a":{{value}}}""");

        Assert.Equal("invalid_type", Assert.Single(error.Errors).Code);
    }

    [Theory]
    [InlineData(TenantFieldValueType.Email, "\"not-an-email\"")]
    [InlineData(TenantFieldValueType.Phone, "\"call me\"")]
    [InlineData(TenantFieldValueType.Url, "\"ftp://files.example\"")]
    [InlineData(TenantFieldValueType.Url, "\"javascript:alert(1)\"")]
    public void A_malformed_formatted_value_is_rejected(TenantFieldValueType type, string value) =>
        Assert.Equal("invalid_value", Assert.Single(Invalid([Field("field_a", type)], $$"""{"field_a":{{value}}}""").Errors).Code);

    [Fact]
    public void Limits_are_enforced()
    {
        Assert.Equal("out_of_range", Assert.Single(Invalid([Field("seats", TenantFieldValueType.Number, config: new(Max: 10))], """{"seats":11}""").Errors).Code);
        Assert.Equal("invalid_value", Assert.Single(Invalid([Field("rate", TenantFieldValueType.Decimal, config: new(Scale: 1))], """{"rate":1.25}""").Errors).Code);
        Assert.Equal("too_long", Assert.Single(Invalid([Field("note", TenantFieldValueType.Text, config: new(MaxLength: 3))], """{"note":"abcd"}""").Errors).Code);
    }

    [Fact]
    public void Unknown_and_invalid_option_keys_are_rejected()
    {
        var source = Field("source", TenantFieldValueType.Select, config: new(Options: [new("web", "Web")]));

        Assert.Equal("unknown_field", Assert.Single(Invalid([source], """{"nope":"x"}""").Errors).Code);
        Assert.Equal("invalid_option", Assert.Single(Invalid([source], """{"source":"tv"}""").Errors).Code);
    }

    [Fact]
    public void All_errors_are_reported_together()
    {
        var definitions = new[] { Field("a1", TenantFieldValueType.Text, required: true), Field("b1", TenantFieldValueType.Number) };

        var error = Invalid(definitions, """{"b1":"x","c1":1}""");

        Assert.Equal(["a1:required", "b1:invalid_type", "c1:unknown_field"],
            error.Errors.Select(e => $"{e.Field}:{e.Code}").Order().ToArray());
    }

    [Fact]
    public void Required_fields_treat_null_empty_text_and_empty_lists_as_missing()
    {
        var text = Field("note", TenantFieldValueType.Text, required: true);
        var tags = Field("tags", TenantFieldValueType.MultiSelect, required: true, config: new(Options: [new("a1", "A")]));

        Assert.Equal(2, Invalid([text, tags], """{"note":"  ","tags":[]}""").Errors.Count);
        Assert.Equal(2, Invalid([text, tags], """{"note":null}""").Errors.Count);
    }

    [Fact]
    public void No_values_normalize_to_null()
    {
        Assert.Null(CustomFieldValues.Normalize([Field("note", TenantFieldValueType.Text)], null, null));
        Assert.Null(CustomFieldValues.Normalize([Field("note", TenantFieldValueType.Text)], Json("""{"note":""}"""), null));
    }

    [Fact]
    public void A_non_object_payload_is_rejected() =>
        Assert.Equal("$", Assert.Single(Invalid([Field("note", TenantFieldValueType.Text)], "[1]").Errors).Field);

    [Fact]
    public void A_deprecated_field_cannot_be_written_but_its_stored_value_is_carried_forward()
    {
        var legacy = Field("legacy", TenantFieldValueType.Text);
        legacy.Deprecate();
        var note = Field("note", TenantFieldValueType.Text);
        const string stored = """{"legacy":"kept","note":"old"}""";

        Assert.Equal("field_deprecated", Assert.Single(Invalid([legacy, note], """{"legacy":"new"}""", stored).Errors).Code);

        var values = CustomFieldValues.Parse(CustomFieldValues.Normalize([legacy, note], Json("""{"note":"new"}"""), stored));
        Assert.Equal("kept", values["legacy"].GetString());
        Assert.Equal("new", values["note"].GetString());
    }

    [Fact]
    public void A_deprecated_option_is_refused_for_new_writes_but_can_be_kept()
    {
        var source = Field("source", TenantFieldValueType.Select, config: new(Options: [new("web", "Web"), new("fax", "Fax", IsDeprecated: true)]));
        var tags = Field("tags", TenantFieldValueType.MultiSelect, config: new(Options: [new("a1", "A"), new("old", "Old", IsDeprecated: true)]));

        Assert.Equal("option_deprecated", Assert.Single(Invalid([source], """{"source":"fax"}""").Errors).Code);
        Assert.Equal("option_deprecated", Assert.Single(Invalid([tags], """{"tags":["old"]}""", """{"tags":["a1"]}""").Errors).Code);

        Assert.NotNull(CustomFieldValues.Normalize([source], Json("""{"source":"fax"}"""), """{"source":"fax"}"""));
        Assert.NotNull(CustomFieldValues.Normalize([tags], Json("""{"tags":["a1","old"]}"""), """{"tags":["old"]}"""));
    }

    [Fact]
    public void The_payload_is_capped()
    {
        var fields = Enumerable.Range(0, 10).Select(i => Field($"f{i}", TenantFieldValueType.LongText)).ToArray();
        var payload = "{" + string.Join(",", fields.Select(f => $"\"{f.FieldName}\":\"{new string('x', 9000)}\"")) + "}";

        Assert.Equal("payload_too_large", Assert.Single(Invalid(fields, payload).Errors).Code);
    }

    [Fact]
    public void Changed_keys_report_only_differences()
    {
        Assert.Equal(["a1", "c1"], CustomFieldValues.ChangedKeys("""{"a1":1,"b1":2}""", """{"b1":2,"c1":3}"""));
        Assert.Empty(CustomFieldValues.ChangedKeys(null, null));
    }
}
