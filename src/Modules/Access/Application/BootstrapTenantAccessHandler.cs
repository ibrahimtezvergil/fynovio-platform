using Access.Domain.Authorization;
using Access.Evidence;
using Access.Outbox;
using Access.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

/// <summary>The one path with no `Authorize()` call, and it must stay that way
/// (gap-closure §6): a default-deny system cannot gate the creation of its own first
/// grant. Still writes evidence + outbox — this is a real mutation, just not
/// authorization-gated (round 3's "no silent DB update" principle still applies).</summary>
public sealed class BootstrapTenantAccessHandler(AccessDbContext context)
{
    internal const string TenantAdministratorRoleKey = "tenant_administrator";
    internal const string SupportRoleKey = "support";
    private const string TenantAdministratorPermissionSetKey = "tenant_administration";
    private const string AccessTemplateModuleKey = "access";
    private const int AccessTemplateVersion = 1;

    public async Task HandleAsync(BootstrapTenantAccessCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        if (await context.TenantAccessStates.AnyAsync(s => s.TenantId == command.TenantId, cancellationToken))
            throw new InvalidOperationException($"Tenant {command.TenantId} has already been bootstrapped.");

        var accountId = await context.ExternalIdentities
            .Where(e => e.Issuer == command.TenantAdministrator.Issuer && e.Subject == command.TenantAdministrator.Subject)
            .Select(e => (long?)e.AccountId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Tenant administrator principal must have a linked Account/ExternalIdentity before bootstrap.");

        var permissionSet = PermissionSet.Create(
            command.TenantId, TenantAdministratorPermissionSetKey, "Sistem Yönetimi", PermissionSet.OriginSystemTemplate,
            AccessTemplateModuleKey, AccessTemplateVersion);
        // The bootstrap administrator is the tenant's break-glass operator. Grant every
        // active platform action already registered by Host, rather than a partial,
        // hard-coded Access-only catalog.
        var actionKeys = await context.Actions
            .Where(action => !action.IsDeprecated)
            .OrderBy(action => action.ActionKey)
            .Select(action => action.ActionKey)
            .ToListAsync(cancellationToken);
        foreach (var actionKey in actionKeys)
            permissionSet.Grant(actionKey);
        context.PermissionSets.Add(permissionSet);

        var role = Role.Create(
            command.TenantId, TenantAdministratorRoleKey, "Sistem Yöneticisi (Admin)", Role.OriginSystemTemplate,
            AccessTemplateModuleKey, AccessTemplateVersion);
        var supportSet = PermissionSet.Create(command.TenantId, SupportRoleKey, "Support", PermissionSet.OriginSystemTemplate,
            AccessTemplateModuleKey, AccessTemplateVersion);
        var supportRole = Role.Create(command.TenantId, SupportRoleKey, "Support", Role.OriginSystemTemplate,
            AccessTemplateModuleKey, AccessTemplateVersion);
        context.PermissionSets.Add(supportSet);
        context.Roles.Add(role);
        context.Roles.Add(supportRole);
        await context.SaveChangesAsync(cancellationToken); // assigns Id to permissionSet/role before the join row

        context.RolePermissionSets.Add(RolePermissionSet.Create(command.TenantId, role.Id, permissionSet.Id));
        context.RolePermissionSets.Add(RolePermissionSet.Create(command.TenantId, supportRole.Id, supportSet.Id));

        var assignment = RoleAssignment.Grant(command.TenantId, accountId, role.Id, accountId, RoleAssignment.SourceBootstrap, reason: "Tenant bootstrap");
        context.RoleAssignments.Add(assignment);

        context.TenantAccessStates.Add(TenantAccessState.Initialize(command.TenantId));

        var detail = $$"""{"role":"{{TenantAdministratorRoleKey}}","accountId":{{accountId}}}""";
        context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId, nameof(RoleAssignment), assignment.Id, assignment.RowVersion,
            command.TenantAdministrator, "AccessBootstrapped", detail, command.CorrelationId));
        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(RoleAssignment), assignment.Id, assignment.RowVersion,
            "enterprise.access.tenant.bootstrapped.v1", "/enterprise/access", $"role-assignments/{assignment.Id}",
            command.CorrelationId, null, detail));

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
