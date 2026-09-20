using System.Text.Json;
using Access.Domain.Authentication;
using Access.Domain.Identity;
using Access.Evidence;
using Access.Outbox;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Access.Application.Authentication;

/// <summary>Redeems an invitation. Nothing is consumed until everything that can fail without side
/// effects has passed (password verified / policy met); then, in ONE transaction: consume the token
/// (atomic single use — the first thing, so concurrent accepts of one token produce exactly one
/// winner and the losers create nothing), create the account when the address is new, activate the
/// membership, establish the session (same code as login), write the tenant evidence.</summary>
public sealed class AcceptInvitationHandler
{
    private const string EventType = "enterprise.access.membership.activated.v1";
    private const string EventSource = "/enterprise/access";

    private readonly AccessDbContext _context;
    private readonly PasswordService _passwordService;
    private readonly PasswordPolicy _passwordPolicy;
    private readonly CredentialVerifier _verifier;
    private readonly SessionOptions _sessionOptions;
    private readonly AccountTokenService _tokens;
    private readonly AuthEventWriter _eventWriter;
    private readonly TimeProvider _timeProvider;

    public AcceptInvitationHandler(
        AccessDbContext context,
        PasswordService passwordService,
        PasswordPolicy passwordPolicy,
        LockoutOptions lockoutOptions,
        SessionOptions sessionOptions,
        AccountTokenService tokens,
        AuthEventWriter eventWriter,
        TimeProvider? timeProvider = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        _passwordPolicy = passwordPolicy ?? throw new ArgumentNullException(nameof(passwordPolicy));
        _sessionOptions = sessionOptions ?? throw new ArgumentNullException(nameof(sessionOptions));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _eventWriter = eventWriter ?? throw new ArgumentNullException(nameof(eventWriter));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _verifier = new CredentialVerifier(context, passwordService, lockoutOptions ?? throw new ArgumentNullException(nameof(lockoutOptions)));
    }

    public async Task<AcceptInvitationResult> HandleAsync(AcceptInvitationCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = _timeProvider.GetUtcNow();
        var invalid = new AcceptInvitationResult(AcceptInvitationStatus.InvalidOrExpiredToken);

        var token = await _tokens.FindOutstandingAsync(command.Token, [AccountTokenPurpose.Invite], now, cancellationToken);
        if (token is null)
            return invalid;

        var email = token.EmailNormalized!;
        var tenantId = new TenantId(token.TenantId!.Value);
        var credential = await _context.AccountCredentials.FirstOrDefaultAsync(c => c.LoginEmailNormalized == email, cancellationToken);
        var rehash = false;

        if (credential is not null)
        {
            // The address already signs in: the invitation proves nothing about the password, so it must verify.
            var check = await _verifier.CheckAsync(credential, command.Password, now, cancellationToken);
            if (check is CredentialCheck.Failed or CredentialCheck.Locked)
            {
                await _eventWriter.WriteAsync("invite_accepted", check == CredentialCheck.Locked ? "account_locked" : "bad_password", now,
                    credential.AccountId, tenantId.Value, correlationId: command.CorrelationId, ipHash: command.IpHash,
                    detail: new() { ["attemptCount"] = credential.FailedAttempts }, cancellationToken: cancellationToken);
                return new AcceptInvitationResult(AcceptInvitationStatus.InvalidCredentials);
            }

            rehash = check == CredentialCheck.VerifiedRehashNeeded;
        }
        else
        {
            var violations = _passwordPolicy.Validate(command.Password, email);
            if (violations.Count > 0)
                return new AcceptInvitationResult(AcceptInvitationStatus.PolicyViolation, PolicyViolations: violations);
        }

        AcceptInvitationResult? outcome = null;
        Account? account = null;
        PrincipalRef? principal = null;
        IssuedSession? issued = null;
        long? membershipTenant = null;

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            if (!await _tokens.TryConsumeAsync(token.Id, now, cancellationToken))
                return invalid; // lost the race for the single use; nothing was created

            if (credential is null)
            {
                var displayName = FirstNonBlank(command.DisplayName, token.DisplayName, email[..email.IndexOf('@')]);
                (account, principal) = await AccountProvisioning.AddAsync(
                    _context, email, displayName, token.Locale, _sessionOptions.PlatformIssuer,
                    _passwordService.HashPassword(command.Password), email, cancellationToken);
            }
            else
            {
                account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == credential.AccountId, cancellationToken);
                var identity = await _context.ExternalIdentities.FirstOrDefaultAsync(
                    x => x.AccountId == credential.AccountId && x.Issuer == _sessionOptions.PlatformIssuer, cancellationToken);
                if (account is null || identity is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return invalid;
                }

                principal = identity.Principal;
                credential.ResetFailedAttempts();
                credential.RecordLogin(now);
                if (rehash)
                    credential.UpdatePasswordHash(_passwordService.HashPassword(command.Password), now);
            }

