using Contracts;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterData.Application;

public sealed class PartyIdentityResolver : IPartyIdentityResolver
{
    private readonly MasterDataDbContext _context;

    public PartyIdentityResolver(MasterDataDbContext context) => _context = context;

    public async Task<PartyRef?> ResolveAsync(PartyRef partyRef, CancellationToken cancellationToken = default)
    {
        var party = await _context.Parties.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == partyRef.TenantId && p.Id == partyRef.PartyId, cancellationToken);
        if (party is null) return null;

        if (party.MergedIntoPartyId is { } canonicalId)
            return new PartyRef(partyRef.TenantId, canonicalId);

        return partyRef;
    }

    public async Task<PartyRef?> ResolveExternalIdentityAsync(
        TenantId tenantId, string provider, string sourceInstanceRef,
        string? externalType, string externalId, CancellationToken cancellationToken = default)
    {
        var normalizedType = externalType ?? string.Empty;
        var identity = await _context.PartyExternalIdentities.AsNoTracking()
            .SingleOrDefaultAsync(
                e => e.TenantId == tenantId && e.SourceInstanceRef == sourceInstanceRef
                    && (e.ExternalType ?? string.Empty) == normalizedType && e.ExternalId == externalId,
                cancellationToken);
        if (identity is null) return null;

        return await ResolveAsync(new PartyRef(tenantId, identity.PartyId), cancellationToken);
    }
}
