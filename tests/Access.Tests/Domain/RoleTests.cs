using Access.Domain.Authorization;
using Contracts;

namespace Access.Tests.Domain;

public sealed class RoleTests
{
    [Fact]
    public void Create_rejects_invalid_origin()
    {
        Assert.Throws<ArgumentException>(() => Role.Create(new TenantId(1), "sales_rep", "Sales Representative", "bogus"));
    }

    [Fact]
    public void Create_sets_tenant_id_not_null()
    {
        var role = Role.Create(new TenantId(1), "sales_rep", "Sales Representative");
        Assert.Equal(new TenantId(1), role.TenantId);
    }

    [Fact]
    public void Create_defaults_to_tenant_origin()
    {
        var role = Role.Create(new TenantId(1), "sales_rep", "Sales Representative");
        Assert.Equal(Role.OriginTenant, role.Origin);
    }
}
