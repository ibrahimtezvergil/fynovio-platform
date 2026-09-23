using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Access.Domain.Authentication;
using Access.Domain.Authorization;
using Access.Evidence;
using Access.Idempotency;
using Access.Outbox;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application.Authentication;

/// <summary>Order (same as the other Access commands): tenant context → authorize → mutation →
/// evidence/outbox → commit; the mail goes out only after the commit, so a token that was never
/// stored is never mailed. Authorization comes before any lookup so a denied caller learns nothing.</summary>
public sealed class CreateInvitationHandler
{
    public const string ActionKeyValue = "identity.membership.invite";
    private const string EventType = "enterprise.access.membership.invited.v1";
    private const string EventSource = "/enterprise/access";

    private readonly AccessDbContext _context;
    private readonly IAuthorizer _authorizer;
    private readonly AccountTokenService _tokens;
    private readonly IEmailSender _emailSender;
    private readonly AuthEventWriter _eventWriter;
    private readonly TimeProvider _timeProvider;
    private readonly IInvitationTokenProtector? _protector;
    private readonly IInvitationPreviewRecorder? _previewRecorder;

    public CreateInvitationHandler(
        AccessDbContext context,
        IAuthorizer authorizer,
        AccountTokenService tokens,
        IEmailSender emailSender,
        AuthEventWriter eventWriter,
        TimeProvider? timeProvider = null,
        IInvitationTokenProtector? protector = null,
        IInvitationPreviewRecorder? previewRecorder = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        _eventWriter = eventWriter ?? throw new ArgumentNullException(nameof(eventWriter));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _protector = protector;
        _previewRecorder = previewRecorder;
    }

    public async Task<CreateInvitationResult> HandleAsync(CreateInvitationCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = _timeProvider.GetUtcNow();
        var tenantId = command.Actor.TenantId;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.SetTenantContextAsync(tenantId, cancellationToken);

        var decision = await _authorizer.AuthorizeAsync(
            new AuthorizationRequest(command.Actor, new ActionKey(ActionKeyValue), new ResourceDescriptor("Identity.TenantMembership", null, null)),
            cancellationToken);
        if (!decision.IsAllowed)
        {
            await transaction.RollbackAsync(cancellationToken);
            await _eventWriter.WriteAsync("invite_created", "forbidden", now, tenantId: tenantId.Value,
                correlationId: command.Actor.CorrelationId.ToString(), ipHash: command.IpHash, cancellationToken: cancellationToken);
            return new CreateInvitationResult(CreateInvitationStatus.Forbidden);
        }

        if (!EmailAddressRules.IsPlausible(command.Email))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new CreateInvitationResult(CreateInvitationStatus.InvalidEmail);
        }

