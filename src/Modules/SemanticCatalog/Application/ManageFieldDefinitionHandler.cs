using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Contracts;
using Microsoft.EntityFrameworkCore;
using SemanticCatalog.Domain;
using SemanticCatalog.Evidence;
using SemanticCatalog.Idempotency;
using SemanticCatalog.Outbox;
using SemanticCatalog.Persistence;

namespace SemanticCatalog.Application;

public enum FieldOperation
{
    Create,
    Update,
    Deprecate,
    Reactivate
}

public sealed record ManageFieldDefinitionCommand(
    TenantId TenantId,
    PrincipalRef Principal,
    FieldOperation Operation,
    long? DefinitionId,
    long ExpectedRowVersion,
    string OwnerContext,
    string ObjectType,
    string Key,
    string Label,
    FieldType Type,
    bool IsRequired,
    FieldConfigInput? Config,
    int SortOrder,
    string IdempotencyKey,
    Guid CorrelationId);

public sealed record FieldConfigInput(
    IReadOnlyList<FieldOptionInput>? Options = null,
    int? Scale = null,
    decimal? Min = null,
    decimal? Max = null,
    int? MaxLength = null);

public sealed record FieldOptionInput(string Key, string Label, bool IsDeprecated = false);

public sealed record ManageFieldDefinitionResult(long DefinitionId, long RowVersion, bool Replayed);

/// <summary>Creates, edits, deprecates and reactivates field definitions — behavior carried over unchanged from CRM's
/// tier-1 handler (adr-tier1-custom-fields.md). State, outbox, evidence and the idempotency record commit together.</summary>
public sealed class ManageFieldDefinitionHandler(SemanticCatalogDbContext context, IAuthorizer authorizer)
{
    private const int MaxActiveFieldsPerObjectType = 100;

    public async Task<ManageFieldDefinitionResult> HandleAsync(ManageFieldDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        ValidateCommand(command);
        var operation = $"ManageCustomFieldDefinition:{command.Operation}";
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);
        await CatalogAuthorization.AuthorizeAsync(command.TenantId, command.Principal, command.CorrelationId, command.OwnerContext, write: true, authorizer, cancellationToken);

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
        var result = new ManageFieldDefinitionResult(definition.Id, definition.RowVersion, false);

        var responsePayload = JsonSerializer.Serialize(result);
        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId,
            "TenantFieldDefinition",
            result.DefinitionId,
            result.RowVersion,
            "enterprise.crm.custom_field_definition.changed.v1",
            "semantic-catalog",
            $"crm-customization/field-definition/{result.DefinitionId}",
            command.CorrelationId,
            null,
            JsonSerializer.Serialize(new
            {
                id = result.DefinitionId,
                aggregateType = "Opportunity",
                fieldName = definition.Key,
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
                fieldName = definition.Key,
                aggregateType = "Opportunity",
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
            throw new CatalogConcurrencyConflictException();
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
            throw new FieldKeyConflictException(command.Key);
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static ManageFieldDefinitionResult Replay(string responsePayload) =>
        (JsonSerializer.Deserialize<ManageFieldDefinitionResult>(responsePayload)
            ?? throw new InvalidOperationException("Stored field definition response is empty.")) with
        { Replayed = true };

    private async Task<CatalogFieldDefinition> MutateAsync(ManageFieldDefinitionCommand command, CancellationToken ct)
    {
        CatalogFieldDefinition definition;

        switch (command.Operation)
        {
            case FieldOperation.Create:
                {
                    if (command.DefinitionId is not null)
                        throw new ArgumentException("Definition ID must be null for create operations.");

                    await EnsureRoomForActiveFieldAsync(command, ct);

                    definition = CatalogFieldDefinition.Create(
                        command.TenantId, command.OwnerContext, command.ObjectType, command.Key, command.Label, command.Type,
                        command.IsRequired, ToConfig(command.Config), command.SortOrder);

                    context.Entry(definition).Property(x => x.Id).CurrentValue = await context.AllocateFieldDefinitionIdAsync(ct);
                    context.FieldDefinitions.Add(definition);
                    break;
                }
            case FieldOperation.Update:
                {
                    definition = await LoadAsync(command, "update", ct);
                    CheckVersion(definition.RowVersion, command.ExpectedRowVersion);
                    definition.Update(command.Label, command.IsRequired, ToConfig(command.Config) ?? FieldConfig.Empty, command.SortOrder);
                    break;
                }
            case FieldOperation.Deprecate:
                {
                    definition = await LoadAsync(command, "deprecate", ct);
                    CheckVersion(definition.RowVersion, command.ExpectedRowVersion);
                    definition.Deprecate();
                    break;
                }
            case FieldOperation.Reactivate:
                {
                    definition = await LoadAsync(command, "reactivate", ct);
                    CheckVersion(definition.RowVersion, command.ExpectedRowVersion);
                    await EnsureRoomForActiveFieldAsync(command with { ObjectType = definition.ObjectType }, ct);
                    definition.Reactivate();
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(command.Operation));
        }

        return definition;
    }

    private async Task<CatalogFieldDefinition> LoadAsync(ManageFieldDefinitionCommand command, string verb, CancellationToken ct)
    {
        if (command.DefinitionId is null)
            throw new ArgumentException($"Definition ID is required for {verb} operations.");

        return await context.FieldDefinitions
            .SingleOrDefaultAsync(x => x.TenantId == command.TenantId && x.OwnerContext == command.OwnerContext && x.Id == command.DefinitionId, ct)
            ?? throw new KeyNotFoundException($"Field definition {command.DefinitionId} was not found.");
    }

    private async Task EnsureRoomForActiveFieldAsync(ManageFieldDefinitionCommand command, CancellationToken ct)
    {
        var activeCount = await context.FieldDefinitions
            .CountAsync(x => x.TenantId == command.TenantId && x.OwnerContext == command.OwnerContext && x.ObjectType == command.ObjectType && x.Status == FieldStatus.Active, ct);

        if (activeCount >= MaxActiveFieldsPerObjectType)
            throw new FieldLimitExceededException(MaxActiveFieldsPerObjectType);
    }

    private static FieldConfig? ToConfig(FieldConfigInput? input) => input is null
        ? null
        : new FieldConfig(
            input.Options?.Select(o => new FieldOption(o.Key, o.Label, o.IsDeprecated)).ToList(),
            input.Scale, input.Min, input.Max, input.MaxLength);

    private static void CheckVersion(long actual, long expected)
    {
        if (actual != expected) throw new CatalogConcurrencyConflictException();
    }

    private static bool IsFieldKeyViolation(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505" } postgres
        && postgres.ConstraintName == "ix_field_definitions_tenant_id_owner_context_object_type_key";

    private static void ValidateCommand(ManageFieldDefinitionCommand command)
    {
        if (!Enum.IsDefined(command.Operation)) throw new ArgumentException("Unknown operation.", nameof(command.Operation));
        if (!CatalogOwners.IsKnown(command.OwnerContext, command.ObjectType))
            throw new ArgumentException($"Field definitions for '{command.OwnerContext}/{command.ObjectType}' are not supported.", nameof(command.ObjectType));

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 200)
            throw new ArgumentException("An idempotency key is required.");

        if (command.Operation == FieldOperation.Create)
        {
            if (!Enum.IsDefined(command.Type))
                throw new ArgumentException("Unknown field type.", nameof(command.Type));
        }
        else
        {
            if (command.DefinitionId is null || command.DefinitionId <= 0)
                throw new ArgumentException("Definition ID is required for this operation.", nameof(command.DefinitionId));
        }
    }
}
