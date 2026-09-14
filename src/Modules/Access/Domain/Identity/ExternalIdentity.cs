using Contracts;

namespace Access.Domain.Identity;

/// <summary>Physical backing for <see cref="PrincipalRef"/> — one <see cref="Account"/> may
/// hold several rows (multiple IdPs). Directly maps `(issuer, subject)`, the pair
/// `PrincipalRef` already fixes as the cross-module identity key; never a mutable value like
/// email (docs/schema/identity-access-schema.md §1.1).</summary>
public sealed class ExternalIdentity
{
    public long Id { get; private set; }
    public long AccountId { get; private set; }
    public string Issuer { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public string? RawClaims { get; private set; }
    public DateTimeOffset LinkedAt { get; private set; }

    public PrincipalRef Principal => new(Issuer, Subject);

    private ExternalIdentity() { }

    public static ExternalIdentity Link(long accountId, PrincipalRef principal, string? rawClaimsJson = null)
    {
        return new ExternalIdentity
        {
            AccountId = accountId,
            Issuer = principal.Issuer,
            Subject = principal.Subject,
            RawClaims = rawClaimsJson,
            LinkedAt = DateTimeOffset.UtcNow
        };
    }
}
