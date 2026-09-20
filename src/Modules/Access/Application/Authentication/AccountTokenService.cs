using Access.Domain.Authentication;
using Access.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Access.Application.Authentication;

/// <summary>Issues and redeems the single-use opaque tokens (`invite`, `password_reset`,
/// `password_setup`). The raw token is `<id>.<secret>`; only SHA-256(secret) is stored.
/// Redeeming is two steps on purpose: <see cref="FindOutstandingAsync"/> validates without
/// consuming (so a wrong password or a policy violation leaves the token usable), and
/// <see cref="TryConsumeAsync"/> is the atomic single-use compare-and-set.</summary>
public sealed class AccountTokenService
{
    private const int MaxRawLength = 200;

    // A dummy hash so an unknown id costs the same constant-time comparison as a known one.
    private static readonly string DummyHash = TokenSecrets.HashToken("dummy-account-token-secret");

    private readonly AccessDbContext _context;
    private readonly TokenOptions _options;

    public AccountTokenService(AccessDbContext context, TokenOptions options)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public TimeSpan InviteLifetime => TimeSpan.FromDays(_options.InviteDays);
    public TimeSpan PasswordResetLifetime => TimeSpan.FromMinutes(_options.PasswordResetMinutes);
    public TimeSpan PasswordSetupLifetime => TimeSpan.FromHours(_options.PasswordSetupHours);

    /// <summary>Generates a secret and its hash; the caller builds the entity and adds it to the context.</summary>
    public static (string Secret, string Hash) NewSecret() => TokenSecrets.GenerateAndHash();

    public static string Compose(Guid id, string secret) => $"{id:D}.{secret}";

    /// <summary>Never throws: malformed, oversized or empty input is simply not a token.</summary>
    public static bool TryParse(string? raw, out Guid id, out string secret)
    {
        id = Guid.Empty;
        secret = string.Empty;
        if (string.IsNullOrEmpty(raw) || raw.Length > MaxRawLength)
            return false;

        var separator = raw.IndexOf('.');
        if (separator <= 0 || separator != raw.LastIndexOf('.') || separator == raw.Length - 1)
            return false;

        if (!Guid.TryParseExact(raw.AsSpan(0, separator), "D", out id))
            return false;

        secret = raw[(separator + 1)..];
        return true;
    }

    /// <summary>The token if it is well-formed, the secret matches (constant time), it has one of
    /// <paramref name="purposes"/> and is neither consumed, revoked nor expired; otherwise null —
    /// the same null for every cause.</summary>
    public async Task<AccountToken?> FindOutstandingAsync(
        string? raw,
        IReadOnlyCollection<string> purposes,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!TryParse(raw, out var id, out var secret))
        {
            _ = TokenSecrets.HashesEqual(DummyHash, TokenSecrets.HashToken("malformed-input"));
            return null;
        }

        var token = await _context.AccountTokens.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        var presentedHash = TokenSecrets.HashToken(secret);
        var matches = TokenSecrets.HashesEqual(token?.TokenHash ?? DummyHash, presentedHash);

        if (token is null || !matches || !purposes.Contains(token.Purpose) || !token.IsOutstanding(now))
            return null;

        return token;
    }

    /// <summary>Atomic single use: true for exactly one caller, however many race for the same token.</summary>
    public async Task<bool> TryConsumeAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var affected = await _context.AccountTokens
            .Where(t => t.Id == id && t.ConsumedAt == null && t.RevokedAt == null && t.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumedAt, now), cancellationToken);

        return affected == 1;
    }

    public Task<int> RevokeOutstandingInvitesAsync(long tenantId, string emailNormalized, DateTimeOffset now, CancellationToken cancellationToken) =>
        _context.AccountTokens
            .Where(t => t.Purpose == AccountTokenPurpose.Invite && t.TenantId == tenantId && t.EmailNormalized == emailNormalized
                        && t.ConsumedAt == null && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);

    public Task<int> RevokeOutstandingForAccountAsync(long accountId, string purpose, DateTimeOffset now, CancellationToken cancellationToken) =>
        _context.AccountTokens
            .Where(t => t.AccountId == accountId && t.Purpose == purpose && t.ConsumedAt == null && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
}
