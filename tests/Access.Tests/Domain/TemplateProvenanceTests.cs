using Access.Domain.Authorization;
using Contracts;

namespace Access.Tests.Domain;

public sealed class TemplateProvenanceTests
{
    private static readonly TenantId Tenant = new(1);

    [Fact]
    public void A_template_copy_records_its_module_and_version()
    {
        var role = Role.Create(Tenant, "k", "N", Role.OriginSystemTemplate, "demo", 3);
        var set = PermissionSet.Create(Tenant, "k", "N", PermissionSet.OriginSystemTemplate, "demo", 3);

        Assert.Equal(("demo", 3), (role.OriginModuleKey, role.OriginVersion));
        Assert.Equal(("demo", 3), (set.OriginModuleKey, set.OriginVersion));
    }

    [Fact]
    public void Provenance_is_optional()
    {
        Assert.Null(Role.Create(Tenant, "k", "N").OriginModuleKey);
        Assert.Null(PermissionSet.Create(Tenant, "k", "N", PermissionSet.OriginSystemTemplate).OriginVersion);
    }

    [Theory]
    [InlineData("demo", null)]
    [InlineData(null, 1)]
    [InlineData("demo", 0)]
    [InlineData(" ", 1)]
    public void Provenance_is_all_or_nothing_and_valid(string? moduleKey, int? version)
    {
        Assert.Throws<ArgumentException>(() => Role.Create(Tenant, "k", "N", Role.OriginSystemTemplate, moduleKey, version));
        Assert.Throws<ArgumentException>(() => PermissionSet.Create(Tenant, "k", "N", PermissionSet.OriginSystemTemplate, moduleKey, version));
    }

    [Fact]
    public void A_tenant_authored_row_never_carries_provenance()
    {
        Assert.Throws<ArgumentException>(() => Role.Create(Tenant, "k", "N", Role.OriginTenant, "demo", 1));
        Assert.Throws<ArgumentException>(() => PermissionSet.Create(Tenant, "k", "N", PermissionSet.OriginTenant, "demo", 1));
    }

    [Fact]
    public void The_module_enablement_assignment_source_is_accepted_and_others_are_not()
    {
        var assignment = RoleAssignment.Grant(Tenant, 1, 1, 1, RoleAssignment.SourceModuleEnablement);
        Assert.Equal("module_enablement", assignment.Source);
        Assert.Throws<ArgumentException>(() => RoleAssignment.Grant(Tenant, 1, 1, 1, "bogus"));
    }

    [Fact]
    public void An_enablement_needs_a_module_and_a_positive_version()
    {
        Assert.Throws<ArgumentException>(() => TenantModuleEnablement.Enable(Tenant, " ", 1, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => TenantModuleEnablement.Enable(Tenant, "demo", 0, DateTimeOffset.UtcNow));
    }
}
