using Contracts;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterData.Application;

public sealed class PartyDirectory : IPartyDirectory
{
    private readonly MasterDataDbContext _context;

    public PartyDirectory(MasterDataDbContext context) => _context = context;

    public async Task<PartyDirectoryEntry?> GetPartyAsync(PartyRef partyRef, CancellationToken cancellationToken = default)
    {
        var entries = await GetPartiesAsync([partyRef], cancellationToken);
        return entries.TryGetValue(partyRef, out var entry) ? entry : null;
    }

    public async Task<IReadOnlyDictionary<PartyRef, PartyDirectoryEntry>> GetPartiesAsync(
        IReadOnlyCollection<PartyRef> partyRefs, CancellationToken cancellationToken = default)
    {
        if (partyRefs.Count == 0)
            return new Dictionary<PartyRef, PartyDirectoryEntry>();

        var tenantId = partyRefs.First().TenantId;
        if (partyRefs.Any(r => !r.TenantId.Equals(tenantId)))
            throw new ArgumentException("All PartyRefs in a batch lookup must share the same tenant.", nameof(partyRefs));

        var ids = partyRefs.Select(r => r.PartyId).ToHashSet();
        var parties = await _context.Parties
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && ids.Contains(p.Id))
            .ToListAsync(cancellationToken);

        // Resolve any tombstones found to their canonical in one extra round-trip.
        var canonicalIds = parties
            .Where(p => p.MergedIntoPartyId is not null)
            .Select(p => p.MergedIntoPartyId!.Value)
            .Distinct()
            .ToList();

        var canonicals = canonicalIds.Count == 0
            ? []
            : await _context.Parties.AsNoTracking()
                .Where(p => p.TenantId == tenantId && canonicalIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

        var result = new Dictionary<PartyRef, PartyDirectoryEntry>();
        foreach (var requested in partyRefs)
        {
            var party = parties.SingleOrDefault(p => p.Id == requested.PartyId);
            if (party is null) continue;

            var resolved = party.MergedIntoPartyId is { } canonicalId
                ? canonicals.SingleOrDefault(p => p.Id == canonicalId)
                : party;
            if (resolved is null) continue;

            result[requested] = new PartyDirectoryEntry(requested, resolved.PartyType, resolved.Name, resolved.Surname, resolved.Email);
        }

        return result;
    }
}
