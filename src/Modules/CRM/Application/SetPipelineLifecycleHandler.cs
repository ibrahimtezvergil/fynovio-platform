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

namespace CRM.Application;

public sealed record SetPipelineLifecycleCommand(TenantId TenantId, PrincipalRef Principal, long PipelineDefinitionId,
    long ExpectedRowVersion, bool IsActive, bool Archive, bool Restore, string IdempotencyKey, Guid CorrelationId);
public sealed record SetPipelineLifecycleResult(long PipelineDefinitionId, long RowVersion, bool IsActive, bool IsArchived, bool Replayed);

public sealed class SetPipelineLifecycleHandler(CrmDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "SetPipelineLifecycle";

    public async Task<SetPipelineLifecycleResult> HandleAsync(SetPipelineLifecycleCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Archive && command.Restore) throw new ArgumentException("A pipeline cannot be archived and restored in the same operation.");
        if (command.Restore && command.IsActive) throw new ArgumentException("A restored pipeline must start inactive.");
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);
        await GetCrmSettingsHandler.AuthorizeAsync(command.TenantId, command.Principal, command.CorrelationId, CrmActionKeys.SettingsUpdate, authorizer, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command with { IdempotencyKey = string.Empty, CorrelationId = Guid.Empty }))));
        var prior = await context.IdempotencyRecords.SingleOrDefaultAsync(x => x.TenantId == command.TenantId && x.PrincipalIssuer == command.Principal.Issuer
            && x.PrincipalSubject == command.Principal.Subject && x.Operation == Operation && x.IdempotencyKey == command.IdempotencyKey, cancellationToken);
        if (prior is not null)
        {
            if (prior.RequestHash != hash) throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);
            return (JsonSerializer.Deserialize<SetPipelineLifecycleResult>(prior.ResponsePayload) ?? throw new InvalidOperationException("Stored lifecycle response is empty.")) with { Replayed = true };
        }
        var definition = await context.PipelineDefinitions.SingleOrDefaultAsync(x => x.TenantId == command.TenantId && x.Id == command.PipelineDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException("Pipeline was not found.");
        if (definition.RowVersion != command.ExpectedRowVersion) throw new CrmSettingsConcurrencyConflictException();
        if ((!command.IsActive && !command.Restore) || command.Archive)
        {
            if (await context.CrmSettings.AnyAsync(x => x.TenantId == command.TenantId && x.DefaultPipelineDefinitionId == definition.Id, cancellationToken))
                throw new InvalidOperationException("Select another default pipeline before deactivating or archiving this pipeline.");
            // No CrmSettings row does not mean "no guard needed" — a tenant with none
            // configured yet can still be relying on this being its only pipeline (doc
            // 2026-09-27 §6.1/§5 item 7.c: closes the reopen-the-gap path).
            var otherActivePipelineExists = await context.PipelineDefinitions.AnyAsync(
                x => x.TenantId == command.TenantId && x.Id != definition.Id && x.IsActive && !x.IsArchived, cancellationToken);
            if (!otherActivePipelineExists)
                throw new InvalidOperationException("A tenant must always keep at least one active pipeline.");
        }
        if (command.Archive) definition.Archive();
        else if (command.Restore) definition.Restore();
        else definition.SetActive(command.IsActive);
        var result = new SetPipelineLifecycleResult(definition.Id, definition.RowVersion, definition.IsActive, definition.IsArchived, false);
        var payload = JsonSerializer.Serialize(result);
        context.OutboxMessages.Add(OutboxMessage.Create(command.TenantId, nameof(PipelineDefinition), definition.Id, definition.RowVersion,
            "enterprise.crm.pipeline.lifecycle_changed.v1", "crm", $"pipelines/{definition.Id}", command.CorrelationId, null,
            JsonSerializer.Serialize(new { definition.Id, definition.IsActive, definition.IsArchived })));
        context.EvidenceRecords.Add(EvidenceRecord.Create(command.TenantId, nameof(PipelineDefinition), definition.Id, definition.RowVersion,
            command.Principal, "CrmPipeline.SetLifecycle", JsonSerializer.Serialize(new { definition.IsActive, definition.IsArchived }), command.CorrelationId));
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
                var replay = JsonSerializer.Deserialize<SetPipelineLifecycleResult>(winner.ResponsePayload)
                    ?? throw new InvalidOperationException("Stored lifecycle response is empty.");
                return replay with { Replayed = true };
            }
            throw new CrmSettingsConcurrencyConflictException();
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { ConstraintName: "pk_idempotency_records" })
        {
            await transaction.RollbackAsync(cancellationToken);
            var winner = await context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == command.TenantId
                && x.PrincipalIssuer == command.Principal.Issuer && x.PrincipalSubject == command.Principal.Subject
                && x.Operation == Operation && x.IdempotencyKey == command.IdempotencyKey, cancellationToken);
            if (winner is null) throw;
            if (winner.RequestHash != hash) throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);
            var replay = JsonSerializer.Deserialize<SetPipelineLifecycleResult>(winner.ResponsePayload)
                ?? throw new InvalidOperationException("Stored lifecycle response is empty.");
            return replay with { Replayed = true };
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
