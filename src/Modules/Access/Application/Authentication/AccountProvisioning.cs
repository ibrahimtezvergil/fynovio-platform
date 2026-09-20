using System.Security.Cryptography;
using Access.Domain.Authentication;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;

namespace Access.Application.Authentication;

/// <summary>Creates the Account + platform ExternalIdentity (+ optionally the password credential)
/// pieces shared by provisioning, invitation acceptance, self-registration and the bootstrap
/// command. Runs inside the caller's transaction; the account is saved here because the identity
/// and credential need its generated id.</summary>
internal static class AccountProvisioning
{
    /// <summary>128-bit opaque subject: does not contain the e-mail address or the account id.</summary>
    public static string NewSubject() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    public static async Task<(Account Account, PrincipalRef Principal)> AddAsync(
        AccessDbContext context,
        string email,
        string displayName,
        string? locale,
        string platformIssuer,
        string? passwordHash,
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        var account = Account.Create(email, displayName, locale);
        context.Accounts.Add(account);
        await context.SaveChangesAsync(cancellationToken); // assigns account.Id

        if (passwordHash is not null)
            context.AccountCredentials.Add(AccountCredential.Create(account.Id, normalizedEmail, passwordHash));

        var principal = new PrincipalRef(platformIssuer, NewSubject());
        context.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));

        return (account, principal);
    }
}
