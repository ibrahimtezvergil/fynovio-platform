using System.Text.Json;
using Contracts;
using Microsoft.EntityFrameworkCore;
using SemanticCatalog.Idempotency;
using SemanticCatalog.Domain;
using SemanticCatalog.Persistence;

namespace SemanticCatalog.Application;

/// <summary>The settings-edit flow shared by every definition kind (adr-semantic-catalog-changeset.md S-5): one transaction that
/// authorizes the write action, replays a repeated idempotency key, walks a one-item change set
/// `Draft → Validated → AwaitingApproval → Approved` (the author approves their own set; recorded in evidence), publishes it, and
/// commits state, outbox, evidence and the idempotency record together.</summary>
internal static class OneItemChange
{
    public sealed record Request(
        TenantId TenantId, PrincipalRef Principal, Guid CorrelationId, string OwnerContext, string OperationName, string IdempotencyKey, string RequestHash);

    public static async Task<TResult> RunAsync<TResult>(
        SemanticCatalogDbContext context,
        IAuthorizer authorizer,
        Request request,
        Func<ChangeSetItem> buildItem,
        Func<PublishOutcome, TResult> toResult,
        Func<TResult, TResult> markReplayed,
        Func<DbUpdateException, Exception?> mapViolation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(request.TenantId, cancellationToken);
        await CatalogAuthorization.AuthorizeAsync(request.TenantId, request.Principal, request.CorrelationId, request.OwnerContext, write: true, authorizer, cancellationToken);

        var prior = await FindIdempotencyRecordAsync(context, request, asNoTracking: false, cancellationToken);
        if (prior is not null)
        {
            if (prior.RequestHash != request.RequestHash) throw new IdempotencyKeyReusedException(request.OperationName, request.IdempotencyKey);
            return markReplayed(Replay<TResult>(prior.ResponsePayload));
        }

        var publisher = new ChangeSetPublisher(context);
        var set = await publisher.DraftAsync(request.TenantId, request.Principal, [buildItem()], authorUnderLock: true, cancellationToken);
        set.Validate();
        set.SubmitForApproval();
        set.Approve();
        var outcome = await publisher.PublishAsync(set, request.Principal, request.CorrelationId, cancellationToken);
        if (!outcome.Published)
            throw new InvalidOperationException("A one-item change set authored under the revision lock cannot be stale.");

        var result = toResult(outcome);
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            request.TenantId, request.Principal, request.OperationName, request.IdempotencyKey, request.RequestHash, 200,
            JsonSerializer.Serialize(result), TimeSpan.FromDays(1)));

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
            var winner = await FindIdempotencyRecordAsync(context, request, asNoTracking: true, cancellationToken);
            if (winner is null) throw;
            if (winner.RequestHash != request.RequestHash) throw new IdempotencyKeyReusedException(request.OperationName, request.IdempotencyKey);
            return markReplayed(Replay<TResult>(winner.ResponsePayload));
        }
        catch (DbUpdateException exception) when (mapViolation(exception) is { } mapped)
        {
            throw mapped;
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static Task<IdempotencyRecord?> FindIdempotencyRecordAsync(SemanticCatalogDbContext context, Request request, bool asNoTracking, CancellationToken cancellationToken)
    {
        var query = asNoTracking ? context.IdempotencyRecords.AsNoTracking() : context.IdempotencyRecords;
        return query.SingleOrDefaultAsync(
            x => x.TenantId == request.TenantId && x.PrincipalIssuer == request.Principal.Issuer && x.PrincipalSubject == request.Principal.Subject
                && x.Operation == request.OperationName && x.IdempotencyKey == request.IdempotencyKey,
            cancellationToken);
    }

    private static TResult Replay<TResult>(string payload) =>
        JsonSerializer.Deserialize<TResult>(payload) ?? throw new InvalidOperationException("Stored response is empty.");
}
