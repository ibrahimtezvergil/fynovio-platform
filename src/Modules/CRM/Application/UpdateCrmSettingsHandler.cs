using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Contracts;
using CRM.Evidence;
using CRM.Idempotency;
using CRM.Outbox;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

public sealed class UpdateCrmSettingsHandler(CrmDbContext context, IAuthorizer authorizer, IAuthorizedPrincipalDirectory? principalDirectory = null)
{
    private const string Operation = "UpdateCrmSettings";

    public async Task<UpdateCrmSettingsResult> HandleAsync(UpdateCrmSettingsCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);
        await GetCrmSettingsHandler.AuthorizeAsync(command.TenantId, command.Principal, command.CorrelationId, CrmActionKeys.SettingsUpdate, authorizer, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command with { IdempotencyKey = string.Empty, CorrelationId = Guid.Empty }))));
        var existing = await context.IdempotencyRecords.SingleOrDefaultAsync(x => x.TenantId == command.TenantId && x.PrincipalIssuer == command.Principal.Issuer && x.PrincipalSubject == command.Principal.Subject && x.Operation == Operation && x.IdempotencyKey == command.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != hash) throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);
            return new UpdateCrmSettingsResult(JsonSerializer.Deserialize<CrmSettingsDto>(existing.ResponsePayload)!, true);
        }

        if (command.DefaultAssignmentMode is CRM.Domain.AssignmentMode.Team or CRM.Domain.AssignmentMode.Territory)
            throw new CrmAssignmentProviderUnavailableException("Team and territory assignment providers are not available.");

        var settings = await context.CrmSettings.SingleOrDefaultAsync(x => x.TenantId == command.TenantId, cancellationToken);
        if (command.DefaultPipelineDefinitionId is { } pipelineId && !await context.PipelineDefinitions.AnyAsync(x => x.TenantId == command.TenantId && x.Id == pipelineId && x.IsActive && !x.IsArchived, cancellationToken))
            throw new ArgumentException("The default pipeline must be active in the current tenant.", nameof(command));
        if (command.DefaultPipelineDefinitionId is { } selectedPipelineId)
        {
            var latestVersionId = await context.PipelineDefinitionVersions.Where(x => x.TenantId == command.TenantId && x.PipelineDefinitionId == selectedPipelineId && x.Status == CRM.Domain.PipelineVersionStatus.Published)
                .OrderByDescending(x => x.VersionNumber).Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
            if (latestVersionId is null || !await context.PipelineStages.AnyAsync(x => x.TenantId == command.TenantId && x.PipelineDefinitionVersionId == latestVersionId && x.IsEntry && x.IsActive, cancellationToken))
                throw new ArgumentException("The default pipeline must have a version with an active entry stage.", nameof(command));
        }
        if (command.DefaultOpportunityTypeId is { } typeId && !await context.OpportunityTypes.AnyAsync(x => x.TenantId == command.TenantId && x.Id == typeId && x.Status == CRM.Domain.ConfigurationStatus.Active, cancellationToken))
            throw new ArgumentException("The default opportunity type must be active in the current tenant.", nameof(command));
        if (command.DefaultAssignmentMode == CRM.Domain.AssignmentMode.DefaultPrincipal && command.DefaultPrincipal is { } defaultPrincipal
            && principalDirectory is not null && !await principalDirectory.IsPrincipalPermittedAsync(command.TenantId, defaultPrincipal,
                CrmAssignmentPolicy.RequiredAssigneeActions, cancellationToken))
            throw new PrincipalNotAssignableException(defaultPrincipal);
        if (settings is null)
        {
            if (command.ExpectedVersion != 0) throw new CrmSettingsConcurrencyConflictException();
            settings = CRM.Domain.CrmSettings.Create(command.TenantId);
            context.CrmSettings.Add(settings);
        }
        else if (settings.RowVersion != command.ExpectedVersion) throw new CrmSettingsConcurrencyConflictException();

        settings.Replace(command.DefaultPipelineDefinitionId, command.OpportunityCreationMode, command.DefaultOpportunityTypeId, command.RequireLostReason, command.RequireWonLine, command.DefaultAssignmentMode, command.AssignmentPolicy, command.DefaultPrincipal, command.DefaultTeamId, command.DefaultTerritoryId);
        var response = new UpdateCrmSettingsResult(CrmSettingsMapper.Map(settings, [], [], [], []), false);
        context.OutboxMessages.Add(OutboxMessage.Create(command.TenantId, "CrmSettings", command.TenantId.Value, settings.RowVersion, "enterprise.crm.settings.changed.v1", "crm", $"crm-settings/{command.TenantId.Value}", command.CorrelationId, null, JsonSerializer.Serialize(new { tenantId = command.TenantId.Value, rowVersion = settings.RowVersion })));
        context.EvidenceRecords.Add(EvidenceRecord.Create(command.TenantId, "CrmSettings", command.TenantId.Value, settings.RowVersion, command.Principal,
            CrmActionKeys.SettingsUpdate, JsonSerializer.Serialize(new { rowVersion = settings.RowVersion }), command.CorrelationId));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, command.Principal, Operation, command.IdempotencyKey, hash, 200, JsonSerializer.Serialize(response.Settings), TimeSpan.FromDays(1)));
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
                var replayedSettings = JsonSerializer.Deserialize<CrmSettingsDto>(winner.ResponsePayload)
                    ?? throw new InvalidOperationException("Stored settings response is empty.");
                return new UpdateCrmSettingsResult(replayedSettings, true);
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
            return new UpdateCrmSettingsResult(JsonSerializer.Deserialize<CrmSettingsDto>(winner.ResponsePayload)
                ?? throw new InvalidOperationException("Stored settings response is empty."), true);
        }
        await transaction.CommitAsync(cancellationToken);
        return response;
    }
}

public sealed class CrmSettingsConcurrencyConflictException : InvalidOperationException { }
