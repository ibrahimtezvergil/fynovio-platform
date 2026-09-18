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

/// <summary>The Phase 2 handler template every other command copies. Order (round 3 §4,
/// adapted by architecture plan §25): tenant-safe load (needed first so the resource's
/// current owner is known) → authorize → idempotency lookup/replay → expectedVersion
/// check → domain mutation → Evidence+Outbox+IdempotencyRecord in one SaveChangesAsync()
/// → commit. Replaces CompleteOpportunityHandler, whose idempotency-before-authorization
/// order this corrects (round-3-final §6.5).</summary>
public sealed class WinOpportunityHandler(CrmDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "WinOpportunity";
    private const string ActionKeyValue = "crm.opportunity.win";
    private const string EventType = "enterprise.crmsales.opportunity.won.v1";
    private const string EventSource = "/enterprise/crm-sales";
    private const int SucceededStatus = 200;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    public async Task<WinOpportunityResult> HandleAsync(WinOpportunityCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        var opportunity = await context.Opportunities
            .Include(o => o.Lines)
            .SingleOrDefaultAsync(o => o.Id == command.OpportunityId, cancellationToken)
            ?? throw new OpportunityNotFoundException(command.OpportunityId);

        var actor = new ActorContext(command.TenantId, command.Principal, command.CorrelationId);
        var resource = new ResourceDescriptor(nameof(Opportunity), opportunity.Id, opportunity.AssignedPrincipal);
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey(ActionKeyValue), resource), cancellationToken);
        if (!decision.IsAllowed)
            throw new OpportunityAuthorizationDeniedException(ActionKeyValue, decision.ReasonCode);

        var requestHash = HashRequest(command);
        var existing = await context.IdempotencyRecords
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
            var stored = JsonSerializer.Deserialize<WonPayload>(existing.ResponsePayload)
                ?? throw new InvalidOperationException("Stored idempotency response is empty.");
            return new WinOpportunityResult(stored.OpportunityId, stored.TotalAmount, Replayed: true);
        }

        if (opportunity.RowVersion != command.ExpectedVersion)
            throw new OpportunityConcurrencyConflictException(opportunity.Id, command.ExpectedVersion);

        opportunity.Win();

        var payload = new WonPayload(
            opportunity.Id,
            opportunity.TotalAmount ?? throw new InvalidOperationException("A won opportunity must carry a total."),
            opportunity.Currency);
        var payloadJson = JsonSerializer.Serialize(payload);

        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
            EventType, EventSource, $"opportunities/{opportunity.Id}", command.CorrelationId, null, payloadJson));

        context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
            command.Principal, "Opportunity.Win", payloadJson, command.CorrelationId));

        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId, command.Principal, Operation, command.IdempotencyKey, requestHash, SucceededStatus, payloadJson, IdempotencyRetention));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new OpportunityConcurrencyConflictException(opportunity.Id, command.ExpectedVersion);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Another request with the same idempotency key committed first between our
            // lookup and our SaveChanges — replay its result instead of inventing an
            // ad-hoc lock (architecture plan §13/§7A).
            await transaction.RollbackAsync(cancellationToken);
            var winner = await context.IdempotencyRecords.AsNoTracking().SingleAsync(
                record => record.TenantId == command.TenantId
                    && record.PrincipalIssuer == command.Principal.Issuer
                    && record.PrincipalSubject == command.Principal.Subject
                    && record.Operation == Operation
                    && record.IdempotencyKey == command.IdempotencyKey,
                cancellationToken);
            if (winner.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);
            var stored = JsonSerializer.Deserialize<WonPayload>(winner.ResponsePayload)
                ?? throw new InvalidOperationException("Stored idempotency response is empty.");
            return new WinOpportunityResult(stored.OpportunityId, stored.TotalAmount, Replayed: true);
        }

        await transaction.CommitAsync(cancellationToken);
        return new WinOpportunityResult(payload.OpportunityId, payload.TotalAmount, Replayed: false);
    }

    // PostgreSQL error code 23505 = unique_violation (Npgsql exposes SqlState as this raw
    // string, not a named constant — verify against the installed Npgsql version's
    // PostgresException.SqlState docs before relying on this if it's ever unclear).
    private const string UniqueViolationSqlState = "23505";

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState };

    private static string HashRequest(WinOpportunityCommand command)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.Principal}|{command.OpportunityId}|{command.ExpectedVersion}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
