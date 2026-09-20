using Access.Domain.Authorization;
using Access.Persistence;
using Contracts;
using CRM.Application;
using Microsoft.EntityFrameworkCore;

namespace Host.Authentication;

/// <summary>Development-only CRM data: extra role assignments for the seeded identities and the sales pipeline.
/// Neither the CRM ROLES nor the pipeline are built here — the dev seed enables the module through the same
/// `EnableTenantModuleHandler` an operator uses in production (`enable-tenant-module`) and provisions the pipeline
/// through the same `ProvisionPipelineHandler` as `provision-crm-pipeline`, so dev and production share one path.
/// Called by <see cref="DevSeeder"/>, which already refuses to run outside Development. Idempotent: each piece is
/// skipped once it exists.</summary>
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

    /// <summary>The pipeline comes from the same handler the `provision-crm-pipeline` operator command uses. The one
    /// dev-only addition is a retired stage, so the UI is exercised against a configured-but-inactive stage.
    /// Idempotent: a tenant that already has a pipeline is left alone.</summary>
    public static Task EnsurePipelineAsync(ProvisionPipelineHandler handler, TenantId tenantId, CancellationToken cancellationToken) =>
        handler.HandleAsync(new ProvisionPipelineCommand(tenantId, PipelineName, ActiveStageNames, [RetiredStageName]), cancellationToken);
}
