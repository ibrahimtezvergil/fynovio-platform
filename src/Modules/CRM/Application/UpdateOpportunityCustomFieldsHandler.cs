using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Contracts;
using CRM.Customization;
using CRM.Domain;
using CRM.Idempotency;
using CRM.Outbox;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CRM.Application;

/// <summary>Not risk-catalogued, so no evidence record (ADR decision 8); the outbox fact carries changed keys only,
/// never values, and is written only when something actually changed.</summary>
public sealed class UpdateOpportunityCustomFieldsHandler(CrmDbContext context, IAuthorizer authorizer, ISemanticDefinitionReader definitionReader)
{
    private const string Operation = "UpdateOpportunityCustomFields";
    private const string ActionKeyValue = "crm.opportunity.update_custom_fields";
    private const string EventType = "enterprise.crmsales.opportunity.custom_fields_changed.v1";
    private const string EventSource = "/enterprise/crm-sales";
    private const int SucceededStatus = 200;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    public async Task<UpdateOpportunityCustomFieldsResult> HandleAsync(UpdateOpportunityCustomFieldsCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 200)
            throw new ArgumentException("An idempotency key is required.", nameof(command.IdempotencyKey));

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
            return new UpdateOpportunityCustomFieldsResult(command.OpportunityId, Replayed: true);
        }

        if (opportunity.RowVersion != command.ExpectedVersion)
            throw new OpportunityConcurrencyConflictException(opportunity.Id, command.ExpectedVersion);

        // Checked up front so an archived record is refused even when the request would change nothing.
        if (opportunity.IsArchived)
            throw new InvalidOperationException("Archived opportunities cannot change their custom fields.");

        var definitions = await definitionReader.ListFieldsAsync(command.TenantId, OpportunityFields.OwnerContext, OpportunityFields.ObjectType, cancellationToken);
        var before = opportunity.CustomFields;
        var after = CustomFieldValues.Normalize(definitions, command.CustomFields, before);
        var changedKeys = CustomFieldValues.ChangedKeys(before, after);

        var payloadJson = JsonSerializer.Serialize(new CustomFieldsChangedPayload(opportunity.Id, changedKeys));
        if (changedKeys.Count > 0)
        {
            opportunity.ReplaceCustomFields(after);
            context.OutboxMessages.Add(OutboxMessage.Create(
                command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
                EventType, EventSource, $"opportunities/{opportunity.Id}", command.CorrelationId, null, payloadJson));
        }

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
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another request with the same idempotency key committed first — replay its result.
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
            return new UpdateOpportunityCustomFieldsResult(command.OpportunityId, Replayed: true);
        }

        await transaction.CommitAsync(cancellationToken);
        return new UpdateOpportunityCustomFieldsResult(opportunity.Id, Replayed: false);
    }

    private static string HashRequest(UpdateOpportunityCustomFieldsCommand command)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.Principal}|{command.OpportunityId}|{command.ExpectedVersion}|{command.CustomFields?.GetRawText()}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
