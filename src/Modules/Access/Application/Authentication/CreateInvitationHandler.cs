using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Access.Domain.Authentication;
using Access.Evidence;
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

    public CreateInvitationHandler(
        AccessDbContext context,
        IAuthorizer authorizer,
        AccountTokenService tokens,
        IEmailSender emailSender,
        AuthEventWriter eventWriter,
        TimeProvider? timeProvider = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        _eventWriter = eventWriter ?? throw new ArgumentNullException(nameof(eventWriter));
        _timeProvider = timeProvider ?? TimeProvider.System;
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

        var inviterAccountId = await _context.ExternalIdentities
            .Where(e => e.Issuer == command.Actor.Principal.Issuer && e.Subject == command.Actor.Principal.Subject)
            .Select(e => (long?)e.AccountId)
            .FirstOrDefaultAsync(cancellationToken);

        // Only the newest invitation for (tenant, address) can be redeemed.
        await _tokens.RevokeOutstandingInvitesAsync(tenantId.Value, email, now, cancellationToken);

        var (secret, hash) = AccountTokenService.NewSecret();
        var token = AccountToken.CreateInvite(
            tenantId.Value, email, command.DisplayName, command.Locale, hash, now, _tokens.InviteLifetime, inviterAccountId);
        _context.AccountTokens.Add(token);

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

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _eventWriter.WriteAsync("invite_created", "success", now, inviterAccountId, tenantId.Value,
            correlationId: correlationId.ToString(), ipHash: command.IpHash, cancellationToken: cancellationToken);

        await _emailSender.SendAsync(
            new EmailMessage(email, EmailMessage.InviteTemplate, command.Locale ?? "tr", new Dictionary<string, string>
            {
                ["token"] = AccountTokenService.Compose(token.Id, secret),
                ["tenantId"] = tenantId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["expiresAt"] = token.ExpiresAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
            }),
            cancellationToken);

        return new CreateInvitationResult(CreateInvitationStatus.Accepted);
    }
}
