using System.Globalization;
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

namespace Access.Application;

public sealed record CreateTenantRoleCommand(
    TenantId TenantId,
    PrincipalRef RequestedBy,
    string Name,
    IReadOnlyList<string> ActionKeys,
    long ExpectedRevision,
    Guid CorrelationId,
    string IdempotencyKey);

public sealed record UpdateTenantRoleCommand(
    TenantId TenantId,
    PrincipalRef RequestedBy,
    string RoleKey,
    string Name,
    IReadOnlyList<string> ActionKeys,
    long ExpectedRevision,
    Guid CorrelationId,
    string IdempotencyKey);

public sealed record ManageTenantRoleResult(string RoleKey, long Revision, bool Replayed);

public sealed class TenantAccessRevisionConflictException : InvalidOperationException
{
    public TenantAccessRevisionConflictException() : base("Access settings changed. Refresh and try again.") { }
}

public sealed class SystemRoleImmutableException : InvalidOperationException
{
    public SystemRoleImmutableException() : base("Roles provided by an enabled module are read-only.") { }
}

/// <summary>Creates and changes tenant-authored roles. Module-template roles keep their provenance and are
/// intentionally immutable; a tenant can add a custom role without changing a module's capability contract.</summary>
public sealed class ManageTenantRoleHandler(AccessDbContext context, IAuthorizer authorizer)
{
    private const string CreateOperation = "CreateTenantRole";
    private const string UpdateOperation = "UpdateTenantRole";
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    public async Task<ManageTenantRoleResult> CreateAsync(CreateTenantRoleCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);
        await AuthorizeAsync(command.TenantId, command.RequestedBy, command.CorrelationId, cancellationToken);

        var hash = Hash(CreateOperation, command.TenantId, command.RequestedBy, command.Name, command.ActionKeys, command.ExpectedRevision);
        var replay = await ReplayAsync(command.TenantId, command.RequestedBy, CreateOperation, command.IdempotencyKey, hash, cancellationToken);
        if (replay is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return replay with { Replayed = true };
        }

        await ValidateActionsAsync(command.ActionKeys, cancellationToken);
        var state = await RequireRevisionAsync(command.TenantId, command.ExpectedRevision, cancellationToken);
        var key = $"custom-{Guid.NewGuid():N}";
        var permissionSet = PermissionSet.Create(command.TenantId, key, command.Name);
        permissionSet.ReplaceGrants(command.ActionKeys.Select(actionKey => (actionKey, (string?)null)));
        var role = Role.Create(command.TenantId, key, command.Name);
        context.PermissionSets.Add(permissionSet);
        context.Roles.Add(role);
        await context.SaveChangesAsync(cancellationToken);
        context.RolePermissionSets.Add(RolePermissionSet.Create(command.TenantId, role.Id, permissionSet.Id));
        state.BumpRevision();
        await WriteAuditAsync(command.TenantId, command.RequestedBy, command.CorrelationId, role, state.Revision,
            "Role.Create", new { roleKey = role.Key, role.Name, actionKeys = command.ActionKeys }, CreateOperation, command.IdempotencyKey, hash, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ManageTenantRoleResult(role.Key, state.Revision, false);
    }

    public async Task<ManageTenantRoleResult> UpdateAsync(UpdateTenantRoleCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);
        await AuthorizeAsync(command.TenantId, command.RequestedBy, command.CorrelationId, cancellationToken);

        var hash = Hash(UpdateOperation, command.TenantId, command.RequestedBy, command.RoleKey, command.Name, command.ActionKeys, command.ExpectedRevision);
        var replay = await ReplayAsync(command.TenantId, command.RequestedBy, UpdateOperation, command.IdempotencyKey, hash, cancellationToken);
        if (replay is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return replay with { Replayed = true };
        }

        await ValidateActionsAsync(command.ActionKeys, cancellationToken);
        var state = await RequireRevisionAsync(command.TenantId, command.ExpectedRevision, cancellationToken);
        var role = await context.Roles.SingleOrDefaultAsync(role => role.TenantId == command.TenantId && role.Key == command.RoleKey, cancellationToken)
            ?? throw new ArgumentException("Role was not found.", nameof(command.RoleKey));
        if (role.Origin != Role.OriginTenant)
            throw new SystemRoleImmutableException();

        var permissionSet = await (
            from rolePermissionSet in context.RolePermissionSets
            join set in context.PermissionSets.Include(set => set.Items) on rolePermissionSet.PermissionSetId equals set.Id
            where rolePermissionSet.TenantId == command.TenantId && rolePermissionSet.RoleId == role.Id && set.Origin == PermissionSet.OriginTenant
            select set).SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The custom role does not have an editable permission set.");

