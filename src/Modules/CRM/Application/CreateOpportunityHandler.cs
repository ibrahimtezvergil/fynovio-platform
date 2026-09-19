using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Contracts;
using CRM.Domain;
using CRM.Evidence;
using CRM.Idempotency;
using CRM.Outbox;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CRM.Application;

public sealed class CreateOpportunityHandler(CrmDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "CreateOpportunity";
    private const string ActionKeyValue = "crm.opportunity.create";
    private const string EventType = "enterprise.crmsales.opportunity.created.v1";
    private const string EventSource = "/enterprise/crm-sales";
    private const int SucceededStatus = 201;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    public async Task<CreateOpportunityResult> HandleAsync(CreateOpportunityCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        // CREATE-shaped resource: Id is null, no owner yet (round-3 §11).
        var actor = new ActorContext(command.TenantId, command.AssignedPrincipal, command.CorrelationId);
        var resource = new ResourceDescriptor(nameof(Opportunity), null, null);
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey(ActionKeyValue), resource), cancellationToken);
        if (!decision.IsAllowed)
            throw new OpportunityAuthorizationDeniedException(ActionKeyValue, decision.ReasonCode, decision.DenialStage);

        var requestHash = HashRequest(command);
        var existing = await context.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                r => r.TenantId == command.TenantId
                    && r.PrincipalIssuer == command.AssignedPrincipal.Issuer
                    && r.PrincipalSubject == command.AssignedPrincipal.Subject
                    && r.Operation == Operation
                    && r.IdempotencyKey == command.IdempotencyKey,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);

            await transaction.CommitAsync(cancellationToken);
            var stored = JsonSerializer.Deserialize<CreatedPayload>(existing.ResponsePayload)
                ?? throw new InvalidOperationException("Stored idempotency response is empty.");
            return new CreateOpportunityResult(stored.OpportunityId, Replayed: true);
        }

        var opportunity = Opportunity.Create(
            command.TenantId, command.PartyRef, command.AssignedPrincipal, command.Currency, command.EstimatedAmount);
        context.Opportunities.Add(opportunity);
        await context.SaveChangesAsync(cancellationToken); // assigns opportunity.Id

        var payload = new CreatedPayload(opportunity.Id, command.PartyRef.PartyId, command.Currency, command.EstimatedAmount);
        var payloadJson = JsonSerializer.Serialize(payload);

        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
            EventType, EventSource, $"opportunities/{opportunity.Id}", command.CorrelationId, null, payloadJson));
        context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
            command.AssignedPrincipal, "Opportunity.Create", payloadJson, command.CorrelationId));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId, command.AssignedPrincipal, Operation, command.IdempotencyKey, requestHash, SucceededStatus, payloadJson, IdempotencyRetention));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Another request with the same idempotency key committed first between our
            // lookup and our SaveChanges — replay its result instead of inventing an
            // ad-hoc lock (architecture plan §13/§7A).
            await transaction.RollbackAsync(cancellationToken);
            var winner = await context.IdempotencyRecords.AsNoTracking().SingleAsync(
                record => record.TenantId == command.TenantId
                    && record.PrincipalIssuer == command.AssignedPrincipal.Issuer
                    && record.PrincipalSubject == command.AssignedPrincipal.Subject
                    && record.Operation == Operation
                    && record.IdempotencyKey == command.IdempotencyKey,
                cancellationToken);
            if (winner.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);
            var stored = JsonSerializer.Deserialize<CreatedPayload>(winner.ResponsePayload)
                ?? throw new InvalidOperationException("Stored idempotency response is empty.");
            return new CreateOpportunityResult(stored.OpportunityId, Replayed: true);
        }

        await transaction.CommitAsync(cancellationToken);
        return new CreateOpportunityResult(opportunity.Id, Replayed: false);
    }

    // PostgreSQL error code 23505 = unique_violation (Npgsql exposes SqlState as this raw
    // string, not a named constant — verify against the installed Npgsql version's
    // PostgresException.SqlState docs before relying on this if it's ever unclear).
    private const string UniqueViolationSqlState = "23505";

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState };

    private static string HashRequest(CreateOpportunityCommand command)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.AssignedPrincipal}|{command.PartyRef}|{command.Currency}|{command.EstimatedAmount}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
