using Access.Domain.Identity;
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

    /// <summary>Used by Host's ActorContextMiddleware (round 3 §4 pipeline step
    /// "Tenant membership/state validation") — kept inside Access.Application so Host
    /// never touches Access.Domain.Identity directly (AGENTS.md: "Domain and
    /// Infrastructure namespaces inside a module are never imported from outside it").</summary>
    public async Task<bool> IsActiveTenantMemberAsync(PrincipalRef principal, TenantId tenantId, CancellationToken cancellationToken = default) =>
        await context.TenantMemberships
            .Join(context.ExternalIdentities, m => m.AccountId, e => e.AccountId, (m, e) => new { m, e })
            .AnyAsync(
                x => x.m.TenantId == tenantId && x.e.Issuer == principal.Issuer && x.e.Subject == principal.Subject
                    && x.m.Status == MembershipStatus.Active,
                cancellationToken);
}