        role.Rename(command.Name);
        permissionSet.Rename(command.Name);
        permissionSet.ReplaceGrants(command.ActionKeys.Select(actionKey => (actionKey, (string?)null)));
        state.BumpRevision();
        await WriteAuditAsync(command.TenantId, command.RequestedBy, command.CorrelationId, role, state.Revision,
            "Role.Update", new { roleKey = role.Key, role.Name, actionKeys = command.ActionKeys }, UpdateOperation, command.IdempotencyKey, hash, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ManageTenantRoleResult(role.Key, state.Revision, false);
    }

    private async Task AuthorizeAsync(TenantId tenantId, PrincipalRef principal, Guid correlationId, CancellationToken cancellationToken)
    {
        var actor = new ActorContext(tenantId, principal, correlationId);
        foreach (var (actionKey, resource) in new[]
                 {
                     ("access.role.manage", "Access.Role"),
                     ("access.permission_set.manage", "Access.PermissionSet")
                 })
        {
            var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(actor, new ActionKey(actionKey),
                new ResourceDescriptor(resource, null, null)), cancellationToken);
            if (!decision.IsAllowed)
                throw new AuthorizationDeniedException(actionKey, decision.ReasonCode);
        }
    }

    private async Task ValidateActionsAsync(IReadOnlyList<string> actionKeys, CancellationToken cancellationToken)
    {
        if (actionKeys.Count == 0 || actionKeys.Any(string.IsNullOrWhiteSpace)
            || actionKeys.Distinct(StringComparer.Ordinal).Count() != actionKeys.Count)
            throw new ArgumentException("Select one or more distinct permissions.", nameof(actionKeys));

        var known = await context.Actions.Where(action => actionKeys.Contains(action.ActionKey) && !action.IsDeprecated)
            .Select(action => action.ActionKey).ToListAsync(cancellationToken);
        if (known.Count != actionKeys.Count)
            throw new ArgumentException("One or more permissions are not available.", nameof(actionKeys));
    }

    private async Task<TenantAccessState> RequireRevisionAsync(TenantId tenantId, long expectedRevision, CancellationToken cancellationToken)
    {
        var state = await context.TenantAccessStates.SingleAsync(state => state.TenantId == tenantId, cancellationToken);
        if (state.Revision != expectedRevision)
            throw new TenantAccessRevisionConflictException();
        return state;
    }

    private async Task<ManageTenantRoleResult?> ReplayAsync(TenantId tenantId, PrincipalRef principal, string operation,
        string idempotencyKey, string requestHash, CancellationToken cancellationToken)
    {
        var existing = await context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(record =>
            record.TenantId == tenantId && record.PrincipalIssuer == principal.Issuer && record.PrincipalSubject == principal.Subject
            && record.Operation == operation && record.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is null) return null;
        if (existing.RequestHash != requestHash)
            throw new IdempotencyKeyReusedException(operation, idempotencyKey);
        return JsonSerializer.Deserialize<ManageTenantRoleResult>(existing.ResponsePayload)
            ?? throw new InvalidOperationException("Stored idempotency response is empty.");
    }

    private async Task WriteAuditAsync(TenantId tenantId, PrincipalRef principal, Guid correlationId, Role role, long revision,
        string evidenceAction, object detail, string operation, string idempotencyKey, string requestHash, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { roleKey = role.Key, revision });
        context.EvidenceRecords.Add(EvidenceRecord.Create(tenantId, nameof(Role), role.Id, revision, principal, evidenceAction,
            JsonSerializer.Serialize(detail), correlationId));
        context.OutboxMessages.Add(OutboxMessage.Create(tenantId, nameof(Role), role.Id, revision,
            evidenceAction == "Role.Create" ? "enterprise.access.role.created.v1" : "enterprise.access.role.updated.v1",
            "/enterprise/access", $"roles/{role.Id}", correlationId, null, payload));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(tenantId, principal, operation, idempotencyKey,
            requestHash, 200, JsonSerializer.Serialize(new ManageTenantRoleResult(role.Key, revision, false)), IdempotencyRetention));
        await Task.CompletedTask;
    }

    private static string Hash(string operation, TenantId tenantId, PrincipalRef principal, params object[] values)
    {
        var canonical = string.Join('|', new[] { operation, tenantId.Value.ToString(CultureInfo.InvariantCulture), principal.ToString() }
            .Concat(values.Select(value => value switch
            {
                IReadOnlyList<string> strings => string.Join(',', strings.Order(StringComparer.Ordinal)),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            })));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
