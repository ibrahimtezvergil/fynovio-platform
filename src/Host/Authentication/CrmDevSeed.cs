using Access.Domain.Authorization;
using Access.Persistence;
using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Host.Authentication;

/// <summary>Development-only CRM data for the seeded identities. Without it no seeded user holds any
/// `crm.*` grant (the tenant bootstrap grants only Access's own catalog) and no tenant has a pipeline,
/// so the Opportunity API cannot be exercised from a browser. Called by <see cref="DevSeeder"/>, which
/// already refuses to run outside Development. Idempotent: each piece is skipped once it exists.
/// The production grant path is a separate, undecided question (Phase 2.5B plan, OD2).</summary>
public static class CrmDevSeed
{
    public const string ManagerRoleKey = "crm_manager";
    public const string ViewerRoleKey = "crm_viewer";
    public const string PipelineName = "Sales pipeline";

    private const string SeedReason = "Development seed";

    /// <summary>Stage names in sort order. The first is the entry stage; the last is retired so the
    /// UI is exercised against a configured-but-inactive stage that must never be offered.</summary>
    public static readonly IReadOnlyList<string> ActiveStageNames = ["Qualification", "Proposal", "Negotiation"];
    public const string RetiredStageName = "Legacy stage";

    public static async Task EnsureRolesAsync(
        AccessDbContext context,
        TenantId tenantId,
        long grantorAccountId,
        long managerAccountId,
        long? viewerAccountId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(tenantId, cancellationToken);

        var revisionChanged = false;

        var managerKeys = CrmActionCatalog.All.Select(a => a.ActionKey).ToList();
        revisionChanged |= await EnsureRoleAsync(context, tenantId, ManagerRoleKey, "CRM Manager", managerKeys, managerAccountId, grantorAccountId, cancellationToken);

        if (viewerAccountId is { } viewer)
        {
            string[] viewerKeys = ["crm.opportunity.read", "crm.opportunity.list"];
            revisionChanged |= await EnsureRoleAsync(context, tenantId, ViewerRoleKey, "CRM Viewer", viewerKeys, viewer, grantorAccountId, cancellationToken);
        }

        if (revisionChanged)
        {
            var state = await context.TenantAccessStates.SingleAsync(s => s.TenantId == tenantId, cancellationToken);
            state.BumpRevision();
            await context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<bool> EnsureRoleAsync(
        AccessDbContext context,
        TenantId tenantId,
        string roleKey,
        string roleName,
        IReadOnlyList<string> actionKeys,
        long assigneeAccountId,
        long grantorAccountId,
        CancellationToken cancellationToken)
    {
        var role = await context.Roles.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Key == roleKey, cancellationToken);
        var changed = false;

        if (role is null)
        {
            var permissionSet = PermissionSet.Create(tenantId, roleKey, roleName, PermissionSet.OriginTenant);
            foreach (var actionKey in actionKeys)
                permissionSet.Grant(actionKey);
            context.PermissionSets.Add(permissionSet);

            role = Role.Create(tenantId, roleKey, roleName, Role.OriginTenant);
            context.Roles.Add(role);
            await context.SaveChangesAsync(cancellationToken); // assigns ids for the join row

            context.RolePermissionSets.Add(RolePermissionSet.Create(tenantId, role.Id, permissionSet.Id));
            changed = true;
        }

        var alreadyAssigned = await context.RoleAssignments.AnyAsync(
            a => a.TenantId == tenantId && a.RoleId == role.Id && a.AccountId == assigneeAccountId, cancellationToken);
        if (!alreadyAssigned)
        {
            context.RoleAssignments.Add(RoleAssignment.Grant(
                tenantId, assigneeAccountId, role.Id, grantorAccountId, RoleAssignment.SourceManual, SeedReason));
            changed = true;
        }

        if (changed)
            await context.SaveChangesAsync(cancellationToken);

        return changed;
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
