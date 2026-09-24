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

public sealed class LoseOpportunityHandler(CrmDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "LoseOpportunity";
    private const string ActionKeyValue = "crm.opportunity.lose";
    private const string EventType = "enterprise.crmsales.opportunity.lost.v1";
    private const string EventSource = "/enterprise/crm-sales";
    private const int SucceededStatus = 200;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    public async Task<LoseOpportunityResult> HandleAsync(LoseOpportunityCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        var opportunity = await context.Opportunities.SingleOrDefaultAsync(o => o.Id == command.OpportunityId, cancellationToken)
            ?? throw new OpportunityNotFoundException(command.OpportunityId);

        var actor = new ActorContext(command.TenantId, command.Principal, command.CorrelationId);
        var resource = new ResourceDescriptor(nameof(Opportunity), opportunity.Id, opportunity.AssignedPrincipal);
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey(ActionKeyValue), resource), cancellationToken);
        if (!decision.IsAllowed)
            throw new OpportunityAuthorizationDeniedException(ActionKeyValue, decision.ReasonCode, decision.DenialStage, opportunity.Id);

        var requestHash = HashRequest(command);
        var existing = await context.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                r => r.TenantId == command.TenantId && r.PrincipalIssuer == command.Principal.Issuer
                    && r.PrincipalSubject == command.Principal.Subject && r.Operation == Operation
                    && r.IdempotencyKey == command.IdempotencyKey,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);

            await transaction.CommitAsync(cancellationToken);
            return new LoseOpportunityResult(command.OpportunityId, Replayed: true);
        }

        if (opportunity.RowVersion != command.ExpectedVersion)
            throw new OpportunityConcurrencyConflictException(opportunity.Id, command.ExpectedVersion);

        var requireLostReason = await context.CrmSettings.AsNoTracking()
            .Where(settings => settings.TenantId == command.TenantId)
            .Select(settings => (bool?)settings.RequireLostReason)
            .SingleOrDefaultAsync(cancellationToken) ?? false;
        var activeReasons = await context.LostReasons.AsNoTracking().Where(x => x.TenantId == command.TenantId && x.Status == ConfigurationStatus.Active).ToListAsync(cancellationToken);
        var matchedReason = activeReasons.FirstOrDefault(x => string.Equals(x.Key, command.LostReason, StringComparison.OrdinalIgnoreCase))
            ?? activeReasons.FirstOrDefault(x => string.Equals(x.Name, command.LostReason, StringComparison.OrdinalIgnoreCase));
        var resolvedReason = activeReasons.Count == 0 ? command.LostReason : matchedReason?.Name;
        if (requireLostReason && string.IsNullOrWhiteSpace(resolvedReason))
            throw new ArgumentException("A lost reason is required by this tenant's CRM settings.", nameof(command));
        if (activeReasons.Count > 0 && resolvedReason is null)
            throw new ArgumentException("Choose an active lost reason configured by this tenant.", nameof(command));

        opportunity.Lose(resolvedReason ?? command.LostReason);

        // Written for audit/outbox purposes only — the idempotency-replay branches below
        // rebuild the result from command fields instead of deserializing this back, since
        // LoseOpportunityResult carries nothing server-computed beyond what's in the command.
        var payload = new LostPayload(opportunity.Id, resolvedReason ?? command.LostReason);
        var payloadJson = JsonSerializer.Serialize(payload);

        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
            EventType, EventSource, $"opportunities/{opportunity.Id}", command.CorrelationId, null, payloadJson));
        context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
            command.Principal, "Opportunity.Lose", payloadJson, command.CorrelationId));
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
            return new LoseOpportunityResult(command.OpportunityId, Replayed: true);
        }

        await transaction.CommitAsync(cancellationToken);
        return new LoseOpportunityResult(opportunity.Id, Replayed: false);
    }

    // PostgreSQL error code 23505 = unique_violation (Npgsql exposes SqlState as this raw
    // string, not a named constant — verify against the installed Npgsql version's
    // PostgresException.SqlState docs before relying on this if it's ever unclear).
    private const string UniqueViolationSqlState = "23505";

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState };

    private static string HashRequest(LoseOpportunityCommand command)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.Principal}|{command.OpportunityId}|{command.ExpectedVersion}|{command.LostReason}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
