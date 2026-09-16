using MasterData.Domain;
using MasterData.Outbox;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterData.Application;

/// <summary>No separate IdempotencyRecord — PartyExternalIdentity's own
/// (tenant_id, source_instance_ref, external_type, external_id) uniqueness already
/// makes this naturally idempotent: calling it twice with the same external reference
/// resolves to the same party both times. See this plan's Task 8 intro note.</summary>
public sealed class ResolveOrCreatePartyHandler
{
    private const string EventType = "enterprise.masterdata.party.created.v1";
    private const string EventSource = "/enterprise/master-data";

    private readonly MasterDataDbContext _context;

    public ResolveOrCreatePartyHandler(MasterDataDbContext context) => _context = context;

    public async Task<ResolveOrCreatePartyResult> HandleAsync(ResolveOrCreatePartyCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.SetTenantContextAsync(command.TenantId, cancellationToken);

        var externalType = command.ExternalType ?? string.Empty; // matches the computed column's COALESCE(external_type, '')
        var existing = await _context.PartyExternalIdentities
            .SingleOrDefaultAsync(
                e => e.TenantId == command.TenantId
                    && e.SourceInstanceRef == command.SourceInstanceRef
                    && (e.ExternalType ?? string.Empty) == externalType
                    && e.ExternalId == command.ExternalId,
                cancellationToken);

        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new ResolveOrCreatePartyResult(existing.PartyId, Created: false);
        }

        var party = Party.Create(command.TenantId, command.PartyType, command.Name);
        _context.Parties.Add(party);
        await _context.SaveChangesAsync(cancellationToken);

        _context.PartyExternalIdentities.Add(PartyExternalIdentity.Create(
            command.TenantId, party.Id, command.Provider, command.SourceInstanceRef, command.ExternalType, command.ExternalId));

        _context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId,
            aggregateType: nameof(Party),
            aggregateId: party.Id,
            aggregateVersion: 1,
            eventType: EventType,
            source: EventSource,
            subject: $"parties/{party.Id}",
            correlationId: command.CorrelationId,
            causationId: null,
            payload: $"{{\"partyId\":{party.Id}}}"));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ResolveOrCreatePartyResult(party.Id, Created: true);
    }
}
