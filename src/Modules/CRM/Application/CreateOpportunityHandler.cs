using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Contracts;
using CRM.Customization;
using CRM.Domain;
using CRM.Evidence;
using CRM.Idempotency;
using CRM.Outbox;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CRM.Application;

public sealed class CreateOpportunityHandler(CrmDbContext context, IAuthorizer authorizer, IPartyIdentityResolver partyResolver, IAuthorizedPrincipalDirectory? principalDirectory = null)
{
    private const string Operation = "CreateOpportunity";
    private const string ActionKeyValue = "crm.opportunity.create";
    private const string EventType = "enterprise.crmsales.opportunity.created.v1";
    private const string EventSource = "/enterprise/crm-sales";
    private const int SucceededStatus = 201;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    public async Task<CreateOpportunityResult> HandleAsync(CreateOpportunityCommand command, CancellationToken cancellationToken = default)
    {
        var actingPrincipal = command.CallerPrincipal ?? command.AssignedPrincipal;
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        // CREATE-shaped resource: Id is null, no owner yet (round-3 §11).
        var actor = new ActorContext(command.TenantId, actingPrincipal, command.CorrelationId);
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
                    && r.PrincipalIssuer == actingPrincipal.Issuer
                    && r.PrincipalSubject == actingPrincipal.Subject
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

        // The customer must exist in this tenant, and a merged one is stored as its survivor — a command
        // precondition, so it goes through the identity resolver, not the display-oriented directory. Resolved
        // AFTER the replay check (a replay must keep working once the party is later merged) and the request hash
        // stays over the INPUT ref (a merge between two attempts must not turn a retry into a key-reuse conflict).
        // The resolver reads through the MasterData context, outside this transaction: a party removed between
        // this read and the commit is not detected here (accepted TOCTOU for a reference precondition).
        var partyRef = await partyResolver.ResolveAsync(command.PartyRef, cancellationToken)
            ?? throw new PartyNotFoundException(command.PartyRef);

        var settings = await context.CrmSettings.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == command.TenantId, cancellationToken);
        var (assignedPrincipal, opportunityTypeId) = ResolveDefaults(settings, command.AssignedPrincipal);
        if (principalDirectory is not null && !await principalDirectory.IsPrincipalPermittedAsync(command.TenantId, assignedPrincipal,
                CrmAssignmentPolicy.RequiredAssigneeActions, cancellationToken))
            throw new PrincipalNotAssignableException(assignedPrincipal);

        var fieldDefinitions = await context.TenantFieldDefinitions.AsNoTracking()
            .Where(x => x.TenantId == command.TenantId && x.AggregateType == TenantFieldAggregateType.Opportunity)
            .ToListAsync(cancellationToken);
        var customFields = CustomFieldValues.Normalize(fieldDefinitions, command.CustomFields, existingJson: null);

        var opportunity = Opportunity.Create(
            command.TenantId, partyRef, assignedPrincipal, command.Currency, command.EstimatedAmount, opportunityTypeId, customFields);
        context.Opportunities.Add(opportunity);
        await context.SaveChangesAsync(cancellationToken); // assigns opportunity.Id

        var payload = new CreatedPayload(opportunity.Id, partyRef.PartyId, command.Currency, command.EstimatedAmount, opportunityTypeId);
        var payloadJson = JsonSerializer.Serialize(payload);

        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
            EventType, EventSource, $"opportunities/{opportunity.Id}", command.CorrelationId, null, payloadJson));
        context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
            actingPrincipal, "Opportunity.Create", payloadJson, command.CorrelationId));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId, actingPrincipal, Operation, command.IdempotencyKey, requestHash, SucceededStatus, payloadJson, IdempotencyRetention));

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
                    && record.PrincipalIssuer == actingPrincipal.Issuer
                    && record.PrincipalSubject == actingPrincipal.Subject
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
            $"{Operation}|{command.TenantId.Value}|{command.CallerPrincipal ?? command.AssignedPrincipal}|{command.AssignedPrincipal}|{command.PartyRef}|{command.Currency}|{command.EstimatedAmount}");
        // Appended only when present, so requests without custom fields keep their pre-existing hash.
        if (command.CustomFields is { } customFields)
            canonical += "|" + customFields.GetRawText();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static (PrincipalRef Assignee, long? OpportunityTypeId) ResolveDefaults(CRM.Domain.CrmSettings? settings, PrincipalRef requestedAssignee)
    {
        if (settings is null || settings.DefaultAssignmentMode == AssignmentMode.Manual)
            return (requestedAssignee, settings?.DefaultOpportunityTypeId);
        if (settings.DefaultAssignmentMode == AssignmentMode.DefaultPrincipal
            && settings.DefaultPrincipalIssuer is { } issuer && settings.DefaultPrincipalSubject is { } subject)
            return (new PrincipalRef(issuer, subject), settings.DefaultOpportunityTypeId);
        if (settings.DefaultAssignmentMode == AssignmentMode.Team)
            throw new CrmAssignmentProviderUnavailableException("Team assignment requires the Organization team directory.");
        if (settings.DefaultAssignmentMode == AssignmentMode.Territory)
            throw new CrmAssignmentProviderUnavailableException("Territory assignment requires the Organization territory directory.");
        throw new InvalidOperationException("CRM assignment configuration is invalid.");
    }
}

public sealed class CrmAssignmentProviderUnavailableException(string message) : InvalidOperationException(message);
