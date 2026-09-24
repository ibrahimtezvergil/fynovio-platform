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

/// <summary>Validates the cross-aggregate facts Opportunity.ChangeStage() itself cannot
/// (architecture plan §7/§9 OD#4: unrestricted within the current pipeline version's
/// active stages — no transition matrix; §2.4: retired stages rejected as new targets
/// but never destroy historical references, which OpportunityConfiguration's
/// DeleteBehavior.Restrict FK already guarantees structurally).</summary>
public sealed class ChangePipelineStageHandler(CrmDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "ChangePipelineStage";
    private const string ActionKeyValue = "crm.opportunity.change_stage";
    private const string EventType = "enterprise.crmsales.opportunity.stage_changed.v1";
    private const string EventSource = "/enterprise/crm-sales";
    private const int SucceededStatus = 200;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    public async Task<ChangePipelineStageResult> HandleAsync(ChangePipelineStageCommand command, CancellationToken cancellationToken = default)
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
            var stored = JsonSerializer.Deserialize<StageChangedPayload>(existing.ResponsePayload)
                ?? throw new InvalidOperationException("Stored idempotency response is empty.");
            return new ChangePipelineStageResult(stored.OpportunityId, stored.ToStageId, Replayed: true);
        }

        if (opportunity.RowVersion != command.ExpectedVersion)
            throw new OpportunityConcurrencyConflictException(opportunity.Id, command.ExpectedVersion);

        var targetStage = await context.PipelineStages.AsNoTracking()
            .SingleOrDefaultAsync(s => s.TenantId == command.TenantId && s.Id == command.TargetStageId, cancellationToken)
            ?? throw new InvalidPipelineTransitionException(opportunity.Id, command.TargetStageId, "stage does not exist for this tenant");
        if (targetStage.PipelineDefinitionVersionId != opportunity.PipelineDefinitionVersionId)
            throw new InvalidPipelineTransitionException(opportunity.Id, command.TargetStageId, "stage does not belong to this opportunity's current pipeline version");
        if (!targetStage.IsActive)
            throw new InvalidPipelineTransitionException(opportunity.Id, command.TargetStageId, "stage is retired and not a valid target for new transitions");

        if (opportunity.PipelineDefinitionVersionId is { } currentVersionId)
        {
            var restrictTransitions = await context.PipelineDefinitionVersions.AsNoTracking()
                .Where(version => version.TenantId == command.TenantId && version.Id == currentVersionId)
                .Select(version => version.EnforceAllowedTransitions)
                .SingleAsync(cancellationToken);
            if (restrictTransitions && (opportunity.PipelineStageId is not { } transitionSourceStageId || !await context.PipelineStageTransitions.AnyAsync(edge =>
                    edge.TenantId == command.TenantId && edge.PipelineDefinitionVersionId == currentVersionId && edge.FromStageId == transitionSourceStageId && edge.ToStageId == command.TargetStageId,
                    cancellationToken)))
                throw new InvalidPipelineTransitionException(opportunity.Id, command.TargetStageId, "the configured transition is not allowed");
        }

        var fromStageId = opportunity.PipelineStageId;
        opportunity.ChangeStage(command.TargetStageId);

        var payload = new StageChangedPayload(opportunity.Id, fromStageId, command.TargetStageId);
        var payloadJson = JsonSerializer.Serialize(payload);

        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
            EventType, EventSource, $"opportunities/{opportunity.Id}", command.CorrelationId, null, payloadJson));
        context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
            command.Principal, "Opportunity.ChangeStage", payloadJson, command.CorrelationId));
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
            var stored = JsonSerializer.Deserialize<StageChangedPayload>(winner.ResponsePayload)
                ?? throw new InvalidOperationException("Stored idempotency response is empty.");
            return new ChangePipelineStageResult(stored.OpportunityId, stored.ToStageId, Replayed: true);
        }

        await transaction.CommitAsync(cancellationToken);
        return new ChangePipelineStageResult(opportunity.Id, command.TargetStageId, Replayed: false);
    }

    // PostgreSQL error code 23505 = unique_violation (Npgsql exposes SqlState as this raw
    // string, not a named constant — verify against the installed Npgsql version's
    // PostgresException.SqlState docs before relying on this if it's ever unclear).
    private const string UniqueViolationSqlState = "23505";

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState };

    private static string HashRequest(ChangePipelineStageCommand command)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.Principal}|{command.OpportunityId}|{command.ExpectedVersion}|{command.TargetStageId}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
