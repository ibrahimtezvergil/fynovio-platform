using Access.Domain.Authorization;
using Access.Persistence;
using Contracts;
using CRM.Domain;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Host.Authentication;

/// <summary>Development-only CRM data that has no production path yet: extra role assignments for the seeded
/// identities and the sales pipeline. The CRM ROLES themselves are no longer seeded here — the dev seed
/// enables the module through the same `EnableTenantModuleHandler` an operator uses in production
/// (`enable-tenant-module`), so dev and production share one grant mechanism. A production tenant still has
/// no pipeline provisioning (Phase 2.6 plan, P1). Called by <see cref="DevSeeder"/>, which already refuses to
/// run outside Development. Idempotent: each piece is skipped once it exists.</summary>
public static class CrmDevSeed
{
    public const string PipelineName = "Sales pipeline";

    private const string SeedReason = "Development seed";

    /// <summary>Stage names in sort order. The first is the entry stage; the last is retired so the
    /// UI is exercised against a configured-but-inactive stage that must never be offered.</summary>
    public static readonly IReadOnlyList<string> ActiveStageNames = ["Qualification", "Proposal", "Negotiation"];
    public const string RetiredStageName = "Legacy stage";

    /// <summary>Assigns an already-enabled template role to a dev identity. Uses the same `manual` assignment
    /// source and revision bump as `GrantRoleAssignmentHandler`; skipped when the assignment exists.</summary>
    public static async Task EnsureAssignmentAsync(
        AccessDbContext context,
        TenantId tenantId,
        string roleKey,
        long assigneeAccountId,
        long grantorAccountId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(tenantId, cancellationToken);

        var role = await context.Roles.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Key == roleKey, cancellationToken);
        if (role is null)
            return; // module not enabled for this tenant (e.g. legacy dev database) — the caller already warned

        var alreadyAssigned = await context.RoleAssignments.AnyAsync(
            a => a.TenantId == tenantId && a.RoleId == role.Id && a.AccountId == assigneeAccountId, cancellationToken);
        if (alreadyAssigned)
            return;

        context.RoleAssignments.Add(RoleAssignment.Grant(
            tenantId, assigneeAccountId, role.Id, grantorAccountId, RoleAssignment.SourceManual, SeedReason));
        var state = await context.TenantAccessStates.SingleAsync(s => s.TenantId == tenantId, cancellationToken);
        state.BumpRevision();
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public static async Task EnsurePipelineAsync(CrmDbContext context, TenantId tenantId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(tenantId, cancellationToken);

        if (await context.PipelineDefinitions.AnyAsync(p => p.TenantId == tenantId, cancellationToken))
            return;

        var definition = PipelineDefinition.Create(tenantId, PipelineName);
        context.PipelineDefinitions.Add(definition);
        await context.SaveChangesAsync(cancellationToken); // assigns definition.Id

        var version = definition.AddVersion(1);
        context.PipelineDefinitionVersions.Add(version);
        await context.SaveChangesAsync(cancellationToken); // assigns version.Id

        var sortOrder = 10;
        foreach (var name in ActiveStageNames)
        {
            var stage = version.AddStage(name, sortOrder);
            context.PipelineStages.Add(stage);
            sortOrder += 10;
        }

        var retired = version.AddStage(RetiredStageName, sortOrder);
        retired.Deactivate();
        context.PipelineStages.Add(retired);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