            // The membership is tenant-scoped (RLS): read and write it inside the invitation's tenant context.
            await _context.SetTenantContextAsync(tenantId, cancellationToken);
            var membership = await _context.TenantMemberships
                .FirstOrDefaultAsync(m => m.AccountId == account.Id && m.TenantId == tenantId, cancellationToken);

            var activated = false;
            if (membership is null)
            {
                membership = TenantMembership.Invite(tenantId, account.Id);
                membership.Activate();
                _context.TenantMemberships.Add(membership);
                activated = true;
            }
            else if (membership.Status == MembershipStatus.Invited)
            {
                membership.Activate();
                activated = true;
            }
            else if (membership.Status == MembershipStatus.Disabled)
            {
                await transaction.RollbackAsync(cancellationToken); // an invitation never re-enables a disabled member
                return invalid;
            }

            await SaveAllowingCredentialConflictAsync(credential, cancellationToken); // assigns the membership id

            if (activated)
            {
                var payload = JsonSerializer.Serialize(new { accountId = account.Id, invitationId = token.Id });
                var correlationId = CorrelationIds.ParseOrNew(command.CorrelationId);
                _context.EvidenceRecords.Add(EvidenceRecord.Create(
                    tenantId, nameof(TenantMembership), membership.Id, membership.RowVersion, principal.Value,
                    "TenantMembership.Activate", payload, correlationId));
                _context.OutboxMessages.Add(OutboxMessage.Create(
                    tenantId, nameof(TenantMembership), membership.Id, membership.RowVersion, EventType, EventSource,
                    $"tenants/{tenantId.Value}/memberships/{membership.Id}", correlationId, null, payload));
            }

            issued = await new SessionIssuer(_sessionOptions).PrepareAsync(_context, account.Id, now, command.UserAgentHash, cancellationToken);
            await SaveAllowingCredentialConflictAsync(credential, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            membershipTenant = tenantId.Value;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two tokens for one new address redeemed at the same instant: the loser retries as an existing account.
            return invalid;
        }

        await _eventWriter.WriteAsync("invite_accepted", "success", now, account!.Id, membershipTenant,
            issued!.Session.Id, command.CorrelationId, command.IpHash, cancellationToken: cancellationToken);

        outcome = new AcceptInvitationResult(
            AcceptInvitationStatus.Accepted,
            new AuthenticateResult(
                issued.Status,
                account.Id,
                account.DisplayName,
                issued.MembershipTenantIds,
                issued.SelectedTenantId,
                null,
                issued.RefreshCookie,
                issued.Session.Id,
                principal,
                new AccountSummary(account.Id, account.Email, account.DisplayName, account.Locale),
                issued.Session.AbsoluteExpiresAt));
        return outcome;
    }

    private static string FirstNonBlank(params string?[] values) => values.First(v => !string.IsNullOrWhiteSpace(v))!.Trim();

    /// <summary>A concurrent failed login may have touched the credential row; the membership/session write must
    /// not depend on the lockout counter, so a credential conflict is dropped and the rest is saved.</summary>
    private async Task SaveAllowingCredentialConflictAsync(AccountCredential? credential, CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) when (credential is not null)
        {
            _context.Entry(credential).State = EntityState.Unchanged;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
