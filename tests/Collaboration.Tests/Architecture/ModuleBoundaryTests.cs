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
}
