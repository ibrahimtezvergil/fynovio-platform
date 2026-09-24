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

public enum CrmCatalogKind
{
    OpportunityType,
    LostReason,
    CustomerNeed
}

public sealed record ManageCrmCatalogCommand(TenantId TenantId, PrincipalRef Principal, CrmCatalogKind Kind, long? Id,
    long ExpectedVersion, string Key, string Name, string? Category, decimal AveragePrice, ConfigurationStatus Status,
    string IdempotencyKey, Guid CorrelationId);
public sealed record ManageCrmCatalogResult(CrmCatalogItemDto Item, bool Replayed);
public sealed record CrmCatalogItemDto(long Id, string? Key, string Name, string? Category, decimal? AveragePrice,
    string Status, long RowVersion);

/// <summary>All CRM-owned lookup catalogs share the enterprise mutation ordering and durability contract.
/// Delete is intentionally not an operation; referenced choices move through inactive/archive states.</summary>
public sealed class ManageCrmCatalogHandler(CrmDbContext context, IAuthorizer authorizer)
{
    public async Task<ManageCrmCatalogResult> HandleAsync(ManageCrmCatalogCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command);
        var operation = $"ManageCrmCatalog:{command.Kind}:{(command.Id is null ? "Create" : "Update")}";
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);
        await GetCrmSettingsHandler.AuthorizeAsync(command.TenantId, command.Principal, command.CorrelationId, CrmActionKeys.SettingsUpdate, authorizer, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command with { IdempotencyKey = string.Empty, CorrelationId = Guid.Empty }))));
        var prior = await context.IdempotencyRecords.SingleOrDefaultAsync(x => x.TenantId == command.TenantId && x.PrincipalIssuer == command.Principal.Issuer
            && x.PrincipalSubject == command.Principal.Subject && x.Operation == operation && x.IdempotencyKey == command.IdempotencyKey, cancellationToken);
        if (prior is not null)
        {
            if (prior.RequestHash != hash) throw new IdempotencyKeyReusedException(operation, command.IdempotencyKey);
            return new ManageCrmCatalogResult(JsonSerializer.Deserialize<CrmCatalogItemDto>(prior.ResponsePayload) ?? throw new InvalidOperationException("Stored catalog response is empty."), true);
        }

        var item = await MutateAsync(command, cancellationToken);
        var payload = JsonSerializer.Serialize(item);
        var aggregate = command.Kind.ToString();
        context.OutboxMessages.Add(OutboxMessage.Create(command.TenantId, aggregate, item.Id, item.RowVersion,
            "enterprise.crm.catalog.changed.v1", "crm", $"crm-settings/catalog/{command.Kind}/{item.Id}", command.CorrelationId, null,
            JsonSerializer.Serialize(new { kind = command.Kind.ToString(), item.Id, item.RowVersion, item.Status })));
        context.EvidenceRecords.Add(EvidenceRecord.Create(command.TenantId, aggregate, item.Id, item.RowVersion, command.Principal,
            $"CrmCatalog.{command.Kind}.{(command.Id is null ? "Create" : "Update")}", JsonSerializer.Serialize(new { item.Key, item.Name, item.Status, item.RowVersion }), command.CorrelationId));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, command.Principal, operation, command.IdempotencyKey, hash, 200, payload, TimeSpan.FromDays(1)));
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
            var winner = await context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == command.TenantId
                && x.PrincipalIssuer == command.Principal.Issuer && x.PrincipalSubject == command.Principal.Subject
                && x.Operation == operation && x.IdempotencyKey == command.IdempotencyKey, cancellationToken);
            if (winner is null) throw;
            if (winner.RequestHash != hash) throw new IdempotencyKeyReusedException(operation, command.IdempotencyKey);
            return new ManageCrmCatalogResult(JsonSerializer.Deserialize<CrmCatalogItemDto>(winner.ResponsePayload)
                ?? throw new InvalidOperationException("Stored catalog response is empty."), true);
        }
        catch (DbUpdateException exception) when (IsCatalogKeyViolation(exception, command.Kind))
        {
            throw new CrmCatalogKeyConflictException(command.Kind);
        }
        await transaction.CommitAsync(cancellationToken);
        return new ManageCrmCatalogResult(item, false);
    }

    private async Task<CrmCatalogItemDto> MutateAsync(ManageCrmCatalogCommand command, CancellationToken ct)
    {
        switch (command.Kind)
        {
            case CrmCatalogKind.OpportunityType:
                {
                    OpportunityType entity;
                    if (command.Id is null)
                    {
                        entity = OpportunityType.Create(command.TenantId, command.Key, command.Name);
                        context.Entry(entity).Property(x => x.Id).CurrentValue = await context.AllocateConfigurationIdAsync<OpportunityType>(ct);
                        context.OpportunityTypes.Add(entity);
                    }
                    else
                    {
                        entity = await context.OpportunityTypes.SingleOrDefaultAsync(x => x.TenantId == command.TenantId && x.Id == command.Id, ct) ?? throw new KeyNotFoundException("Opportunity type was not found.");
                        CheckVersion(entity.RowVersion, command.ExpectedVersion);
                        if (entity.Status != ConfigurationStatus.Archived) entity.Rename(command.Name);
                        if (command.Status != ConfigurationStatus.Active && await context.CrmSettings.AnyAsync(x => x.TenantId == command.TenantId && x.DefaultOpportunityTypeId == entity.Id, ct))
                            throw new InvalidOperationException("Select another default opportunity type before deactivating or archiving this type.");
                        entity.ChangeStatus(command.Status);
                    }
                    return new CrmCatalogItemDto(entity.Id, entity.Key, entity.Name, null, null, entity.Status.ToString(), entity.RowVersion);
                }
            case CrmCatalogKind.LostReason:
                {
                    LostReason entity;
                    if (command.Id is null)
                    {
                        entity = LostReason.Create(command.TenantId, command.Key, command.Name);
                        context.Entry(entity).Property(x => x.Id).CurrentValue = await context.AllocateConfigurationIdAsync<LostReason>(ct);
                        context.LostReasons.Add(entity);
                    }
                    else
                    {
                        entity = await context.LostReasons.SingleOrDefaultAsync(x => x.TenantId == command.TenantId && x.Id == command.Id, ct) ?? throw new KeyNotFoundException("Lost reason was not found.");
                        CheckVersion(entity.RowVersion, command.ExpectedVersion);
                        if (entity.Status != ConfigurationStatus.Archived) entity.Rename(command.Name);
                        entity.ChangeStatus(command.Status);
                    }
                    return new CrmCatalogItemDto(entity.Id, entity.Key, entity.Name, null, null, entity.Status.ToString(), entity.RowVersion);
                }
            case CrmCatalogKind.CustomerNeed:
                {
                    CustomerNeed entity;
                    if (command.Id is null)
                    {
                        entity = CustomerNeed.Create(command.TenantId, command.Name, command.AveragePrice, command.Category);
                        context.Entry(entity).Property(x => x.Id).CurrentValue = await context.AllocateConfigurationIdAsync<CustomerNeed>(ct);
                        context.CustomerNeeds.Add(entity);
                    }
                    else
                    {
                        entity = await context.CustomerNeeds.SingleOrDefaultAsync(x => x.TenantId == command.TenantId && x.Id == command.Id, ct) ?? throw new KeyNotFoundException("Customer need was not found.");
                        CheckVersion(entity.RowVersion, command.ExpectedVersion);
                        if (entity.Status != ConfigurationStatus.Archived) entity.Replace(command.Name, command.Category, command.AveragePrice);
                        entity.ChangeStatus(command.Status);
                    }
                    return new CrmCatalogItemDto(entity.Id, null, entity.Name, entity.Category, entity.AveragePrice, entity.Status.ToString(), entity.RowVersion);
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(command.Kind));
        }
    }

    private static void CheckVersion(long actual, long expected)
    {
        if (actual != expected) throw new CrmSettingsConcurrencyConflictException();
    }

    private static bool IsCatalogKeyViolation(DbUpdateException exception, CrmCatalogKind kind) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505" } postgres
        && (kind, postgres.ConstraintName) switch
        {
            (CrmCatalogKind.OpportunityType, "ix_opportunity_types_tenant_id_key") => true,
            (CrmCatalogKind.LostReason, "ix_lost_reasons_tenant_id_key") => true,
            _ => false
        };

    private static void Validate(ManageCrmCatalogCommand command)
    {
        if (!Enum.IsDefined(command.Kind) || !Enum.IsDefined(command.Status)) throw new ArgumentException("Unknown CRM catalog kind or status.");
        if (command.Id is null && command.Status != ConfigurationStatus.Active) throw new ArgumentException("New catalog entries start active.");
        if (command.Kind != CrmCatalogKind.CustomerNeed && (string.IsNullOrWhiteSpace(command.Key) || command.Key.Trim().Length > 100)) throw new ArgumentException("A stable key of at most 100 characters is required.");
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Trim().Length > 100) throw new ArgumentException("A name of 1–100 characters is required.");
        if (command.Category is { Length: > 100 } || command.AveragePrice < 0 || decimal.Round(command.AveragePrice, 2) != command.AveragePrice) throw new ArgumentException("Customer need category or average price is invalid.");
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 200) throw new ArgumentException("An idempotency key is required.");
    }
}

public sealed class CrmCatalogKeyConflictException(CrmCatalogKind kind)
    : InvalidOperationException($"A {kind} catalog item with this key already exists in the tenant.");
