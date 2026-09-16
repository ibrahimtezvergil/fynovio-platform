using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MasterData.Domain;
using MasterData.Outbox;
using MasterData.Idempotency;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterData.Application;

public sealed class CreatePartyHandler
{
    private const string Operation = "CreateParty";
    private const string EventType = "enterprise.masterdata.party.created.v1";
    private const string EventSource = "/enterprise/master-data";
    private const int SucceededStatus = 200;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    private readonly MasterDataDbContext _context;

    public CreatePartyHandler(MasterDataDbContext context) => _context = context;

    public async Task<CreatePartyResult> HandleAsync(CreatePartyCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.SetTenantContextAsync(command.TenantId, cancellationToken);

        var requestHash = HashRequest(command);

        var existing = await _context.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.TenantId == command.TenantId
                    && record.Operation == Operation
                    && record.IdempotencyKey == command.IdempotencyKey,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);

            await transaction.CommitAsync(cancellationToken);
            var stored = JsonSerializer.Deserialize<CreatedPartyPayload>(existing.ResponsePayload)
                ?? throw new InvalidOperationException("Stored idempotency response is empty.");
            return new CreatePartyResult(stored.PartyId, Replayed: true);
        }

        var party = Party.Create(command.TenantId, command.PartyType, command.Name, command.Surname, command.Phone, command.Email);
        _context.Parties.Add(party);
        await _context.SaveChangesAsync(cancellationToken); // party.Id needs a round-trip before the outbox payload can reference it

        var payload = new CreatedPartyPayload(party.Id, party.PartyType);
        var payloadJson = JsonSerializer.Serialize(payload);

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
            payload: payloadJson));

        _context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId,
            new Contracts.PrincipalRef("system", Operation), // TODO Task 9: replace with the real caller principal once a caller exists
            Operation,
            command.IdempotencyKey,
            requestHash,
            SucceededStatus,
            payloadJson,
            IdempotencyRetention));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CreatePartyResult(party.Id, Replayed: false);
    }

    private static string HashRequest(CreatePartyCommand command)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.PartyType}|{command.Name}|{command.Surname}|{command.Phone}|{command.Email}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
