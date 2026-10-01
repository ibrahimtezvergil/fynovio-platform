using SemanticCatalog.Persistence;
using NetArchTest.Rules;

namespace SemanticCatalog.Tests.Architecture;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void The_catalog_depends_on_Contracts_only()
    {
        var result = Types.InAssembly(typeof(SemanticCatalogDbContext).Assembly)
            .ShouldNot().HaveDependencyOnAny("Access", "CRM", "MasterData", "Organization", "TenantLifecycle", "Collaboration", "Messaging")
            .GetResult();
        Assert.True(result.IsSuccessful, result.FailingTypeNames is null ? "OK" : string.Join(", ", result.FailingTypeNames));
    }

    [Fact]
    public void The_catalog_domain_does_not_depend_on_persistence()
    {
        var result = Types.InAssembly(typeof(SemanticCatalogDbContext).Assembly)
            .That().ResideInNamespace("SemanticCatalog.Domain")
            .ShouldNot().HaveDependencyOn("SemanticCatalog.Persistence")
            .GetResult();
        Assert.True(result.IsSuccessful, result.FailingTypeNames is null ? "OK" : string.Join(", ", result.FailingTypeNames));
    }
}
