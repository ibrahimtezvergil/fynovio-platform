using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Contracts;
using Microsoft.EntityFrameworkCore;
using SemanticCatalog.Domain;
using SemanticCatalog.Persistence;

namespace SemanticCatalog.Application;

public sealed record ManageViewDefinitionCommand(
    TenantId TenantId,
    PrincipalRef Principal,
    ChangeOperation Operation,
    long? DefinitionId,
    long ExpectedRowVersion,
    string OwnerContext,
    string ObjectType,
    string Key,
    string Name,
    IReadOnlyList<ViewColumnInput> Columns,
    int SortOrder,
    string IdempotencyKey,
    Guid CorrelationId);

public sealed record ViewColumnInput(string Kind, string Key);

public sealed record ManageViewDefinitionResult(long DefinitionId, long RowVersion, bool Replayed, long? ChangeSetId = null);

/// <summary>Creates, edits, deprecates and reactivates shared table views — always as a one-item change set, so a view is never
/// live before it is `Active` and its dependency edges are computed in the publish transaction.</summary>
public sealed class ManageViewDefinitionHandler(SemanticCatalogDbContext context, IAuthorizer authorizer)
{
    public Task<ManageViewDefinitionResult> HandleAsync(ManageViewDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command with { IdempotencyKey = string.Empty, CorrelationId = Guid.Empty }))));

        return OneItemChange.RunAsync(
            context, authorizer,
            new OneItemChange.Request(command.TenantId, command.Principal, command.CorrelationId, command.OwnerContext,
                $"ManageViewDefinition:{command.Operation}", command.IdempotencyKey, hash),
            () => ChangeSetItem.ForView(command.Operation, command.DefinitionId,
                new ViewChangeContent(command.OwnerContext, command.ObjectType, command.Key, command.Name,
                    command.Columns.Select(c => new ViewColumn(c.Kind, c.Key)).ToList(), command.SortOrder, command.ExpectedRowVersion)),
            outcome =>
            {
                var view = outcome.Views.Single();
                return new ManageViewDefinitionResult(view.Id, view.RowVersion, false, outcome.ChangeSet.Id);
            },
            replay => replay with { Replayed = true },
            exception => IsKeyViolation(exception) ? new ViewKeyConflictException(command.Key) : null,
            cancellationToken);
    }

    private static bool IsKeyViolation(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505" } postgres
        && postgres.ConstraintName == "ix_view_definitions_tenant_id_owner_context_object_type_key";

    private static void Validate(ManageViewDefinitionCommand command)
    {
        if (!Enum.IsDefined(command.Operation)) throw new ArgumentException("Unknown operation.", nameof(command.Operation));
        if (!CatalogOwners.IsKnown(command.OwnerContext, command.ObjectType))
            throw new ArgumentException($"Views for '{command.OwnerContext}/{command.ObjectType}' are not supported.", nameof(command.ObjectType));
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 200)
            throw new ArgumentException("An idempotency key is required.");
        if (command.Operation != ChangeOperation.Create && command.DefinitionId is null or <= 0)
            throw new ArgumentException("Definition ID is required for this operation.", nameof(command.DefinitionId));
        if (command.Operation == ChangeOperation.Create && command.DefinitionId is not null)
            throw new ArgumentException("Definition ID must be null for create operations.");
    }
}

public sealed record ListViewDefinitionsQuery(TenantId TenantId, PrincipalRef Principal, string OwnerContext, string ObjectType, Guid CorrelationId);

public sealed record ViewColumnDto(string Kind, string Key);

public sealed record ViewDefinitionDto(long Id, string Key, string Name, string Kind, IReadOnlyList<ViewColumnDto> Columns, string Status, int SortOrder, long RowVersion);

/// <summary>Reads the tenant's shared views at `crm.settings.read` — the action every CRM reader already holds, which is what lets
/// a list page offer them without a capability-template change (adr-semantic-catalog-changeset.md S-4).</summary>
public sealed class ListViewDefinitionsHandler(SemanticCatalogDbContext context, IAuthorizer authorizer)
{
    public async Task<IReadOnlyList<ViewDefinitionDto>> HandleAsync(ListViewDefinitionsQuery query, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);
        await CatalogAuthorization.AuthorizeAsync(query.TenantId, query.Principal, query.CorrelationId, query.OwnerContext, write: false, authorizer, cancellationToken);

        var views = await context.ViewDefinitions.AsNoTracking()
            .Where(x => x.TenantId == query.TenantId && x.OwnerContext == query.OwnerContext && x.ObjectType == query.ObjectType)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Key)
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return views.Select(v => new ViewDefinitionDto(
            v.Id, v.Key, v.Name, v.Kind, v.Columns.Select(c => new ViewColumnDto(c.Kind, c.Key)).ToList(), v.Status.ToString(), v.SortOrder, v.RowVersion)).ToList();
    }
}
