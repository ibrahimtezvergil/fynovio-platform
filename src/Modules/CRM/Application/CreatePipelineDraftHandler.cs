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

public sealed class CreatePipelineDraftHandler(CrmDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "CreatePipelineDraft";

    public async Task<CreatePipelineDraftResult> HandleAsync(CreatePipelineDraftCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);
        await GetCrmSettingsHandler.AuthorizeAsync(command.TenantId, command.Principal, command.CorrelationId, CrmActionKeys.SettingsUpdate, authorizer, cancellationToken);
        var requestHash = Hash(command);
        var prior = await FindIdempotencyAsync(command.TenantId, command.Principal, command.IdempotencyKey, cancellationToken);
        if (prior is not null) return Replay(prior, requestHash);

        PipelineDefinition definition;
        if (command.PipelineDefinitionId is { } definitionId)
        {
            definition = await context.PipelineDefinitions.SingleOrDefaultAsync(x => x.TenantId == command.TenantId && x.Id == definitionId, cancellationToken)
                ?? throw new KeyNotFoundException("Pipeline was not found.");
            if (definition.RowVersion != command.ExpectedRowVersion) throw new CrmSettingsConcurrencyConflictException();
            definition.Rename(command.Name);
        }
        else
        {
            if (command.ExpectedRowVersion != 0) throw new CrmSettingsConcurrencyConflictException();
            definition = PipelineDefinition.Create(command.TenantId, command.Name);
            context.Entry(definition).Property(x => x.Id).CurrentValue = await context.AllocateConfigurationIdAsync<PipelineDefinition>(cancellationToken);
            context.PipelineDefinitions.Add(definition);
        }

        var latestNumber = await context.PipelineDefinitionVersions.Where(x => x.TenantId == command.TenantId && x.PipelineDefinitionId == definition.Id)
            .Select(x => (int?)x.VersionNumber).MaxAsync(cancellationToken) ?? 0;
        if (latestNumber != command.ExpectedLatestVersionNumber) throw new CrmSettingsConcurrencyConflictException();
        var priorDrafts = await context.PipelineDefinitionVersions.Where(x => x.TenantId == command.TenantId && x.PipelineDefinitionId == definition.Id && x.Status == PipelineVersionStatus.Draft).ToListAsync(cancellationToken);
        foreach (var draft in priorDrafts) draft.ArchiveDraft();

        var version = definition.AddVersion(latestNumber + 1);
        version.SetTransitionMode(command.EnforceAllowedTransitions);
        context.Entry(version).Property(x => x.Id).CurrentValue = await context.AllocateConfigurationIdAsync<PipelineDefinitionVersion>(cancellationToken);
        context.PipelineDefinitionVersions.Add(version);

        var stages = command.Stages.OrderBy(x => x.SortOrder).Select(x => version.AddStage(x.Name.Trim(), x.SortOrder)).ToList();
        foreach (var (stage, input) in stages.Zip(command.Stages.OrderBy(x => x.SortOrder)))
        {
            if (input.IsArchived) stage.Archive();
            else if (!input.IsActive) stage.Deactivate();
            context.Entry(stage).Property(x => x.Id).CurrentValue = await context.AllocateConfigurationIdAsync<PipelineStage>(cancellationToken);
        }
        version.MarkEntry(stages[command.Stages.OrderBy(x => x.SortOrder).ToList().FindIndex(x => x.IsEntry)]);

        var systemSortOrder = stages.Max(s => s.SortOrder) + 10;
        var wonStage = version.AddWonStage("Won", systemSortOrder);
        var lostStage = version.AddLostStage("Lost", systemSortOrder + 10);
        context.Entry(wonStage).Property(x => x.Id).CurrentValue = await context.AllocateConfigurationIdAsync<PipelineStage>(cancellationToken);
        context.Entry(lostStage).Property(x => x.Id).CurrentValue = await context.AllocateConfigurationIdAsync<PipelineStage>(cancellationToken);

        stages.Add(wonStage);
        stages.Add(lostStage);
        context.PipelineStages.AddRange(stages);

        var stagesByName = stages.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var edge in command.AllowedTransitions)
            context.PipelineStageTransitions.Add(PipelineStageTransition.Create(command.TenantId, version.Id, stagesByName[edge.FromStageName.Trim()].Id, stagesByName[edge.ToStageName.Trim()].Id));

        var result = new CreatePipelineDraftResult(definition.Id, version.Id, version.VersionNumber, definition.RowVersion, false);
        var payload = JsonSerializer.Serialize(result);
        context.OutboxMessages.Add(OutboxMessage.Create(command.TenantId, nameof(PipelineDefinition), definition.Id, definition.RowVersion,
            "enterprise.crm.pipeline.draft_created.v1", "crm", $"pipelines/{definition.Id}/versions/{version.Id}", command.CorrelationId, null,
            JsonSerializer.Serialize(new { pipelineDefinitionId = definition.Id, versionId = version.Id, versionNumber = version.VersionNumber })));
        context.EvidenceRecords.Add(EvidenceRecord.Create(command.TenantId, nameof(PipelineDefinition), definition.Id, definition.RowVersion,
            command.Principal, "CrmPipeline.CreateDraft", JsonSerializer.Serialize(new { versionId = version.Id, stageCount = stages.Count }), command.CorrelationId));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, command.Principal, Operation, command.IdempotencyKey,
            requestHash, 201, payload, TimeSpan.FromDays(1)));
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            var winner = await context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == command.TenantId
                && x.PrincipalIssuer == command.Principal.Issuer && x.PrincipalSubject == command.Principal.Subject
                && x.Operation == Operation && x.IdempotencyKey == command.IdempotencyKey, cancellationToken);
            if (winner is not null) return Replay(winner, requestHash);
            throw new CrmSettingsConcurrencyConflictException();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { ConstraintName: "pk_idempotency_records" })
        {
            await transaction.RollbackAsync(cancellationToken);
            var winner = await context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == command.TenantId
                && x.PrincipalIssuer == command.Principal.Issuer && x.PrincipalSubject == command.Principal.Subject
                && x.Operation == Operation && x.IdempotencyKey == command.IdempotencyKey, cancellationToken);
            if (winner is null) throw;
            return Replay(winner, requestHash);
        }
        catch (DbUpdateException exception) when (IsPipelineNameViolation(exception))
        {
            throw new CrmPipelineNameConflictException();
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static void Validate(CreatePipelineDraftCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Trim().Length > 100) throw new ArgumentException("Pipeline name must be 1–100 characters.");
        if (command.Stages is not { Count: > 0 and <= 50 } || command.Stages.Count(x => x.IsEntry) != 1) throw new ArgumentException("A pipeline draft needs 1–50 stages and exactly one explicit entry stage.");
        if (command.Stages.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.Name.Trim().Length > 100 || x.SortOrder < 0 || x.IsArchived && x.IsActive)) throw new ArgumentException("Stage names, lifecycle and sort orders are invalid.");
        if (command.Stages.Select(x => x.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != command.Stages.Count || command.Stages.Select(x => x.SortOrder).Distinct().Count() != command.Stages.Count) throw new ArgumentException("Stage names and sort orders must be unique.");
        var active = command.Stages.Where(x => x.IsActive).Select(x => x.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!active.Contains(command.Stages.Single(x => x.IsEntry).Name.Trim())) throw new ArgumentException("The entry stage must be active.");
        var names = command.Stages.Select(x => x.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (command.AllowedTransitions.Any(x => !names.Contains(x.FromStageName.Trim()) || !names.Contains(x.ToStageName.Trim()) || !active.Contains(x.FromStageName.Trim()) || !active.Contains(x.ToStageName.Trim()) || string.Equals(x.FromStageName, x.ToStageName, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Allowed transitions must connect two distinct active stages in this draft.");
        if (command.AllowedTransitions.Select(x => (x.FromStageName.ToUpperInvariant(), x.ToStageName.ToUpperInvariant())).Distinct().Count() != command.AllowedTransitions.Count) throw new ArgumentException("Allowed transitions must be unique.");
    }

    private async Task<IdempotencyRecord?> FindIdempotencyAsync(TenantId tenant, PrincipalRef principal, string key, CancellationToken ct) =>
        await context.IdempotencyRecords.SingleOrDefaultAsync(x => x.TenantId == tenant && x.PrincipalIssuer == principal.Issuer
            && x.PrincipalSubject == principal.Subject && x.Operation == Operation && x.IdempotencyKey == key, ct);

    private static string Hash(CreatePipelineDraftCommand command) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command with { IdempotencyKey = string.Empty, CorrelationId = Guid.Empty }))));
    private static bool IsPipelineNameViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { ConstraintName: "ix_pipeline_definitions_tenant_id_name" };
    private static CreatePipelineDraftResult Replay(IdempotencyRecord record, string requestHash)
    {
        if (record.RequestHash != requestHash) throw new IdempotencyKeyReusedException(Operation, record.IdempotencyKey);
        var response = JsonSerializer.Deserialize<CreatePipelineDraftResult>(record.ResponsePayload) ?? throw new InvalidOperationException("Stored pipeline response is empty.");
        return response with { Replayed = true };
    }
}

