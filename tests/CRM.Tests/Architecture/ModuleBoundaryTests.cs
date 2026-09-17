using System.Reflection;
using CRM.Persistence;
using NetArchTest.Rules;
using Xunit;

namespace CRM.Tests.Architecture;

/// <summary>FF01 (doc 12): bir modül yalnızca Contracts'a bağımlı olabilir; modülün
/// Domain namespace'i kendi Persistence katmanına bağımlı olamaz.</summary>
public sealed class ModuleBoundaryTests
{
    private static readonly Assembly CrmAssembly = typeof(CrmDbContext).Assembly;

    [Fact]
    public void Crm_does_not_depend_on_another_module()
    {
        var result = Types.InAssembly(CrmAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Access", "MasterData", "Organization", "TenantLifecycle")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Crm_domain_does_not_depend_on_persistence()
    {
        var result = Types.InAssembly(CrmAssembly)
            .That().ResideInNamespace("CRM.Domain")
            .ShouldNot().HaveDependencyOn("CRM.Persistence")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Crm_does_not_reference_Access()
    {
        var result = Types.InAssembly(CrmAssembly)
            .ShouldNot()
            .HaveDependencyOn("Access")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.FailingTypeNames is null ? "OK" : string.Join(", ", result.FailingTypeNames);
}
