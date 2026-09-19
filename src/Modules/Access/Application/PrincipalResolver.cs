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
    /// Infrastructure namespaces inside a module are never imported from outside it").
    /// `identity.tenant_memberships` is RLS-protected (2026-09-20 PHASE_1_5_RUNTIME_RLS_PDP_DELTA):
    /// the claimed `tenantId` establishes DB visibility before this membership check runs —
    /// it is not itself authorization, since the query still filters on `m.TenantId == tenantId`
    /// and `m.Status == Active` independently of RLS.</summary>
    public async Task<bool> IsActiveTenantMemberAsync(PrincipalRef principal, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        // Reentrant — see AccessAuthorizer.AuthorizeAsync's comment. Not currently exercised
        // (ActorContextMiddleware calls this before anything else touches AccessDbContext in
        // the request), but kept consistent with the other two PDP entry points so all three
        // share one uniform rule rather than three subtly different ones.
        if (context.Database.CurrentTransaction is not null)
            return await EvaluateIsActiveTenantMemberAsync(principal, tenantId, cancellationToken);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(tenantId, cancellationToken);

        var isActiveMember = await EvaluateIsActiveTenantMemberAsync(principal, tenantId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return isActiveMember;
    }

    private async Task<bool> EvaluateIsActiveTenantMemberAsync(PrincipalRef principal, TenantId tenantId, CancellationToken cancellationToken) =>
        await context.ExternalIdentities
            .Where(e => e.Issuer == principal.Issuer && e.Subject == principal.Subject)
            .Join(context.TenantMemberships, e => e.AccountId, m => m.AccountId, (e, m) => m)
            .AnyAsync(m => m.TenantId == tenantId && m.Status == MembershipStatus.Active, cancellationToken);
}
