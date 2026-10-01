using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Contracts;
using Microsoft.EntityFrameworkCore;
using SemanticCatalog.Domain;
using SemanticCatalog.Idempotency;
using SemanticCatalog.Persistence;

namespace SemanticCatalog.Application;

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
    int? MaxLength = null,
    FieldTargetInput? Target = null);

public sealed record FieldOptionInput(string Key, string Label, bool IsDeprecated = false);

public sealed record FieldTargetInput(string BoundedContext, string EntityType);

public sealed record ManageFieldDefinitionResult(long DefinitionId, long RowVersion, bool Replayed, long? ChangeSetId = null);

/// <summary>Creates, edits, deprecates and reactivates field definitions. Since C-2 every edit is a **one-item change set**
/// that this handler walks `Draft → Validated → AwaitingApproval → Approved` (the writer approves their own set; recorded in
/// evidence) and hands to <see cref="ChangeSetPublisher"/>, which publishes and activates it in the same transaction. Limits,
/// validation, errors and the response are those of the tier-1 handler (adr-tier1-custom-fields.md).</summary>
public sealed class ManageFieldDefinitionHandler(SemanticCatalogDbContext context, IAuthorizer authorizer)
{
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

        var publisher = new ChangeSetPublisher(context);
        var set = await publisher.DraftAsync(command.TenantId, command.Principal, [ToItem(command)], authorUnderLock: true, cancellationToken);
        set.Validate();
        set.SubmitForApproval();
        set.Approve();
        var outcome = await publisher.PublishAsync(set, command.Principal, command.CorrelationId, cancellationToken);
        if (!outcome.Published)
            throw new InvalidOperationException("A one-item change set authored under the revision lock cannot be stale.");

        var definition = outcome.Definitions.Single();
        var result = new ManageFieldDefinitionResult(definition.Id, definition.RowVersion, false, set.Id);

        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId,
            command.Principal,
            operation,
            command.IdempotencyKey,
            hash,
            200,
            JsonSerializer.Serialize(result),
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

    private static ChangeSetItem ToItem(ManageFieldDefinitionCommand command)
    {
        var config = command.Config is null
            ? null
            : new FieldConfig(
                command.Config.Options?.Select(o => new FieldOption(o.Key, o.Label, o.IsDeprecated)).ToList(),
                command.Config.Scale, command.Config.Min, command.Config.Max, command.Config.MaxLength,
                command.Config.Target is null ? null : new FieldTarget(command.Config.Target.BoundedContext, command.Config.Target.EntityType));
        var content = new FieldChangeContent(
            command.OwnerContext, command.ObjectType, command.Key, command.Label,
            command.Operation == FieldOperation.Create ? command.Type : null,
            command.IsRequired, config, command.SortOrder, command.ExpectedRowVersion);

        if (command.Operation == FieldOperation.Create && command.DefinitionId is not null)
            throw new ArgumentException("Definition ID must be null for create operations.");
        return ChangeSetItem.ForField(command.Operation, command.DefinitionId, content);
    }

    private static ManageFieldDefinitionResult Replay(string responsePayload) =>
        (JsonSerializer.Deserialize<ManageFieldDefinitionResult>(responsePayload)
            ?? throw new InvalidOperationException("Stored field definition response is empty.")) with
        { Replayed = true };

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
