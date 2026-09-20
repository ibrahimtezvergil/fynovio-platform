using Access.Application;
using Contracts;

namespace Access.Tests.Application;

public sealed class ModuleCapabilityManifestTests
{
    private static ModuleCapabilityManifest Valid() => new(
        "demo", "Demo", 1,
        [new PermissionSetTemplate("read", "Read", [new("demo.record.read")])],
        [new RoleTemplate("viewer", "Viewer", ["read"])]);

    [Fact]
    public void A_well_formed_manifest_validates_and_lists_its_action_keys()
    {
        var manifest = Valid();
        manifest.Validate();
        Assert.Equal(["demo.record.read"], manifest.ActionKeys());
    }

    [Theory]
    [InlineData("Demo")]
    [InlineData("1demo")]
    [InlineData("de-mo")]
    [InlineData("")]
    public void A_module_key_must_be_a_lower_case_identifier(string key) =>
        Assert.Throws<InvalidOperationException>(() => (Valid() with { ModuleKey = key }).Validate());

    [Fact]
    public void Version_must_be_positive() =>
        Assert.Throws<InvalidOperationException>(() => (Valid() with { Version = 0 }).Validate());

    [Fact]
    public void Wildcard_action_keys_are_refused() =>
        Assert.Throws<InvalidOperationException>(() => (Valid() with
        {
            PermissionSets = [new PermissionSetTemplate("read", "Read", [new("demo.*")])]
        }).Validate());

    [Fact]
    public void Only_the_owner_relation_is_accepted() =>
        Assert.Throws<InvalidOperationException>(() => (Valid() with
        {
            PermissionSets = [new PermissionSetTemplate("read", "Read", [new("demo.record.read", "team")])]
        }).Validate());

    [Fact]
    public void A_role_cannot_reference_an_unknown_permission_set() =>
        Assert.Throws<InvalidOperationException>(() => (Valid() with
        {
            Roles = [new RoleTemplate("viewer", "Viewer", ["missing"])]
        }).Validate());

    [Fact]
    public void Duplicate_keys_are_refused()
    {
        var set = new PermissionSetTemplate("read", "Read", [new("demo.record.read")]);
        Assert.Throws<InvalidOperationException>(() => (Valid() with { PermissionSets = [set, set] }).Validate());
        Assert.Throws<InvalidOperationException>(() => (Valid() with
        {
            Roles = [new RoleTemplate("viewer", "Viewer", ["read"]), new RoleTemplate("viewer", "Other", ["read"])]
        }).Validate());
        Assert.Throws<InvalidOperationException>(() => (Valid() with
        {
            PermissionSets = [new PermissionSetTemplate("read", "Read", [new("demo.record.read"), new("demo.record.read")])]
        }).Validate());
    }

    [Fact]
    public void The_catalog_validates_every_manifest_and_refuses_a_module_registered_twice()
    {
        Assert.Throws<InvalidOperationException>(() => new ModuleCapabilityCatalog([Valid() with { Version = 0 }]));
        Assert.Throws<InvalidOperationException>(() => new ModuleCapabilityCatalog([Valid(), Valid()]));

        var catalog = new ModuleCapabilityCatalog([Valid()]);
        Assert.NotNull(catalog.Find("demo"));
        Assert.Null(catalog.Find("other"));
    }
}
