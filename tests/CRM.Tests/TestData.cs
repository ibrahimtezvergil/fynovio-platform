using Contracts;
using CRM.Domain;
using CRM.Persistence;
using MasterData.Domain;
using MasterData.Persistence;

namespace CRM.Tests;

/// <summary>Her test kendi tenant'ıyla çalışır; testler arasında veri sızmasın diye
/// tenant id'leri artan bir sayaçtan gelir (AGENTS.md: testlerde sabit id yok).</summary>
public static class TestData
{
    private static long _nextTenantId = 1000;

    public static TenantId NextTenant() => new(Interlocked.Increment(ref _nextTenantId));

    public static PrincipalRef Seller { get; } = new("https://idp.local", "seller-1");

    public static EntityRef ProductRef(TenantId tenantId, long productId = 1) =>
        new(tenantId, "masterdata", "product", productId);

    /// <summary>Seeds a Party to the MasterData schema and returns a PartyRef pointing to it.
    /// The party is created as an Organization type. Used for CRM tests that need to
    /// reference a party before creating an Opportunity.</summary>
    public static async Task<PartyRef> CreatePartyAsync(
        MasterDataDbContext context,
        TenantId tenantId,
        string name,
        string? surname = null,
        string? phone = null,
        string? email = null)
    {
        var party = Party.Create(tenantId, PartyType.Organization, name, surname, phone, email);
        context.Parties.Add(party);
        await context.SaveChangesAsync();
        return new PartyRef(tenantId, party.Id);
    }

    /// <summary>Seeds a minimal published pipeline (one entry stage) so tests can open an
    /// opportunity with a valid stage, per `ck_opportunities_stage_required_once_open`
    /// (docs/plans/2026-09-27 Task 19). Three `SaveChangesAsync` calls are required because
    /// each aggregate's identity must be allocated (via the DB) before the next step can
    /// reference it — `PipelineDefinitionVersion.AddStage` stamps the stage with the
    /// version's `Id`, which only exists once the version itself has been saved.</summary>
    public static async Task<(long PipelineDefinitionVersionId, long PipelineStageId)> CreatePublishedPipelineWithEntryStageAsync(
        CrmDbContext context, TenantId tenantId)
    {
        // Name must be unique per (tenant_id, name) — a Guid suffix lets a test seed more
        // than one pipeline for the same tenant (e.g. two calls with the same TenantId).
        var definition = PipelineDefinition.Create(tenantId, $"Test Pipeline {Guid.NewGuid():N}");
        context.PipelineDefinitions.Add(definition);
        await context.SaveChangesAsync();

        var version = definition.AddVersion(1);
        context.PipelineDefinitionVersions.Add(version);
        await context.SaveChangesAsync();

        var entryStage = version.AddStage("Open", 10);
        version.Publish();
        context.PipelineStages.Add(entryStage);
        await context.SaveChangesAsync();

        return (version.Id, entryStage.Id);
    }
}
