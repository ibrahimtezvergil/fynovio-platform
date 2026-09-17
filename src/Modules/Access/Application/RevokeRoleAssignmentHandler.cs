using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Access.Domain.Authorization;
using Access.Evidence;
using Access.Outbox;
using Access.Idempotency;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

/// <summary>Same shape and ordering as `GrantRoleAssignmentHandler`: authorize before
/// the idempotency lookup, one transaction, one final `SaveChanges()`.</summary>
public sealed class RevokeRoleAssignmentHandler(AccessDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "RevokeRoleAssignment";
    private const string EventType = "enterprise.access.role_assignment.revoked.v1";
    private const string EventSource = "/enterprise/access";
    private const int SucceededStatus = 200;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    public async Task<RevokeRoleAssignmentResult> HandleAsync(RevokeRoleAssignmentCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        var actor = new ActorContext(command.TenantId, command.RevokedBy, command.CorrelationId);
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey("access.role_assignment.revoke"), new ResourceDescriptor("Access.RoleAssignment", command.RoleAssignmentId, null)),
            cancellationToken);
        if (!decision.IsAllowed)
            throw new AuthorizationDeniedException("access.role_assignment.revoke", decision.ReasonCode);

        var requestHash = HashRequest(command);
        var existing = await context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
            r => r.TenantId == command.TenantId && r.PrincipalIssuer == command.RevokedBy.Issuer
                && r.PrincipalSubject == command.RevokedBy.Subject && r.Operation == Operation
                && r.IdempotencyKey == command.IdempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);

            await transaction.CommitAsync(cancellationToken);
            var stored = JsonSerializer.Deserialize<RevokedPayload>(existing.ResponsePayload)
                ?? throw new InvalidOperationException("Stored idempotency response is empty.");
            return new RevokeRoleAssignmentResult(stored.RoleAssignmentId, Replayed: true);
        }

        var assignment = await context.RoleAssignments
            .SingleOrDefaultAsync(a => a.TenantId == command.TenantId && a.Id == command.RoleAssignmentId, cancellationToken)
            ?? throw new InvalidOperationException($"RoleAssignment {command.RoleAssignmentId} does not exist for tenant {command.TenantId}.");

        assignment.Revoke();

        var state = await context.TenantAccessStates.SingleAsync(s => s.TenantId == command.TenantId, cancellationToken);
        state.BumpRevision();

        await context.SaveChangesAsync(cancellationToken);

        var payload = new RevokedPayload(assignment.Id, command.Reason);
        var payloadJson = JsonSerializer.Serialize(payload);

        context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId, nameof(RoleAssignment), assignment.Id, assignment.RowVersion,
            command.RevokedBy, "RoleAssignment.Revoke", payloadJson, command.CorrelationId));
        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(RoleAssignment), assignment.Id, assignment.RowVersion,
            EventType, EventSource, $"role-assignments/{assignment.Id}", command.CorrelationId, null, payloadJson));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId, command.RevokedBy, Operation, command.IdempotencyKey, requestHash, SucceededStatus, payloadJson, IdempotencyRetention));

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RevokeRoleAssignmentResult(assignment.Id, Replayed: false);
    }

    private static string HashRequest(RevokeRoleAssignmentCommand command)
    {
        var canonical = string.Create(CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.RevokedBy}|{command.RoleAssignmentId}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed record RevokedPayload(long RoleAssignmentId, string? Reason);
}
