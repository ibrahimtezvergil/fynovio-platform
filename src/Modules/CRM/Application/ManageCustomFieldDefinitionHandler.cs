using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Contracts;
using CRM.Customization;
using CRM.Evidence;
using CRM.Idempotency;
using CRM.Outbox;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

public enum CustomFieldOperation
{
    Create,
    Update,
    Deprecate,
    Reactivate
}

public sealed record ManageCustomFieldDefinitionCommand(
    TenantId TenantId,
    PrincipalRef Principal,
    CustomFieldOperation Operation,
    long? DefinitionId,
    long ExpectedRowVersion,
    TenantFieldAggregateType AggregateType,
    string FieldName,
    string Label,
    TenantFieldValueType FieldType,
    bool IsRequired,
    TenantFieldConfigInput? Config,
    int SortOrder,
    string IdempotencyKey,
    Guid CorrelationId);

public sealed record TenantFieldConfigInput(
    IReadOnlyList<TenantFieldOptionInput>? Options = null,
    int? Scale = null,
    decimal? Min = null,
    decimal? Max = null,
    int? MaxLength = null);

public sealed record TenantFieldOptionInput(string Key, string Label, bool IsDeprecated = false);

public sealed record ManageCustomFieldDefinitionResult(long DefinitionId, long RowVersion, bool Replayed);

public sealed class ManageCustomFieldDefinitionHandler(CrmDbContext context, IAuthorizer authorizer)
{
    private const int MaxActiveFieldsPerAggregateType = 100;

