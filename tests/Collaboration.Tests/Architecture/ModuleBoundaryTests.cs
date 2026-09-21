using System.Reflection;
using Collaboration.Persistence;
using NetArchTest.Rules;

namespace Collaboration.Tests.Architecture;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void Collaboration_does_not_depend_on_other_modules()
    {
        var result = Types.InAssembly(typeof(CollaborationDbContext).Assembly).ShouldNot().HaveDependencyOnAny("Access", "CRM", "MasterData", "Organization", "TenantLifecycle").GetResult();
        Assert.True(result.IsSuccessful, result.FailingTypeNames is null ? "OK" : string.Join(", ", result.FailingTypeNames));
    }

    [Fact]
    public void Collaboration_Tests_does_not_depend_on_other_module_test_projects()
    {
        // Test project isolation: Collaboration.Tests must not reference other modules' test assemblies.
        // This enforces the principle that a module's tests are independent.
        var testAssembly = typeof(ModuleBoundaryTests).Assembly;
        var result = Types.InAssembly(testAssembly).ShouldNot().HaveDependencyOnAny("Access.Tests", "CRM.Tests", "MasterData.Tests").GetResult();
        Assert.True(result.IsSuccessful, result.FailingTypeNames is null ? "OK" : string.Join(", ", result.FailingTypeNames));
    }
}
