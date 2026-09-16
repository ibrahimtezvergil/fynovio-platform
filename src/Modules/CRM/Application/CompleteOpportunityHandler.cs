using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CRM.Domain;
using CRM.Evidence;
using CRM.Idempotency;
using CRM.Outbox;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

/// <summary>State, outbox, evidence and the idempotency record are written by one
/// SaveChanges() inside one transaction (AGENTS.md "Enforcement Scope"; doc 17 §6 item 1),
/// with the RLS tenant context set on that same transaction. Two concurrent first attempts
/// with the same key both reach SaveChanges; the idempotency primary key lets only one
/// commit, and the loser's transaction rolls back with its domain change.</summary>
public sealed class CompleteOpportunityHandler
{
    private const string Operation = "CompleteOpportunity";
    private const string EventType = "enterprise.crmsales.opportunity.completed.v1";
    private const string EventSource = "/enterprise/crm-sales";
    private const int SucceededStatus = 200;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    private readonly CrmDbContext _context;

    public CompleteOpportunityHandler(CrmDbContext context) => _context = context;

    public async Task<CompleteOpportunityResult> HandleAsync(
        CompleteOpportunityCommand command,
        CancellationToken cancellationToken = default)
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
            var stored = JsonSerializer.Deserialize<CompletedPayload>(existing.ResponsePayload)
                ?? throw new InvalidOperationException("Stored idempotency response is empty.");
            return new CompleteOpportunityResult(stored.OpportunityId, stored.TotalAmount, Replayed: true);
        }

        var opportunity = await _context.Opportunities
            .Include(o => o.Lines)
            .SingleOrDefaultAsync(o => o.Id == command.OpportunityId, cancellationToken)
            ?? throw new OpportunityNotFoundException(command.OpportunityId);

        opportunity.Win();

        var payload = new CompletedPayload(
            opportunity.Id,
            opportunity.TotalAmount ?? throw new InvalidOperationException("A completed opportunity must carry a total."),
            opportunity.Currency);
        var payloadJson = JsonSerializer.Serialize(payload);

        _context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId,
            aggregateType: nameof(Opportunity),
            aggregateId: opportunity.Id,
            aggregateVersion: opportunity.RowVersion,
            eventType: EventType,
            source: EventSource,
            subject: $"opportunities/{opportunity.Id}",
            correlationId: command.CorrelationId,
            causationId: null,
            payload: payloadJson));

        _context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId,
            aggregateType: nameof(Opportunity),
            aggregateId: opportunity.Id,
            aggregateVersion: opportunity.RowVersion,
            principal: command.Principal,
            action: "Opportunity.Win",
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

        return new CompleteOpportunityResult(payload.OpportunityId, payload.TotalAmount, Replayed: false);
    }

    private static string HashRequest(CompleteOpportunityCommand command)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.Principal}|{command.OpportunityId}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