        var email = EmailNormalizer.Normalize(command.Email);
        if (command.RoleKey is not null)
        {
            var grantDecision = await _authorizer.AuthorizeAsync(new AuthorizationRequest(command.Actor,
                new ActionKey("access.role_assignment.grant"), new ResourceDescriptor("Access.RoleAssignment", null, null)),
                cancellationToken);
            if (!grantDecision.IsAllowed)
                throw new Access.Application.AuthorizationDeniedException("access.role_assignment.grant", grantDecision.ReasonCode);
            if (!await _context.Roles.AnyAsync(role => role.TenantId == tenantId && role.Key == command.RoleKey, cancellationToken))
                throw new InvitationRoleUnavailableException();
        }
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(new { email, command.DisplayName, command.Locale, command.RoleKey }))));
        var idempotencyKey = command.IdempotencyKey ?? Guid.NewGuid().ToString("N");
        var existing = await _context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(record =>
            record.TenantId == tenantId && record.PrincipalIssuer == command.Actor.Principal.Issuer
            && record.PrincipalSubject == command.Actor.Principal.Subject && record.Operation == "CreateInvitation"
            && record.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                throw new Access.Application.IdempotencyKeyReusedException("CreateInvitation", idempotencyKey);
            return new CreateInvitationResult(CreateInvitationStatus.Accepted);
        }

        var inviterAccountId = await _context.ExternalIdentities
            .Where(e => e.Issuer == command.Actor.Principal.Issuer && e.Subject == command.Actor.Principal.Subject)
            .Select(e => (long?)e.AccountId)
            .FirstOrDefaultAsync(cancellationToken);

        // Only the newest invitation for (tenant, address) can be redeemed.
        var priorInvitationIds = await _context.AccountTokens
            .Where(token => token.Purpose == AccountTokenPurpose.Invite && token.TenantId == tenantId.Value
                && token.EmailNormalized == email && token.ConsumedAt == null && token.RevokedAt == null)
            .Select(token => token.Id).ToListAsync(cancellationToken);
        await _tokens.RevokeOutstandingInvitesAsync(tenantId.Value, email, now, cancellationToken);
        if (priorInvitationIds.Count > 0)
            await _context.InvitationDeliveries
                .Where(delivery => delivery.TenantId == tenantId && priorInvitationIds.Contains(delivery.InvitationId))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(delivery => delivery.DeliveredAt, now)
                    .SetProperty(delivery => delivery.ProtectedToken, string.Empty), cancellationToken);

        var (secret, hash) = AccountTokenService.NewSecret();
        var token = AccountToken.CreateInvite(
            tenantId.Value, email, command.DisplayName, command.Locale, hash, now, _tokens.InviteLifetime, inviterAccountId,
            command.RoleKey);
        _context.AccountTokens.Add(token);
        var rawToken = AccountTokenService.Compose(token.Id, secret);
        if (_protector is not null)
            _context.InvitationDeliveries.Add(InvitationDelivery.Create(tenantId, token.Id,
                _protector.Protect(rawToken), now));

        // Evidence/outbox are tenant-scoped (RLS): written inside the tenant context. They name the
        // invitation and a hash of the address — never the address or the token.
        var revision = await _context.TenantAccessStates
            .Where(s => s.TenantId == tenantId).Select(s => s.Revision).FirstOrDefaultAsync(cancellationToken);
        var payload = JsonSerializer.Serialize(new
        {
            invitationId = token.Id,
            emailHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email))).ToLowerInvariant(),
            expiresAt = token.ExpiresAt
        });
        var correlationId = command.Actor.CorrelationId;
        _context.EvidenceRecords.Add(EvidenceRecord.Create(
            tenantId, "Tenant", tenantId.Value, revision, command.Actor.Principal, "Tenant.InviteMember", payload, correlationId));
        _context.OutboxMessages.Add(OutboxMessage.Create(
            tenantId, "Tenant", tenantId.Value, revision, EventType, EventSource,
            $"tenants/{tenantId.Value}/invitations/{token.Id}", correlationId, null, payload));
        _context.IdempotencyRecords.Add(IdempotencyRecord.Create(tenantId, command.Actor.Principal,
            "CreateInvitation", idempotencyKey, requestHash, 202, payload, TimeSpan.FromDays(7)));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _eventWriter.WriteAsync("invite_created", "success", now, inviterAccountId, tenantId.Value,
            correlationId: correlationId.ToString(), ipHash: command.IpHash, cancellationToken: cancellationToken);

        var message = new EmailMessage(email, EmailMessage.InviteTemplate, command.Locale ?? "tr", new Dictionary<string, string>
        {
            ["token"] = rawToken,
            ["tenantId"] = tenantId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["expiresAt"] = token.ExpiresAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
        });
        if (_protector is null)
            await _emailSender.SendAsync(message, cancellationToken);
        else if (_previewRecorder is not null)
            await _previewRecorder.RecordAsync(message, cancellationToken);

        return new CreateInvitationResult(CreateInvitationStatus.Accepted);
    }
}
