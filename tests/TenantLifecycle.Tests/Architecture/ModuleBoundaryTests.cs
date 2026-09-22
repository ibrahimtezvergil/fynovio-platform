using NetArchTest.Rules;
using TenantLifecycle.Persistence;

namespace TenantLifecycle.Tests.Architecture;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void TenantLifecycle_does_not_depend_on_other_modules()
    {
        var result = Types.InAssembly(typeof(TenantLifecycleDbContext).Assembly)
            .ShouldNot().HaveDependencyOnAny("Access", "CRM", "Collaboration", "MasterData", "Organization").GetResult();

        Assert.True(result.IsSuccessful, result.FailingTypeNames is null ? "OK" : string.Join(", ", result.FailingTypeNames));
    }
}
