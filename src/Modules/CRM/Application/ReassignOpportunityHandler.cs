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

public sealed class ReassignOpportunityHandler(CrmDbContext context, IAuthorizer authorizer, IAuthorizedPrincipalDirectory directory)
{
    private const string Operation = "ReassignOpportunity";
    private const string ActionKeyValue = "crm.opportunity.reassign";
    private const string EventType = "enterprise.crmsales.opportunity.reassigned.v1";
    private const string EventSource = "/enterprise/crm-sales";
    private const int SucceededStatus = 200;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    public async Task<ReassignOpportunityResult> HandleAsync(ReassignOpportunityCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        var opportunity = await context.Opportunities.SingleOrDefaultAsync(o => o.Id == command.OpportunityId, cancellationToken)
            ?? throw new OpportunityNotFoundException(command.OpportunityId);

        var previousOwner = opportunity.AssignedPrincipal;
        var actor = new ActorContext(command.TenantId, command.Principal, command.CorrelationId);
        var resource = new ResourceDescriptor(nameof(Opportunity), opportunity.Id, previousOwner);
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
            return new ReassignOpportunityResult(command.OpportunityId, Replayed: true);
        }

        // Server-side authority over the target: the UI offers only permitted assignees, but a client can send anyone.
        if (!await directory.IsPrincipalPermittedAsync(
                command.TenantId, command.NewAssignedPrincipal, CrmAssignmentPolicy.RequiredAssigneeActions, cancellationToken))
            throw new PrincipalNotAssignableException(command.NewAssignedPrincipal);

        if (opportunity.RowVersion != command.ExpectedVersion)
            throw new OpportunityConcurrencyConflictException(opportunity.Id, command.ExpectedVersion);

        opportunity.Reassign(command.NewAssignedPrincipal);

        // Written for audit/outbox purposes only — the idempotency-replay branches below
        // rebuild the result from command fields instead of deserializing this back, since
        // ReassignOpportunityResult carries nothing server-computed beyond what's in the command.
        var payload = new ReassignedPayload(opportunity.Id, previousOwner.ToString(), command.NewAssignedPrincipal.ToString());
        var payloadJson = JsonSerializer.Serialize(payload);

        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
            EventType, EventSource, $"opportunities/{opportunity.Id}", command.CorrelationId, null, payloadJson));
        context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
            command.Principal, "Opportunity.Reassign", payloadJson, command.CorrelationId));
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
            return new ReassignOpportunityResult(command.OpportunityId, Replayed: true);
        }

        await transaction.CommitAsync(cancellationToken);
        return new ReassignOpportunityResult(opportunity.Id, Replayed: false);
    }

    // PostgreSQL error code 23505 = unique_violation (Npgsql exposes SqlState as this raw
    // string, not a named constant — verify against the installed Npgsql version's
    // PostgresException.SqlState docs before relying on this if it's ever unclear).
    private const string UniqueViolationSqlState = "23505";

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState };

    private static string HashRequest(ReassignOpportunityCommand command)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.Principal}|{command.OpportunityId}|{command.ExpectedVersion}|{command.NewAssignedPrincipal}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
