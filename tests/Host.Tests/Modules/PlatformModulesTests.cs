using Access.Application;
using Access.Persistence;
using Collaboration.Application;
using CRM.Application;
using Host.Modules;
using Host.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Host.Tests.Modules;

/// <summary>`AccessActionCatalogSeeder` deprecates every registered key missing from its manifest, so a module
/// left out of the composed registry would silently lose its actions at the next start-up. These tests make that
/// failure loud.</summary>
[Collection(HostIntegrationCollection.Name)]
public sealed class PlatformModulesTests : IClassFixture<AuthApiFixture>
{
    private readonly AuthApiFixture _fixture;

    public PlatformModulesTests(AuthApiFixture fixture) => _fixture = fixture;

    [Fact]
    public void The_registry_is_the_union_of_access_crm_and_collaboration_with_no_duplicates()
    {
        var keys = PlatformModules.ActionRegistry.Select(d => d.ActionKey).ToList();

        Assert.Equal(keys.Distinct().Count(), keys.Count);
        Assert.Superset(AccessActionCatalog.All.Select(d => d.ActionKey).ToHashSet(), keys.ToHashSet());
        Assert.Superset(CrmActionCatalog.All.Select(d => d.ActionKey).ToHashSet(), keys.ToHashSet());
        Assert.Superset(CollaborationActionCatalog.All.Select(d => d.ActionKey).ToHashSet(), keys.ToHashSet());
        Assert.Equal(AccessActionCatalog.All.Count + CrmActionCatalog.All.Count + CollaborationActionCatalog.All.Count, keys.Count);
        Assert.All(PlatformModules.ActionRegistry.Where(d => d.ActionKey.StartsWith("crm.", StringComparison.Ordinal)), d => Assert.Equal("CRM", d.OwnerModule));
        Assert.All(PlatformModules.ActionRegistry.Where(d => d.ActionKey.StartsWith("collaboration.", StringComparison.Ordinal)), d => Assert.Equal("Collaboration", d.OwnerModule));
    }

    [Fact]
    public void Every_capability_template_only_names_registered_actions_and_the_catalog_accepts_them()
    {
        var registered = PlatformModules.ActionRegistry.Select(d => d.ActionKey).ToHashSet();

        foreach (var manifest in PlatformModules.CapabilityManifests)
            Assert.Subset(registered, manifest.ActionKeys().ToHashSet());

        var catalog = new ModuleCapabilityCatalog(PlatformModules.CapabilityManifests);
        Assert.NotNull(catalog.Find("crm"));
        Assert.NotNull(catalog.Find("collaboration"));
    }

    [Fact]
    public async Task After_start_up_every_registry_key_is_active_in_the_database()
    {
        using var host = await _fixture.StartHostAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var access = scope.ServiceProvider.GetRequiredService<AccessDbContext>();

        var active = await access.Actions.Where(a => !a.IsDeprecated).Select(a => a.ActionKey).ToListAsync();

        Assert.Subset(active.ToHashSet(), PlatformModules.ActionRegistry.Select(d => d.ActionKey).ToHashSet());
    }
}