public sealed class CrmPipelineNameConflictException() : InvalidOperationException("A pipeline with this name already exists in the tenant.");

public sealed class PublishPipelineVersionHandler(CrmDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "PublishPipelineVersion";

    public async Task<PublishPipelineVersionResult> HandleAsync(PublishPipelineVersionCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);
        await GetCrmSettingsHandler.AuthorizeAsync(command.TenantId, command.Principal, command.CorrelationId, CrmActionKeys.SettingsUpdate, authorizer, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command with { IdempotencyKey = string.Empty, CorrelationId = Guid.Empty }))));
        var existing = await context.IdempotencyRecords.SingleOrDefaultAsync(x => x.TenantId == command.TenantId && x.PrincipalIssuer == command.Principal.Issuer
            && x.PrincipalSubject == command.Principal.Subject && x.Operation == Operation && x.IdempotencyKey == command.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != hash) throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);
            var replay = JsonSerializer.Deserialize<PublishPipelineVersionResult>(existing.ResponsePayload) ?? throw new InvalidOperationException("Stored publish response is empty.");
            return replay with { Replayed = true };
        }

        var definition = await context.PipelineDefinitions.SingleOrDefaultAsync(x => x.TenantId == command.TenantId && x.Id == command.PipelineDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException("Pipeline was not found.");
        if (definition.RowVersion != command.ExpectedPipelineRowVersion) throw new CrmSettingsConcurrencyConflictException();
        if (!definition.IsActive || definition.IsArchived) throw new InvalidOperationException("An inactive or archived pipeline cannot be published.");
        var version = await context.PipelineDefinitionVersions.SingleOrDefaultAsync(x => x.TenantId == command.TenantId && x.Id == command.VersionId && x.PipelineDefinitionId == definition.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Pipeline version was not found.");
        if (version.Status != PipelineVersionStatus.Draft) throw new InvalidOperationException("Only a draft version can be published.");
        var stages = await context.PipelineStages.Where(x => x.TenantId == command.TenantId && x.PipelineDefinitionVersionId == version.Id).ToListAsync(cancellationToken);
        var errors = Validate(version, stages, await context.PipelineStageTransitions.Where(x => x.TenantId == command.TenantId && x.PipelineDefinitionVersionId == version.Id).ToListAsync(cancellationToken));
        if (errors.Count > 0) throw new PipelineValidationException(errors);

        var superseded = await context.PipelineDefinitionVersions.Where(x => x.TenantId == command.TenantId && x.PipelineDefinitionId == definition.Id && x.Status == PipelineVersionStatus.Published).ToListAsync(cancellationToken);
        var previousIds = superseded.Select(x => x.Id).ToArray();
        var impactCount = previousIds.Length == 0 ? 0 : await context.Opportunities.LongCountAsync(x => x.TenantId == command.TenantId && x.PipelineDefinitionVersionId != null && previousIds.Contains(x.PipelineDefinitionVersionId.Value), cancellationToken);
        foreach (var oldVersion in superseded) oldVersion.Supersede();
        version.Publish();
        definition.MarkConfigurationChanged();

        var result = new PublishPipelineVersionResult(definition.Id, version.Id, version.VersionNumber, checked((int)Math.Min(impactCount, int.MaxValue)), false);
        var payload = JsonSerializer.Serialize(result);
        context.OutboxMessages.Add(OutboxMessage.Create(command.TenantId, nameof(PipelineDefinition), definition.Id, definition.RowVersion,
            "enterprise.crm.pipeline.published.v1", "crm", $"pipelines/{definition.Id}/versions/{version.Id}", command.CorrelationId, null,
            JsonSerializer.Serialize(new { pipelineDefinitionId = definition.Id, versionId = version.Id, priorVersionOpportunityCount = impactCount })));
        context.EvidenceRecords.Add(EvidenceRecord.Create(command.TenantId, nameof(PipelineDefinition), definition.Id, definition.RowVersion,
            command.Principal, "CrmPipeline.Publish", JsonSerializer.Serialize(new { versionId = version.Id, priorVersionOpportunityCount = impactCount }), command.CorrelationId));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, command.Principal, Operation, command.IdempotencyKey, hash, 200, payload, TimeSpan.FromDays(1)));
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            var winner = await context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == command.TenantId
                && x.PrincipalIssuer == command.Principal.Issuer && x.PrincipalSubject == command.Principal.Subject
                && x.Operation == Operation && x.IdempotencyKey == command.IdempotencyKey, cancellationToken);
            if (winner is not null)
            {
                if (winner.RequestHash != hash) throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);
                var replay = JsonSerializer.Deserialize<PublishPipelineVersionResult>(winner.ResponsePayload)
                    ?? throw new InvalidOperationException("Stored publish response is empty.");
                return replay with { Replayed = true };
            }
            throw new CrmSettingsConcurrencyConflictException();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { ConstraintName: "pk_idempotency_records" })
        {
            await transaction.RollbackAsync(cancellationToken);
            var winner = await context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == command.TenantId
                && x.PrincipalIssuer == command.Principal.Issuer && x.PrincipalSubject == command.Principal.Subject
                && x.Operation == Operation && x.IdempotencyKey == command.IdempotencyKey, cancellationToken);
            if (winner is null) throw;
            if (winner.RequestHash != hash) throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);
            var replay = JsonSerializer.Deserialize<PublishPipelineVersionResult>(winner.ResponsePayload)
                ?? throw new InvalidOperationException("Stored publish response is empty.");
            return replay with { Replayed = true };
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    internal static List<string> Validate(PipelineDefinitionVersion version, IReadOnlyList<PipelineStage> stages, IReadOnlyList<PipelineStageTransition> transitions)
    {
        var errors = new List<string>();
        if (stages.Count == 0) errors.Add("At least one stage is required.");
        if (stages.Count(x => x.IsEntry && x.IsActive) != 1) errors.Add("Exactly one active entry stage is required.");
        var ids = stages.Where(x => x.IsActive).Select(x => x.Id).ToHashSet();
        if (version.EnforceAllowedTransitions && transitions.Any(x => !ids.Contains(x.FromStageId) || !ids.Contains(x.ToStageId))) errors.Add("Every allowed transition must connect active stages in this version.");
        return errors;
    }
}

public sealed class PipelineValidationException(IReadOnlyList<string> errors) : InvalidOperationException(string.Join(" ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
