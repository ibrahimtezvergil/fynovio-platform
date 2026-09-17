using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

public sealed class PrincipalResolver(AccessDbContext context)
{
    /// <summary>Fail-closed: an unrecognized `PrincipalRef` resolves to `null`, never
    /// to an assumed identity (round 1 decision #2 default-deny).</summary>
    public async Task<long?> ResolveAccountIdAsync(PrincipalRef principal, CancellationToken cancellationToken = default) =>
        await context.ExternalIdentities
            .Where(e => e.Issuer == principal.Issuer && e.Subject == principal.Subject)
            .Select(e => (long?)e.AccountId)
            .SingleOrDefaultAsync(cancellationToken);
}
