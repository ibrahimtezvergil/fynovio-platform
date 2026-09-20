using System.Text.Json;
using Access.Domain.Authorization;
using Access.Domain.Identity;
using Access.Evidence;
using Access.Outbox;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Access.Application;

/// <summary>The one path that gives a tenant a business module's roles: copies the module manifest's CURRENT
/// version into tenant-local `Role`/`PermissionSet` rows (`origin = system_template`, with provenance) and
/// records the enablement. Like <see cref="BootstrapTenantAccessHandler"/> it has no `Authorize()` call — a
/// default-deny system cannot gate the creation of a tenant's first grants — so it is never reachable over HTTP.
///
/// Idempotent by state: a second call for the same (tenant, module) changes nothing and reports
/// `AlreadyEnabled`, even when the manifest has since moved to a newer version. There is deliberately no
/// upgrade path and no reconciler (Phase 1.5 Decision A).</summary>
public sealed class EnableTenantModuleHandler(AccessDbContext context, ModuleCapabilityCatalog catalog, TimeProvider timeProvider)
{
    private const string EventType = "enterprise.access.tenant_module.enabled.v1";
    private const string EventSource = "/enterprise/access";
    private const string UniqueViolationSqlState = "23505";

    public async Task<EnableTenantModuleResult> HandleAsync(EnableTenantModuleCommand command, CancellationToken cancellationToken = default)
    {
        var manifest = catalog.Find(command.ModuleKey);
        if (manifest is null)
            return new EnableTenantModuleResult(EnableTenantModuleStatus.UnknownModule, command.ModuleKey);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        if (!await context.TenantAccessStates.AnyAsync(s => s.TenantId == command.TenantId, cancellationToken))
            return new EnableTenantModuleResult(EnableTenantModuleStatus.TenantNotBootstrapped, manifest.ModuleKey, LatestVersion: manifest.Version);

        var existing = await FindEnablementAsync(command.TenantId, manifest.ModuleKey, cancellationToken);
        if (existing is not null)
            return AlreadyEnabled(manifest, existing);

        await RequireRegisteredActionsAsync(manifest, cancellationToken);

        var conflict = await FindKeyConflictAsync(command.TenantId, manifest, cancellationToken);
        if (conflict is not null)
            return new EnableTenantModuleResult(EnableTenantModuleStatus.TemplateKeyConflict, manifest.ModuleKey, LatestVersion: manifest.Version, Detail: conflict);

        try
        {
            var result = await ProvisionAsync(command, manifest, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: UniqueViolationSqlState })
        {
            // Two enables of one tenant/module raced past the checks above; the unique (tenant, key) indexes let
            // exactly one through. The loser reports what the winner committed.
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            return await ReportWinnerAsync(command.TenantId, manifest, cancellationToken);
        }
    }

    private async Task<EnableTenantModuleResult> ProvisionAsync(
        EnableTenantModuleCommand command, ModuleCapabilityManifest manifest, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var tenantId = command.TenantId;

        var permissionSets = new Dictionary<string, PermissionSet>();
        foreach (var template in manifest.PermissionSets)
        {
            var permissionSet = PermissionSet.Create(
                tenantId, template.Key, template.Name, PermissionSet.OriginSystemTemplate, manifest.ModuleKey, manifest.Version);
            foreach (var item in template.Items)
                permissionSet.Grant(item.ActionKey, item.Relation);
            context.PermissionSets.Add(permissionSet);
            permissionSets[template.Key] = permissionSet;
        }

        var roles = new Dictionary<string, Role>();
        foreach (var template in manifest.Roles)
        {
            var role = Role.Create(tenantId, template.Key, template.Name, Role.OriginSystemTemplate, manifest.ModuleKey, manifest.Version);
            context.Roles.Add(role);
            roles[template.Key] = role;
        }

        await context.SaveChangesAsync(cancellationToken); // assigns ids for the join rows

        foreach (var template in manifest.Roles)
            foreach (var permissionSetKey in template.PermissionSetKeys)
                context.RolePermissionSets.Add(RolePermissionSet.Create(tenantId, roles[template.Key].Id, permissionSets[permissionSetKey].Id));

        var administrators = await CurrentTenantAdministratorsAsync(tenantId, now, cancellationToken);
        var granted = 0;
        foreach (var template in manifest.Roles.Where(r => r.GrantToTenantAdministrators))
        {
            foreach (var accountId in administrators)
            {
                context.RoleAssignments.Add(RoleAssignment.Grant(
                    tenantId, accountId, roles[template.Key].Id, accountId, RoleAssignment.SourceModuleEnablement,
                    $"Module enablement: {manifest.ModuleKey} v{manifest.Version}"));
                granted++;
            }
        }

        var enablement = TenantModuleEnablement.Enable(tenantId, manifest.ModuleKey, manifest.Version, now);
        context.TenantModuleEnablements.Add(enablement);

        var state = await context.TenantAccessStates.SingleAsync(s => s.TenantId == tenantId, cancellationToken);
        state.BumpRevision();

        await context.SaveChangesAsync(cancellationToken); // assigns enablement.Id

        var detail = JsonSerializer.Serialize(new
        {
            moduleKey = manifest.ModuleKey,
            version = manifest.Version,
            roles = manifest.Roles.Select(r => r.Key),
            permissionSets = manifest.PermissionSets.Select(s => s.Key),
            administratorAssignments = granted
        });
        context.EvidenceRecords.Add(EvidenceRecord.Create(
            tenantId, nameof(TenantModuleEnablement), enablement.Id, 1, command.Actor, "TenantModule.Enable", detail, command.CorrelationId));
        context.OutboxMessages.Add(OutboxMessage.Create(
            tenantId, nameof(TenantModuleEnablement), enablement.Id, 1, EventType, EventSource,
            $"tenant-modules/{manifest.ModuleKey}", command.CorrelationId, null, detail));
        await context.SaveChangesAsync(cancellationToken);

        return new EnableTenantModuleResult(
            EnableTenantModuleStatus.Enabled, manifest.ModuleKey, manifest.Version, manifest.Version, granted);
    }

