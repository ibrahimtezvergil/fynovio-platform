using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MasterData.Domain;
using MasterData.Evidence;
using MasterData.Idempotency;
using MasterData.Outbox;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterData.Application;

/// <summary>Single-hop resolution + tombstone repointing + external-identity
/// reassignment, all inside MasterData's own schema/transaction — see
/// docs/plans/2026-09-16-masterdata-party-foundation.md §5. Evidence is written here
/// (unlike CreateParty) because a merge is hard to undo and audit-worthy, the same risk
/// category doc 20 already treats money/authorization/cancellation transitions with.</summary>
public sealed class MergePartyHandler
{
    private const string Operation = "MergeParty";
    private const string EventType = "enterprise.masterdata.party.merged.v1";
    private const string EventSource = "/enterprise/master-data";
    private const int SucceededStatus = 200;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    private readonly MasterDataDbContext _context;

    public MergePartyHandler(MasterDataDbContext context) => _context = context;

    public async Task<MergePartyResult> HandleAsync(MergePartyCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.SetTenantContextAsync(command.TenantId, cancellationToken);

        var requestHash = HashRequest(command);

        var existing = await _context.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.TenantId == command.TenantId
                    && record.PrincipalIssuer == command.Principal.Issuer
                    && record.PrincipalSubject == command.Principal.Subject
                    && record.Operation == Operation
                    && record.IdempotencyKey == command.IdempotencyKey,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);

            await transaction.CommitAsync(cancellationToken);
            var stored = JsonSerializer.Deserialize<MergedPartyPayload>(existing.ResponsePayload)
                ?? throw new InvalidOperationException("Stored idempotency response is empty.");
            return new MergePartyResult(stored.SourcePartyId, stored.CanonicalPartyId, Replayed: true);
        }

        var source = await _context.Parties.SingleOrDefaultAsync(p => p.Id == command.SourcePartyId, cancellationToken)
            ?? throw new PartyNotFoundException(command.SourcePartyId);
        var requestedTarget = await _context.Parties.SingleOrDefaultAsync(p => p.Id == command.TargetPartyId, cancellationToken)
            ?? throw new PartyNotFoundException(command.TargetPartyId);

        // Single-hop invariant: resolve the requested target to its own canonical first
        // — Party.MergeInto refuses a target that is itself a tombstone, so this
        // resolution has to happen here, before calling it.
        var canonical = requestedTarget.MergedIntoPartyId is { } alreadyCanonicalId
            ? await _context.Parties.SingleAsync(p => p.Id == alreadyCanonicalId, cancellationToken)
            : requestedTarget;

        // Repoint every existing tombstone that pointed at `source` — source may itself
        // already be canonical for other, previously merged parties.
        var dependentTombstones = await _context.Parties
            .Where(p => p.MergedIntoPartyId == source.Id)
            .ToListAsync(cancellationToken);
        foreach (var tombstone in dependentTombstones)
            tombstone.RepointMergeTarget(canonical.Id);

        // Reassign external identities to the survivor — same-schema, same-transaction.
        var externalIdentities = await _context.PartyExternalIdentities
            .Where(e => e.PartyId == source.Id)
            .ToListAsync(cancellationToken);
        foreach (var identity in externalIdentities)
            identity.ReassignTo(canonical.Id);

        source.MergeInto(canonical);

        var payload = new MergedPartyPayload(source.Id, canonical.Id);
        var payloadJson = JsonSerializer.Serialize(payload);

        _context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId,
            aggregateType: nameof(Party),
            aggregateId: source.Id,
            aggregateVersion: 1,
            eventType: EventType,
            source: EventSource,
            subject: $"parties/{source.Id}",
            correlationId: command.CorrelationId,
            causationId: null,
            payload: payloadJson));

        _context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId,
            aggregateType: nameof(Party),
            aggregateId: source.Id,
            aggregateVersion: 1,
            principal: command.Principal,
            action: "Party.Merge",
            detail: payloadJson,
            correlationId: command.CorrelationId));

        _context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId,
            command.Principal,
            Operation,
            command.IdempotencyKey,
            requestHash,
            SucceededStatus,
            payloadJson,
            IdempotencyRetention));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new MergePartyResult(source.Id, canonical.Id, Replayed: false);
    }

    private static string HashRequest(MergePartyCommand command)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.Principal}|{command.SourcePartyId}|{command.TargetPartyId}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
