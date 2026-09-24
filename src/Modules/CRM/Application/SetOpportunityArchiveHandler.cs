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

public sealed record SetOpportunityArchiveCommand(
    TenantId TenantId,
    long OpportunityId,
    PrincipalRef Principal,
    long ExpectedVersion,
    bool Archive,
    bool ConfirmOpenArchive,
    long? RestoreStageId,
    string IdempotencyKey,
    Guid CorrelationId);

public sealed record SetOpportunityArchiveResult(long OpportunityId, long RowVersion, bool IsArchived, bool Replayed);

public sealed class SetOpportunityArchiveHandler(CrmDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "SetOpportunityArchive";
    private const string EventSource = "/enterprise/crm-sales";
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    public async Task<SetOpportunityArchiveResult> HandleAsync(SetOpportunityArchiveCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);
        var opportunity = await context.Opportunities.SingleOrDefaultAsync(o => o.TenantId == command.TenantId && o.Id == command.OpportunityId, cancellationToken)
            ?? throw new OpportunityNotFoundException(command.OpportunityId);

        var actor = new ActorContext(command.TenantId, command.Principal, command.CorrelationId);
        var resource = new ResourceDescriptor(nameof(Opportunity), opportunity.Id, opportunity.AssignedPrincipal);
        var action = command.Archive ? CrmActionKeys.OpportunityArchive : CrmActionKeys.OpportunityRestore;
        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(actor, new ActionKey(action), resource), cancellationToken);
        if (!decision.IsAllowed)
            throw new OpportunityAuthorizationDeniedException(action, decision.ReasonCode, decision.DenialStage, opportunity.Id);

        var hash = Hash(command);
        var existing = await context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(record =>
            record.TenantId == command.TenantId && record.PrincipalIssuer == command.Principal.Issuer
            && record.PrincipalSubject == command.Principal.Subject && record.Operation == Operation
            && record.IdempotencyKey == command.IdempotencyKey, cancellationToken);
        if (existing is not null)
            return await ReplayAsync(transaction, existing, hash, cancellationToken);

        if (opportunity.RowVersion != command.ExpectedVersion)
            throw new OpportunityConcurrencyConflictException(opportunity.Id, command.ExpectedVersion);

        long? replacementStage = null;
        if (command.Archive)
        {
            opportunity.Archive(command.ConfirmOpenArchive);
        }
        else
        {
            if (!opportunity.IsArchived)
                throw new InvalidOperationException("Opportunity is not archived.");
            if (opportunity.Status == OpportunityStatus.Open && opportunity.PipelineStageId is { } oldStage)
            {
                var oldStageActive = await context.PipelineStages.AsNoTracking().AnyAsync(stage =>
                    stage.Id == oldStage && stage.PipelineDefinitionVersionId == opportunity.PipelineDefinitionVersionId
                    && stage.IsActive && !stage.IsArchived, cancellationToken);
                if (!oldStageActive && command.RestoreStageId is null)
                    throw new OpportunityRestoreStageRequiredException();
                if (command.RestoreStageId is { } selectedStage)
                {
                    if (oldStageActive)
                        throw new OpportunityRestoreStageInvalidException(selectedStage);
                    var valid = await context.PipelineStages.AsNoTracking().AnyAsync(stage =>
                        stage.Id == selectedStage && stage.PipelineDefinitionVersionId == opportunity.PipelineDefinitionVersionId
                        && stage.IsActive && !stage.IsArchived, cancellationToken);
                    if (!valid)
                        throw new OpportunityRestoreStageInvalidException(selectedStage);
                    replacementStage = selectedStage;
                }
            }
            opportunity.Restore(replacementStage);
        }

        var payload = new ArchivePayload(opportunity.Id, opportunity.Status, opportunity.PipelineStageId,
            opportunity.IsArchived, opportunity.ArchivedAt, opportunity.RowVersion);
        var json = JsonSerializer.Serialize(payload);
        var eventType = command.Archive ? "enterprise.crmsales.opportunity.archived.v1" : "enterprise.crmsales.opportunity.restored.v1";
        var evidenceAction = command.Archive ? "Opportunity.Archive" : "Opportunity.Restore";
        context.OutboxMessages.Add(OutboxMessage.Create(command.TenantId, nameof(Opportunity), opportunity.Id,
            opportunity.RowVersion, eventType, EventSource, $"opportunities/{opportunity.Id}", command.CorrelationId, null, json));
        context.EvidenceRecords.Add(EvidenceRecord.Create(command.TenantId, nameof(Opportunity), opportunity.Id,
            opportunity.RowVersion, command.Principal, evidenceAction, json, command.CorrelationId));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, command.Principal, Operation,
            command.IdempotencyKey, hash, 200, json, Retention));

        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new OpportunityConcurrencyConflictException(opportunity.Id, command.ExpectedVersion);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            await transaction.RollbackAsync(cancellationToken);
            var winner = await context.IdempotencyRecords.AsNoTracking().SingleAsync(record =>
                record.TenantId == command.TenantId && record.PrincipalIssuer == command.Principal.Issuer
                && record.PrincipalSubject == command.Principal.Subject && record.Operation == Operation
                && record.IdempotencyKey == command.IdempotencyKey, cancellationToken);
            return await ReplayAsync(transaction, winner, hash, cancellationToken, commit: false);
        }

        await transaction.CommitAsync(cancellationToken);
        return new SetOpportunityArchiveResult(opportunity.Id, opportunity.RowVersion, opportunity.IsArchived, false);
    }

    private static async Task<SetOpportunityArchiveResult> ReplayAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        IdempotencyRecord existing, string hash, CancellationToken cancellationToken, bool commit = true)
    {
        if (existing.RequestHash != hash)
            throw new IdempotencyKeyReusedException(Operation, existing.IdempotencyKey);
        if (commit)
            await transaction.CommitAsync(cancellationToken);
        var payload = JsonSerializer.Deserialize<ArchivePayload>(existing.ResponsePayload)
            ?? throw new InvalidOperationException("Stored archive response is empty.");
        return new SetOpportunityArchiveResult(payload.OpportunityId, payload.RowVersion, payload.IsArchived, true);
    }

    private static string Hash(SetOpportunityArchiveCommand command)
    {
        var canonical = string.Create(CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.Principal}|{command.OpportunityId}|{command.ExpectedVersion}|{command.Archive}|{command.ConfirmOpenArchive}|{command.RestoreStageId}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed record ArchivePayload(long OpportunityId, OpportunityStatus Status, long? PipelineStageId,
        bool IsArchived, DateTimeOffset? ArchivedAt, long RowVersion);
}

public sealed class OpportunityRestoreStageRequiredException() : InvalidOperationException("An active pipeline stage must be selected before restoring this opportunity.");
public sealed class OpportunityRestoreStageInvalidException(long stageId) : InvalidOperationException($"Pipeline stage {stageId} is not an active stage in this opportunity's pipeline version.");
