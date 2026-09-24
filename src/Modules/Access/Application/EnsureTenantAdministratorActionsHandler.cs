using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Access.Domain.Authorization;
using Access.Evidence;
using Access.Idempotency;
using Access.Outbox;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Access.Application;

public sealed record EnsureTenantAdministratorActionsCommand(
    TenantId TenantId, PrincipalRef Principal, IReadOnlyList<string> ActionKeys, Guid CorrelationId);

/// <summary>Adds explicitly selected, newly introduced capabilities to the existing tenant administrator's
/// bootstrap permission set. This is a narrow compatibility path for tenants bootstrapped before an action
/// was registered; it never modifies ordinary roles or grants arbitrary caller-selected permissions.</summary>
public sealed class EnsureTenantAdministratorActionsHandler(AccessDbContext context)
{
    private const string Operation = "EnsureTenantAdministratorActions";
    private const string TenantAdministratorPermissionSetKey = "tenant_administration";
    private const string EventType = "enterprise.access.tenant_administrator.capabilities_updated.v1";
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    public async Task<bool> HandleAsync(EnsureTenantAdministratorActionsCommand command, CancellationToken cancellationToken = default)
    {
        var actionKeys = command.ActionKeys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (actionKeys.Length != command.ActionKeys.Count || actionKeys.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Action keys must be distinct and non-empty.", nameof(command));

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        var accountId = await context.ExternalIdentities
            .Where(identity => identity.Issuer == command.Principal.Issuer && identity.Subject == command.Principal.Subject)
            .Select(identity => (long?)identity.AccountId)
            .SingleOrDefaultAsync(cancellationToken);
        if (accountId is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var permissionSet = await (
            from assignment in context.RoleAssignments
            join role in context.Roles on assignment.RoleId equals role.Id
            join rolePermissionSet in context.RolePermissionSets on role.Id equals rolePermissionSet.RoleId
            join set in context.PermissionSets.Include(set => set.Items) on rolePermissionSet.PermissionSetId equals set.Id
            where assignment.TenantId == command.TenantId && assignment.AccountId == accountId
                && assignment.ValidFrom <= now && (assignment.ValidTo == null || now < assignment.ValidTo)
                && role.TenantId == command.TenantId && role.Key == BootstrapTenantAccessHandler.TenantAdministratorRoleKey
                && role.Origin == Role.OriginSystemTemplate
                && set.TenantId == command.TenantId && set.Key == TenantAdministratorPermissionSetKey
                && set.Origin == PermissionSet.OriginSystemTemplate
            select set).SingleOrDefaultAsync(cancellationToken);

        if (permissionSet is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var missingActions = actionKeys.Except(permissionSet.Items.Select(item => item.ActionKey), StringComparer.Ordinal).ToArray();
        if (missingActions.Length == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var registeredActions = await context.Actions.Where(action => missingActions.Contains(action.ActionKey) && !action.IsDeprecated)
            .Select(action => action.ActionKey).ToListAsync(cancellationToken);
        if (registeredActions.Count != missingActions.Length)
            throw new InvalidOperationException("All administrator capabilities must be active in the action registry.");

        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', actionKeys))));
        var idempotencyKey = $"{Operation}:v1:{requestHash}";
        var replay = await context.IdempotencyRecords.SingleOrDefaultAsync(record =>
            record.TenantId == command.TenantId && record.PrincipalIssuer == command.Principal.Issuer
            && record.PrincipalSubject == command.Principal.Subject && record.Operation == Operation
            && record.IdempotencyKey == idempotencyKey && record.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken);
        if (replay is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        foreach (var actionKey in missingActions)
            permissionSet.Grant(actionKey);

        var accessState = await context.TenantAccessStates.SingleAsync(state => state.TenantId == command.TenantId, cancellationToken);
        accessState.BumpRevision();
        var details = JsonSerializer.Serialize(new { roleKey = BootstrapTenantAccessHandler.TenantAdministratorRoleKey, actionKeys = missingActions, revision = accessState.Revision });
        context.EvidenceRecords.Add(EvidenceRecord.Create(command.TenantId, nameof(PermissionSet), permissionSet.Id,
            accessState.Revision, command.Principal, "TenantAdministrator.CapabilitiesAdded", details, command.CorrelationId));
        context.OutboxMessages.Add(OutboxMessage.Create(command.TenantId, nameof(PermissionSet), permissionSet.Id,
            accessState.Revision, EventType, "/enterprise/access", $"permission-sets/{permissionSet.Id}",
            command.CorrelationId, null, details));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, command.Principal, Operation,
            idempotencyKey, requestHash, 200, JsonSerializer.Serialize(new { actionKeys = missingActions }), IdempotencyRetention));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" }
                                                   or PostgresException { SqlState: "40001" })
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            return false;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            return false;
        }
    }
}