    private async Task<EnableTenantModuleResult> ReportWinnerAsync(TenantId tenantId, ModuleCapabilityManifest manifest, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(tenantId, cancellationToken);
        var winner = await FindEnablementAsync(tenantId, manifest.ModuleKey, cancellationToken)
            ?? throw new InvalidOperationException($"Enabling module '{manifest.ModuleKey}' for tenant {tenantId} hit a uniqueness conflict but no enablement exists.");
        return AlreadyEnabled(manifest, winner);
    }

    private static EnableTenantModuleResult AlreadyEnabled(ModuleCapabilityManifest manifest, TenantModuleEnablement existing) =>
        new(EnableTenantModuleStatus.AlreadyEnabled, manifest.ModuleKey, existing.TemplateVersion, manifest.Version);

    private Task<TenantModuleEnablement?> FindEnablementAsync(TenantId tenantId, string moduleKey, CancellationToken cancellationToken) =>
        context.TenantModuleEnablements.AsNoTracking()
            .SingleOrDefaultAsync(e => e.TenantId == tenantId && e.ModuleKey == moduleKey, cancellationToken);

    /// <summary>A template referencing an unregistered or deprecated action would create dead grants — the
    /// registry is the platform-owned vocabulary, so fail closed rather than provision something inert.</summary>
    private async Task RequireRegisteredActionsAsync(ModuleCapabilityManifest manifest, CancellationToken cancellationToken)
    {
        var keys = manifest.ActionKeys();
        var active = await context.Actions
            .Where(a => keys.Contains(a.ActionKey) && !a.IsDeprecated)
            .Select(a => a.ActionKey)
            .ToListAsync(cancellationToken);

        var missing = keys.Except(active).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Module '{manifest.ModuleKey}' references action keys that are not active in the registry: {string.Join(", ", missing)}.");
    }

    private async Task<string?> FindKeyConflictAsync(TenantId tenantId, ModuleCapabilityManifest manifest, CancellationToken cancellationToken)
    {
        var setKeys = manifest.PermissionSets.Select(s => s.Key).ToList();
        var roleKeys = manifest.Roles.Select(r => r.Key).ToList();
        var roleNames = manifest.Roles.Select(r => r.Name).ToList();

        var clashingSets = await context.PermissionSets
            .Where(p => p.TenantId == tenantId && setKeys.Contains(p.Key))
            .Select(p => p.Key)
            .ToListAsync(cancellationToken);
        var clashingRoles = await context.Roles
            .Where(r => r.TenantId == tenantId && (roleKeys.Contains(r.Key) || roleNames.Contains(r.Name)))
            .Select(r => r.Key)
            .ToListAsync(cancellationToken);

        if (clashingSets.Count == 0 && clashingRoles.Count == 0)
            return null;

        return $"The tenant already has permission sets [{string.Join(", ", clashingSets)}] / roles [{string.Join(", ", clashingRoles)}] "
            + $"that module '{manifest.ModuleKey}' would create; they are never adopted or overwritten.";
    }

    private Task<List<long>> CurrentTenantAdministratorsAsync(TenantId tenantId, DateTimeOffset now, CancellationToken cancellationToken) =>
        (from assignment in context.RoleAssignments
         join role in context.Roles on new { assignment.TenantId, assignment.RoleId } equals new { role.TenantId, RoleId = role.Id }
         join membership in context.TenantMemberships on new { assignment.TenantId, assignment.AccountId } equals new { membership.TenantId, membership.AccountId }
         where assignment.TenantId == tenantId
             && role.Key == BootstrapTenantAccessHandler.TenantAdministratorRoleKey
             && assignment.ValidFrom <= now && (assignment.ValidTo == null || now < assignment.ValidTo)
             && membership.Status == MembershipStatus.Active
         select assignment.AccountId)
        .Distinct()
        .ToListAsync(cancellationToken);
}
