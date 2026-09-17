using System.Reflection;
using Access.Persistence;
using NetArchTest.Rules;

namespace Access.Tests.Architecture;

/// <summary>FF01 (doc 12), symmetric to CRM.Tests' ModuleBoundaryTests: Access may
/// depend on Contracts only.</summary>
public sealed class ModuleBoundaryTests
{
    private static readonly Assembly AccessAssembly = typeof(AccessDbContext).Assembly;

    [Fact]
    public void Access_does_not_depend_on_another_module()
    {
        var result = Types.InAssembly(AccessAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("CRM", "MasterData", "Organization", "TenantLifecycle")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Access_domain_does_not_depend_on_persistence()
    {
        var result = Types.InAssembly(AccessAssembly)
            .That().ResideInNamespace("Access.Domain")
            .ShouldNot().HaveDependencyOn("Access.Persistence")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.FailingTypeNames is null ? "OK" : string.Join(", ", result.FailingTypeNames);
}