    public async Task<ManageCustomFieldDefinitionResult> HandleAsync(ManageCustomFieldDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        ValidateCommand(command);
        var operation = $"ManageCustomFieldDefinition:{command.Operation}";
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);
        await GetCrmSettingsHandler.AuthorizeAsync(command.TenantId, command.Principal, command.CorrelationId, CrmActionKeys.SettingsUpdate, authorizer, cancellationToken);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command with { IdempotencyKey = string.Empty, CorrelationId = Guid.Empty }))));
        var prior = await context.IdempotencyRecords.SingleOrDefaultAsync(
            x => x.TenantId == command.TenantId && x.PrincipalIssuer == command.Principal.Issuer
                && x.PrincipalSubject == command.Principal.Subject && x.Operation == operation && x.IdempotencyKey == command.IdempotencyKey,
            cancellationToken);

        if (prior is not null)
        {
            if (prior.RequestHash != hash) throw new IdempotencyKeyReusedException(operation, command.IdempotencyKey);
            return Replay(prior.ResponsePayload);
        }

        var definition = await MutateAsync(command, cancellationToken);
        var result = new ManageCustomFieldDefinitionResult(definition.Id, definition.RowVersion, false);

        var responsePayload = JsonSerializer.Serialize(result);
        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId,
            "TenantFieldDefinition",
            result.DefinitionId,
            result.RowVersion,
            "enterprise.crm.custom_field_definition.changed.v1",
            "crm",
            $"crm-customization/field-definition/{result.DefinitionId}",
            command.CorrelationId,
            null,
            JsonSerializer.Serialize(new
            {
                id = result.DefinitionId,
                aggregateType = definition.AggregateType.ToString(),
                fieldName = definition.FieldName,
                operation = command.Operation.ToString(),
                rowVersion = result.RowVersion
            })));

        context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId,
            "TenantFieldDefinition",
            result.DefinitionId,
            result.RowVersion,
            command.Principal,
            $"TenantFieldDefinition.{command.Operation}",
            JsonSerializer.Serialize(new
            {
                fieldName = definition.FieldName,
                aggregateType = definition.AggregateType.ToString(),
                operation = command.Operation.ToString(),
                rowVersion = result.RowVersion
            }),
            command.CorrelationId));

        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId,
            command.Principal,
            operation,
            command.IdempotencyKey,
            hash,
            200,
            responsePayload,
            TimeSpan.FromDays(1)));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CrmSettingsConcurrencyConflictException();
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { ConstraintName: "pk_idempotency_records" })
        {
            await transaction.RollbackAsync(cancellationToken);
            var winner = await context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
                x => x.TenantId == command.TenantId && x.PrincipalIssuer == command.Principal.Issuer
                    && x.PrincipalSubject == command.Principal.Subject && x.Operation == operation && x.IdempotencyKey == command.IdempotencyKey,
                cancellationToken);
            if (winner is null) throw;
            if (winner.RequestHash != hash) throw new IdempotencyKeyReusedException(operation, command.IdempotencyKey);
            return Replay(winner.ResponsePayload);
        }
        catch (DbUpdateException exception) when (IsFieldKeyViolation(exception))
        {
            throw new CustomFieldKeyConflictException(command.FieldName);
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static ManageCustomFieldDefinitionResult Replay(string responsePayload) =>
        (JsonSerializer.Deserialize<ManageCustomFieldDefinitionResult>(responsePayload)
            ?? throw new InvalidOperationException("Stored custom field definition response is empty.")) with { Replayed = true };

    private async Task<TenantFieldDefinition> MutateAsync(ManageCustomFieldDefinitionCommand command, CancellationToken ct)
    {
        TenantFieldDefinition definition;

        switch (command.Operation)
        {
            case CustomFieldOperation.Create:
                {
                    if (command.DefinitionId is not null)
                        throw new ArgumentException("Definition ID must be null for create operations.");

                    var activeCount = await context.TenantFieldDefinitions
                        .CountAsync(x => x.TenantId == command.TenantId && x.AggregateType == command.AggregateType && x.Status == TenantFieldStatus.Active, ct);

                    if (activeCount >= MaxActiveFieldsPerAggregateType)
                        throw new CustomFieldLimitExceededException(MaxActiveFieldsPerAggregateType);

                    var config = command.Config is not null
                        ? new TenantFieldConfig(
                            command.Config.Options?.Select(o => new TenantFieldOption(o.Key, o.Label, o.IsDeprecated)).ToList(),
                            command.Config.Scale,
                            command.Config.Min,
                            command.Config.Max,
                            command.Config.MaxLength)
                        : null;

                    definition = TenantFieldDefinition.Create(
                        command.TenantId,
                        command.AggregateType,
                        command.FieldName,
                        command.Label,
                        command.FieldType,
                        command.IsRequired,
                        config,
                        command.SortOrder);

                    context.Entry(definition).Property(x => x.Id).CurrentValue = await context.AllocateConfigurationIdAsync<TenantFieldDefinition>(ct);
                    context.TenantFieldDefinitions.Add(definition);
                    break;
                }
            case CustomFieldOperation.Update:
                {
                    if (command.DefinitionId is null)
                        throw new ArgumentException("Definition ID is required for update operations.");

                    definition = await context.TenantFieldDefinitions
                        .SingleOrDefaultAsync(x => x.TenantId == command.TenantId && x.Id == command.DefinitionId, ct)
                        ?? throw new KeyNotFoundException($"Field definition {command.DefinitionId} was not found.");

                    CheckVersion(definition.RowVersion, command.ExpectedRowVersion);

                    var config = command.Config is not null
                        ? new TenantFieldConfig(
                            command.Config.Options?.Select(o => new TenantFieldOption(o.Key, o.Label, o.IsDeprecated)).ToList(),
                            command.Config.Scale,
                            command.Config.Min,
                            command.Config.Max,
                            command.Config.MaxLength)
                        : TenantFieldConfig.Empty;

                    definition.Update(command.Label, command.IsRequired, config, command.SortOrder);
                    break;
                }
            case CustomFieldOperation.Deprecate:
                {
                    if (command.DefinitionId is null)
                        throw new ArgumentException("Definition ID is required for deprecate operations.");

                    definition = await context.TenantFieldDefinitions
                        .SingleOrDefaultAsync(x => x.TenantId == command.TenantId && x.Id == command.DefinitionId, ct)
                        ?? throw new KeyNotFoundException($"Field definition {command.DefinitionId} was not found.");

                    CheckVersion(definition.RowVersion, command.ExpectedRowVersion);
                    definition.Deprecate();
                    break;
                }
            case CustomFieldOperation.Reactivate:
                {
                    if (command.DefinitionId is null)
                        throw new ArgumentException("Definition ID is required for reactivate operations.");

                    definition = await context.TenantFieldDefinitions
                        .SingleOrDefaultAsync(x => x.TenantId == command.TenantId && x.Id == command.DefinitionId, ct)
                        ?? throw new KeyNotFoundException($"Field definition {command.DefinitionId} was not found.");

                    CheckVersion(definition.RowVersion, command.ExpectedRowVersion);

                    var activeCount = await context.TenantFieldDefinitions
                        .CountAsync(x => x.TenantId == command.TenantId && x.AggregateType == definition.AggregateType && x.Status == TenantFieldStatus.Active, ct);

                    if (activeCount >= MaxActiveFieldsPerAggregateType)
                        throw new CustomFieldLimitExceededException(MaxActiveFieldsPerAggregateType);

                    definition.Reactivate();
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(command.Operation));
        }

        return definition;
    }

    private static void CheckVersion(long actual, long expected)
    {
        if (actual != expected) throw new CrmSettingsConcurrencyConflictException();
    }

    private static bool IsFieldKeyViolation(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505" } postgres
        && postgres.ConstraintName == "ix_tenant_field_definitions_tenant_id_aggregate_type_field_name";

    private static void ValidateCommand(ManageCustomFieldDefinitionCommand command)
    {
        if (!Enum.IsDefined(command.Operation)) throw new ArgumentException("Unknown operation.", nameof(command.Operation));
        if (!Enum.IsDefined(command.AggregateType)) throw new ArgumentException("Unknown aggregate type.", nameof(command.AggregateType));

        if (command.AggregateType == TenantFieldAggregateType.Party)
            throw new ArgumentException("Party field definitions are not supported in this release.", nameof(command.AggregateType));

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 200)
            throw new ArgumentException("An idempotency key is required.");

        if (command.Operation == CustomFieldOperation.Create)
        {
            if (!Enum.IsDefined(command.FieldType))
                throw new ArgumentException("Unknown field type.", nameof(command.FieldType));
        }
        else
        {
            if (command.DefinitionId is null || command.DefinitionId <= 0)
                throw new ArgumentException("Definition ID is required for this operation.", nameof(command.DefinitionId));
        }
    }
}

public sealed class CustomFieldKeyConflictException(string fieldName)
    : InvalidOperationException($"A field definition with the key '{fieldName}' already exists for this tenant and aggregate type.");

public sealed class CustomFieldLimitExceededException(int limit)
    : InvalidOperationException($"Cannot create or reactivate a field definition: the limit of {limit} active fields per aggregate type per tenant has been reached.");
