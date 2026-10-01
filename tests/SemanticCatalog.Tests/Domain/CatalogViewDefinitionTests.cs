using Contracts;
using SemanticCatalog.Domain;

namespace SemanticCatalog.Tests.Domain;

public sealed class CatalogViewDefinitionTests
{
    private static readonly TenantId Tenant = new(1);

    private static ViewColumn Built(string key) => new(ViewColumn.BuiltIn, key);
    private static ViewColumn Field(string key) => new(ViewColumn.Field, key);

    private static CatalogViewDefinition Create(string key = "pipeline_review", string name = "Pipeline review", params ViewColumn[] columns) =>
        CatalogViewDefinition.Create(Tenant, "crm", "opportunity", key, name, columns.Length == 0 ? [Built("id"), Field("region")] : columns);

    [Fact]
    public void A_view_keeps_its_columns_in_the_order_given_and_starts_active_at_version_one()
    {
        var view = Create(columns: [Field("region"), Built("party"), Built("id")]);

        Assert.Equal(["field:region", "builtin:party", "builtin:id"], view.Columns.Select(c => $"{c.Kind}:{c.Key}"));
        Assert.Equal(FieldStatus.Active, view.Status);
        Assert.Equal(1, view.RowVersion);
        Assert.Equal("table", view.Kind);
        Assert.Equal(["field:region", "builtin:party", "builtin:id"], view.ToReadModel().Columns.Select(c => $"{c.Kind}:{c.Key}"));
    }

    [Theory]
    [InlineData("Review")]
    [InlineData("1review")]
    [InlineData("r")]
    [InlineData("pipeline-review")]
    [InlineData("")]
    public void An_invalid_key_is_refused(string key) => Assert.Throws<ArgumentException>(() => Create(key: key));

    [Fact]
    public void A_name_is_required_trimmed_and_bounded()
    {
        Assert.Equal("Review", Create(name: "  Review ").Name);
        Assert.Throws<ArgumentException>(() => Create(name: "   "));
        Assert.Throws<ArgumentException>(() => Create(name: new string('x', 101)));
    }

    [Fact]
    public void Columns_are_one_to_twenty_unique_and_known()
    {
        Assert.Throws<ArgumentException>(() => CatalogViewDefinition.Create(Tenant, "crm", "opportunity", "empty_view", "Empty", []));
        Assert.Throws<ArgumentException>(() => Create(columns: [Built("id"), Built("id")]));
        Assert.Throws<ArgumentException>(() => Create(columns: [Built("serviceDuration")]));      // a placeholder column that shows nothing
        Assert.Throws<ArgumentException>(() => Create(columns: [Built("region")]));              // a field is not a built-in column
        Assert.Throws<ArgumentException>(() => Create(columns: [Field("Region")]));
        Assert.Throws<ArgumentException>(() => Create(columns: [new ViewColumn("custom", "x")]));
        // The same key as a built-in and as a field are two different columns.
        Assert.Equal(2, Create(columns: [Built("status"), Field("status")]).Columns.Count);

        var twenty = Enumerable.Range(0, 20).Select(i => Field($"field_{i:00}")).ToArray();
        Assert.Equal(20, Create(columns: twenty).Columns.Count);
        Assert.Throws<ArgumentException>(() => Create(columns: [.. twenty, Built("id")]));
    }

    [Fact]
    public void An_unknown_owner_is_refused()
    {
        Assert.Throws<ArgumentException>(() => CatalogViewDefinition.Create(Tenant, "sales", "opportunity", "v_one", "V", [Built("id")]));
        Assert.Throws<ArgumentException>(() => CatalogViewDefinition.Create(Tenant, "crm", "party", "v_one", "V", [Built("id")]));
    }

    [Fact]
    public void Update_changes_name_columns_and_order_and_bumps_the_version_but_never_the_key()
    {
        var view = Create();

        view.Update("Renamed", [Built("amount")], 5);

        Assert.Equal(("Renamed", 5, 2L, "pipeline_review"), (view.Name, view.SortOrder, view.RowVersion, view.Key));
        Assert.Equal(["amount"], view.Columns.Select(c => c.Key));
        Assert.Throws<ArgumentException>(() => view.Update("Renamed", [], 5));
        Assert.Equal(2, view.RowVersion);   // a refused update changes nothing
    }

    [Fact]
    public void Deprecate_and_reactivate_are_explicit_transitions()
    {
        var view = Create();

        view.Deprecate();
        Assert.Equal(FieldStatus.Deprecated, view.Status);
        Assert.Throws<InvalidOperationException>(view.Deprecate);

        view.Reactivate();
        Assert.True(view.IsActive);
        Assert.Throws<InvalidOperationException>(view.Reactivate);
        Assert.Equal(3, view.RowVersion);
    }

    [Fact]
    public void A_view_item_and_a_field_item_are_told_apart_and_one_set_cannot_change_a_definition_twice()
    {
        var content = new ViewChangeContent("crm", "opportunity", "v_one", "V", [new ViewColumn("builtin", "id")], 0, 1);
        var fieldContent = new FieldChangeContent("crm", "opportunity", "v_one", "V", FieldType.Text, false, null, 0, 0);

        var view = ChangeSetItem.ForView(ChangeOperation.Update, 4, content);
        var field = ChangeSetItem.ForField(ChangeOperation.Update, 4, fieldContent with { ExpectedRowVersion = 1 });

        Assert.Equal("v_one", view.Key);
        Assert.Throws<InvalidOperationException>(() => view.Field);
        Assert.Throws<InvalidOperationException>(() => field.View);
        // Same numeric id, different kind: two different definitions, so one set may change both.
        var set = ChangeSet.Draft(Tenant, new PrincipalRef("https://identity.test", "admin"), 0, [view, field]);
        Assert.Equal(2, set.Items.Count);
        Assert.Throws<ArgumentException>(() => ChangeSet.Draft(Tenant, new PrincipalRef("https://identity.test", "admin"), 0,
            [ChangeSetItem.ForView(ChangeOperation.Update, 4, content), ChangeSetItem.ForView(ChangeOperation.Deprecate, 4, content)]));
    }
}
