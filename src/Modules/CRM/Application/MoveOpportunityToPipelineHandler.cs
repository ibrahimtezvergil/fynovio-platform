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

/// <summary>Doc 2026-09-27 §3.3/§5 item 5 — moves an open Opportunity to a different
/// PipelineDefinition entirely (not just a stage change within the same one, which stays
/// ChangePipelineStageHandler's job). The target stage is always chosen explicitly by the
/// caller; there is deliberately no auto-default.</summary>
public sealed class MoveOpportunityToPipelineHandler(CrmDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "MoveOpportunityToPipeline";
    private const string ActionKeyValue = "crm.opportunity.move_pipeline";
    private const string EventType = "enterprise.crmsales.opportunity.moved_pipeline.v1";
    private const string EventSource = "/enterprise/crm-sales";
    private const int SucceededStatus = 200;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    public async Task<MoveOpportunityToPipelineResult> HandleAsync(MoveOpportunityToPipelineCommand command, CancellationToken cancellationToken = default)
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
            return new MoveOpportunityToPipelineResult(command.OpportunityId, command.TargetPipelineDefinitionVersionId, command.TargetStageId, Replayed: true);
        }

        if (opportunity.RowVersion != command.ExpectedVersion)
            throw new OpportunityConcurrencyConflictException(opportunity.Id, command.ExpectedVersion);

        var targetVersion = await context.PipelineDefinitionVersions.AsNoTracking()
            .SingleOrDefaultAsync(v => v.TenantId == command.TenantId && v.Id == command.TargetPipelineDefinitionVersionId, cancellationToken)
            ?? throw new InvalidPipelineTransitionException(opportunity.Id, command.TargetStageId, "target pipeline version does not exist for this tenant");
        if (targetVersion.Status != PipelineVersionStatus.Published)
            throw new InvalidPipelineTransitionException(opportunity.Id, command.TargetStageId, "target pipeline version is not published");

        var targetStage = await context.PipelineStages.AsNoTracking()
            .SingleOrDefaultAsync(s => s.TenantId == command.TenantId && s.Id == command.TargetStageId, cancellationToken)
            ?? throw new InvalidPipelineTransitionException(opportunity.Id, command.TargetStageId, "stage does not exist for this tenant");
        if (targetStage.PipelineDefinitionVersionId != command.TargetPipelineDefinitionVersionId)
            throw new InvalidPipelineTransitionException(opportunity.Id, command.TargetStageId, "stage does not belong to the target pipeline version");
        if (!targetStage.IsActive)
            throw new InvalidPipelineTransitionException(opportunity.Id, command.TargetStageId, "stage is retired and not a valid target");
        if (targetStage.Kind != PipelineStageKind.Open)
            throw new InvalidPipelineTransitionException(opportunity.Id, command.TargetStageId, "Won and Lost stages can only be reached through the Win/Lose commands");

        opportunity.MoveToPipeline(command.TargetPipelineDefinitionVersionId, command.TargetStageId);

        var payload = new { opportunity.Id, command.TargetPipelineDefinitionVersionId, command.TargetStageId };
        var payloadJson = JsonSerializer.Serialize(payload);

        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
            EventType, EventSource, $"opportunities/{opportunity.Id}", command.CorrelationId, null, payloadJson));
        context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
            command.Principal, "Opportunity.MoveToPipeline", payloadJson, command.CorrelationId));
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
            return new MoveOpportunityToPipelineResult(command.OpportunityId, command.TargetPipelineDefinitionVersionId, command.TargetStageId, Replayed: true);
        }

        await transaction.CommitAsync(cancellationToken);
        return new MoveOpportunityToPipelineResult(opportunity.Id, command.TargetPipelineDefinitionVersionId, command.TargetStageId, Replayed: false);
    }

    private const string UniqueViolationSqlState = "23505";
    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState };

    private static string HashRequest(MoveOpportunityToPipelineCommand command)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.Principal}|{command.OpportunityId}|{command.ExpectedVersion}|{command.TargetPipelineDefinitionVersionId}|{command.TargetStageId}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
