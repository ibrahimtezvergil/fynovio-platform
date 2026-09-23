using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Access.Domain.Authorization;
using Access.Domain.Identity;
using Access.Evidence;
using Access.Outbox;
using Access.Idempotency;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

/// <summary>Order (round 3 §9 final invariant): tenant context -> authorize ->
/// idempotency lookup -> mutation -> evidence/outbox/idempotency-write -> commit.
/// Authorizing before the idempotency lookup means a principal whose grant authority
/// was revoked between two identical requests gets denied on the retry, never a
/// stale cached "success" (the ordering bug this plan's gap-closure §9/round 3 flagged
/// in CompleteOpportunityHandler's current — CRM, untouched — ordering).</summary>
public sealed class GrantRoleAssignmentHandler(AccessDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "GrantRoleAssignment";
    private const string EventType = "enterprise.access.role_assignment.granted.v1";
    private const string EventSource = "/enterprise/access";
    private const int SucceededStatus = 200;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    public async Task<GrantRoleAssignmentResult> HandleAsync(GrantRoleAssignmentCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        var actor = new ActorContext(command.TenantId, command.GrantedBy, command.CorrelationId);
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey("access.role_assignment.grant"), new ResourceDescriptor("Access.RoleAssignment", null, null)),
            cancellationToken);
        if (!decision.IsAllowed)
            throw new AuthorizationDeniedException("access.role_assignment.grant", decision.ReasonCode);

        var requestHash = HashRequest(command);
        var existing = await context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
            r => r.TenantId == command.TenantId && r.PrincipalIssuer == command.GrantedBy.Issuer
                && r.PrincipalSubject == command.GrantedBy.Subject && r.Operation == Operation
                && r.IdempotencyKey == command.IdempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);

            await transaction.CommitAsync(cancellationToken);
            var stored = JsonSerializer.Deserialize<GrantedPayload>(existing.ResponsePayload)
                ?? throw new InvalidOperationException("Stored idempotency response is empty.");
            return new GrantRoleAssignmentResult(stored.RoleAssignmentId, Replayed: true);
        }

        var granteeAccountId = await context.TenantMemberships
            .Join(context.ExternalIdentities, m => m.AccountId, e => e.AccountId, (m, e) => new { m, e })
            .Where(x => x.m.TenantId == command.TenantId && x.e.Issuer == command.Grantee.Issuer && x.e.Subject == command.Grantee.Subject
                && x.m.Status == MembershipStatus.Active)
            .Select(x => (long?)x.m.AccountId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Grantee must be an active member of this tenant.");

        var grantedByAccountId = await context.ExternalIdentities
            .Where(e => e.Issuer == command.GrantedBy.Issuer && e.Subject == command.GrantedBy.Subject)
            .Select(e => (long?)e.AccountId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Granting principal must have a linked Account/ExternalIdentity.");

        var role = await context.Roles.SingleOrDefaultAsync(r => r.TenantId == command.TenantId && r.Key == command.RoleKey, cancellationToken)
            ?? throw new InvalidOperationException($"Role '{command.RoleKey}' does not exist for tenant {command.TenantId}.");

        // Serialize access changes for this tenant before checking the active grant.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE access.tenant_access_state SET revision = revision WHERE tenant_id = {command.TenantId.Value}", cancellationToken);
        if (await context.RoleAssignments.AnyAsync(a => a.TenantId == command.TenantId
            && a.AccountId == granteeAccountId && a.RoleId == role.Id && a.ValidTo == null, cancellationToken))
            throw new RoleAssignmentConflictException("This member already has the role.");

        var assignment = RoleAssignment.Grant(command.TenantId, granteeAccountId, role.Id, grantedByAccountId, RoleAssignment.SourceManual, command.Reason);
        context.RoleAssignments.Add(assignment);

        var state = await context.TenantAccessStates.SingleAsync(s => s.TenantId == command.TenantId, cancellationToken);
        state.BumpRevision();

        await context.SaveChangesAsync(cancellationToken); // assigns assignment.Id

        var payload = new GrantedPayload(assignment.Id, command.RoleKey);
        var payloadJson = JsonSerializer.Serialize(payload);

        context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId, nameof(RoleAssignment), assignment.Id, assignment.RowVersion,
            command.GrantedBy, "RoleAssignment.Grant", payloadJson, command.CorrelationId));
        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(RoleAssignment), assignment.Id, assignment.RowVersion,
            EventType, EventSource, $"role-assignments/{assignment.Id}", command.CorrelationId, null, payloadJson));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId, command.GrantedBy, Operation, command.IdempotencyKey, requestHash, SucceededStatus, payloadJson, IdempotencyRetention));

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new GrantRoleAssignmentResult(assignment.Id, Replayed: false);
    }

    private static string HashRequest(GrantRoleAssignmentCommand command)
    {
        var canonical = string.Create(CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.GrantedBy}|{command.Grantee}|{command.RoleKey}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed record GrantedPayload(long RoleAssignmentId, string RoleKey);
}
