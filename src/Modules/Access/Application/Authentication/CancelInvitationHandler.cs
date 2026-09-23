using System.Security.Cryptography;
using System.Text;
using Access.Domain.Authentication;
using Access.Evidence;
using Access.Idempotency;
using Access.Outbox;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application.Authentication;

public sealed class CancelInvitationHandler(AccessDbContext context, IAuthorizer authorizer, TimeProvider timeProvider)
{
    private const string Operation = "CancelInvitation";

    public async Task HandleAsync(ActorContext actor, Guid invitationId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(actor.TenantId, cancellationToken);
        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(actor,
            new ActionKey(CreateInvitationHandler.ActionKeyValue), new ResourceDescriptor("Identity.TenantMembership", null, null)), cancellationToken);
        if (!decision.IsAllowed)
            throw new Access.Application.AuthorizationDeniedException(CreateInvitationHandler.ActionKeyValue, decision.ReasonCode);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(invitationId.ToString("D"))));
        var existing = await context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(record =>
            record.TenantId == actor.TenantId && record.PrincipalIssuer == actor.Principal.Issuer
            && record.PrincipalSubject == actor.Principal.Subject && record.Operation == Operation
            && record.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != hash)
                throw new Access.Application.IdempotencyKeyReusedException(Operation, idempotencyKey);
            return;
        }

        var now = timeProvider.GetUtcNow();
        var token = await context.AccountTokens.SingleOrDefaultAsync(t => t.Id == invitationId
            && t.TenantId == actor.TenantId.Value && t.Purpose == AccountTokenPurpose.Invite
            && t.ConsumedAt == null && t.RevokedAt == null && t.ExpiresAt > now, cancellationToken)
            ?? throw new InvitationUnavailableException();
        token.Revoke(now);
        var delivery = await context.InvitationDeliveries.SingleOrDefaultAsync(row =>
            row.TenantId == actor.TenantId && row.InvitationId == invitationId, cancellationToken);
        delivery?.MarkDelivered(now);

        var payload = System.Text.Json.JsonSerializer.Serialize(new { invitationId });
        context.EvidenceRecords.Add(EvidenceRecord.Create(actor.TenantId, "Tenant", actor.TenantId.Value, 0,
            actor.Principal, "Tenant.CancelInvitation", payload, actor.CorrelationId));
        context.OutboxMessages.Add(OutboxMessage.Create(actor.TenantId, "Tenant", actor.TenantId.Value, 0,
            "enterprise.access.membership.invitation_cancelled.v1", "/enterprise/access",
            $"tenants/{actor.TenantId.Value}/invitations/{invitationId}", actor.CorrelationId, null, payload));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(actor.TenantId, actor.Principal, Operation,
            idempotencyKey, hash, 204, payload, TimeSpan.FromDays(7)));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}

public sealed class InvitationUnavailableException : Exception
{
    public InvitationUnavailableException() : base("The invitation is no longer available.") { }
}

public sealed class InvitationRoleUnavailableException : Exception
{
    public InvitationRoleUnavailableException() : base("The selected role is not available in this tenant.") { }
}
