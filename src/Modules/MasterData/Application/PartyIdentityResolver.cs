using Contracts;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterData.Application;

public sealed class PartyIdentityResolver : IPartyIdentityResolver
{
    private readonly MasterDataDbContext _context;

    public PartyIdentityResolver(MasterDataDbContext context) => _context = context;

    public Task<PartyRef?> ResolveAsync(PartyRef partyRef, CancellationToken cancellationToken = default) =>
        InTenantAsync(partyRef.TenantId, async () =>
        {
            var party = await _context.Parties.AsNoTracking()
                .SingleOrDefaultAsync(p => p.TenantId == partyRef.TenantId && p.Id == partyRef.PartyId, cancellationToken);
            if (party is null) return null;

            if (party.MergedIntoPartyId is { } canonicalId)
                return (PartyRef?)new PartyRef(partyRef.TenantId, canonicalId);

            return partyRef;
        }, cancellationToken);

    public async Task<PartyRef?> ResolveExternalIdentityAsync(
        TenantId tenantId, string provider, string sourceInstanceRef,
        string? externalType, string externalId, CancellationToken cancellationToken = default)
    {
        var normalizedType = externalType ?? string.Empty;
        var identity = await InTenantAsync(tenantId, () => _context.PartyExternalIdentities.AsNoTracking()
            .SingleOrDefaultAsync(
                e => e.TenantId == tenantId && e.SourceInstanceRef == sourceInstanceRef
                    && (e.ExternalType ?? string.Empty) == normalizedType && e.ExternalId == externalId,
                cancellationToken), cancellationToken);
        if (identity is null) return null;

        return await ResolveAsync(new PartyRef(tenantId, identity.PartyId), cancellationToken);
    }

    /// <summary>The party tables are RLS-protected: without `app.tenant_id` the runtime role sees nothing.</summary>
    private async Task<T> InTenantAsync<T>(TenantId tenantId, Func<Task<T>> work, CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction is not null)
        {
            await _context.SetTenantContextAsync(tenantId, cancellationToken);
            return await work();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.SetTenantContextAsync(tenantId, cancellationToken);
        var result = await work();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
