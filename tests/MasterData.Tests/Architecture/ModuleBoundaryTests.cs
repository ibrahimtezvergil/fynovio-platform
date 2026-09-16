using System.Reflection;
using MasterData.Domain;
using NetArchTest.Rules;
using Xunit;

namespace MasterData.Tests.Architecture;

/// <summary>Symmetric to tests/CRM.Tests/Architecture/ModuleBoundaryTests.cs — the
/// boundary rule cuts both ways. CRM's own test already forbids depending on
/// MasterData; this is MasterData forbidding the reverse. Anchored on Party rather than
/// MasterDataDbContext (as the execution plan originally wrote it): the DbContext
/// doesn't exist until Task 5, and Types.InAssembly works on the whole assembly
/// regardless of which type in it is used to obtain it.</summary>
public sealed class ModuleBoundaryTests
{
    private static readonly Assembly MasterDataAssembly = typeof(Party).Assembly;

    [Fact]
    public void MasterData_does_not_depend_on_another_module()
    {
        var result = Types.InAssembly(MasterDataAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("CRM", "Access", "Organization", "TenantLifecycle", "Sales")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void MasterData_domain_does_not_depend_on_persistence()
    {
        var result = Types.InAssembly(MasterDataAssembly)
            .That().ResideInNamespace("MasterData.Domain")
            .ShouldNot().HaveDependencyOn("MasterData.Persistence")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.FailingTypeNames is null ? "OK" : string.Join(", ", result.FailingTypeNames);
}
