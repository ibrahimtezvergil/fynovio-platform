using Contracts;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterData.Application;

public sealed class PartyDirectory : IPartyDirectory, IPartySearch
{
    public const int MaxSearchTake = 50;
    private const int MaxQueryLength = 100;

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

        return await InTenantAsync(tenantId, () => ResolveAsync(tenantId, partyRefs, cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyList<PartyDirectoryEntry>> SearchPartiesAsync(
        TenantId tenantId, string? query, int take, CancellationToken cancellationToken = default) =>
        InTenantAsync<IReadOnlyList<PartyDirectoryEntry>>(tenantId, async () =>
        {
            var parties = _context.Parties.AsNoTracking().Where(p => p.TenantId == tenantId && p.MergedIntoPartyId == null);

            if (!string.IsNullOrWhiteSpace(query))
            {
                var pattern = "%" + EscapeLike(query.Trim()) + "%";
                parties = parties.Where(p =>
                    EF.Functions.ILike(p.Name, pattern, "\\")
                    || (p.Surname != null && EF.Functions.ILike(p.Surname, pattern, "\\"))
                    || EF.Functions.ILike(p.Name + " " + (p.Surname ?? ""), pattern, "\\")
                    || (p.Email != null && EF.Functions.ILike(p.Email, pattern, "\\")));
            }

            var rows = await parties
                .OrderBy(p => p.Name).ThenBy(p => p.Surname).ThenBy(p => p.Id)
                .Take(Math.Clamp(take, 1, MaxSearchTake))
                .ToListAsync(cancellationToken);
            return rows.Select(p => new PartyDirectoryEntry(new PartyRef(tenantId, p.Id), p.PartyType, p.Name, p.Surname, p.Email)).ToList();
        }, cancellationToken);

    private async Task<IReadOnlyDictionary<PartyRef, PartyDirectoryEntry>> ResolveAsync(
        TenantId tenantId, IReadOnlyCollection<PartyRef> partyRefs, CancellationToken cancellationToken)
    {
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

    /// <summary>`masterdata.parties` is RLS-protected: without `app.tenant_id` the runtime role sees no rows at all,
    /// so every read here establishes the tenant first (inside its own transaction unless the caller holds one).</summary>
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

    private static string EscapeLike(string value)
    {
        var trimmed = value.Length > MaxQueryLength ? value[..MaxQueryLength] : value;
        return trimmed.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }
}
