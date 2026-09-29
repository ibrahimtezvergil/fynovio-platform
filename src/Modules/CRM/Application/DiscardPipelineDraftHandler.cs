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

public sealed record DiscardPipelineDraftCommand(TenantId TenantId, PrincipalRef Principal, long PipelineDefinitionId, long VersionId,
    string IdempotencyKey, Guid CorrelationId);
public sealed record DiscardPipelineDraftResult(long PipelineDefinitionId, long VersionId, bool Replayed);

/// <summary>
/// Throws an unpublished draft away. The version is archived, not deleted (the audit trail keeps it), and nothing a
/// published version or an opportunity points at is touched — a draft is never referenced by either.
/// </summary>
public sealed class DiscardPipelineDraftHandler(CrmDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "DiscardPipelineDraft";

    public async Task<DiscardPipelineDraftResult> HandleAsync(DiscardPipelineDraftCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);
        await GetCrmSettingsHandler.AuthorizeAsync(command.TenantId, command.Principal, command.CorrelationId, CrmActionKeys.SettingsUpdate, authorizer, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command with { IdempotencyKey = string.Empty, CorrelationId = Guid.Empty }))));
        var existing = await FindAsync(command, cancellationToken);
        if (existing is not null) return Replay(existing, hash, command.IdempotencyKey);

        var definition = await context.PipelineDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == command.TenantId && x.Id == command.PipelineDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException("Pipeline was not found.");
        var version = await context.PipelineDefinitionVersions.SingleOrDefaultAsync(x => x.TenantId == command.TenantId && x.Id == command.VersionId && x.PipelineDefinitionId == definition.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Pipeline version was not found.");
        // Someone published or replaced this draft since the caller loaded it: the caller's view is stale.
        if (version.Status != PipelineVersionStatus.Draft) throw new CrmSettingsConcurrencyConflictException();
        version.ArchiveDraft();

        var result = new DiscardPipelineDraftResult(definition.Id, version.Id, false);
        context.OutboxMessages.Add(OutboxMessage.Create(command.TenantId, nameof(PipelineDefinition), definition.Id, definition.RowVersion,
            "enterprise.crm.pipeline.draft_discarded.v1", "crm", $"pipelines/{definition.Id}/versions/{version.Id}", command.CorrelationId, null,
            JsonSerializer.Serialize(new { pipelineDefinitionId = definition.Id, versionId = version.Id, versionNumber = version.VersionNumber })));
        context.EvidenceRecords.Add(EvidenceRecord.Create(command.TenantId, nameof(PipelineDefinition), definition.Id, definition.RowVersion,
            command.Principal, "CrmPipeline.DiscardDraft", JsonSerializer.Serialize(new { versionId = version.Id }), command.CorrelationId));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, command.Principal, Operation, command.IdempotencyKey, hash, 200, JsonSerializer.Serialize(result), TimeSpan.FromDays(1)));
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException || exception is DbUpdateException { InnerException: PostgresException { ConstraintName: "pk_idempotency_records" } })
        {
            await transaction.RollbackAsync(cancellationToken);
            var winner = await FindAsync(command, cancellationToken);
            if (winner is not null) return Replay(winner, hash, command.IdempotencyKey);
            if (exception is DbUpdateConcurrencyException) throw new CrmSettingsConcurrencyConflictException();
            throw;
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private Task<IdempotencyRecord?> FindAsync(DiscardPipelineDraftCommand command, CancellationToken cancellationToken) =>
        context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == command.TenantId && x.PrincipalIssuer == command.Principal.Issuer
            && x.PrincipalSubject == command.Principal.Subject && x.Operation == Operation && x.IdempotencyKey == command.IdempotencyKey, cancellationToken);

    private static DiscardPipelineDraftResult Replay(IdempotencyRecord record, string hash, string key)
    {
        if (record.RequestHash != hash) throw new IdempotencyKeyReusedException(Operation, key);
        var replay = JsonSerializer.Deserialize<DiscardPipelineDraftResult>(record.ResponsePayload) ?? throw new InvalidOperationException("Stored discard response is empty.");
        return replay with { Replayed = true };
    }
}
