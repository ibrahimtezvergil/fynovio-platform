using System.Text.Json;
using Collaboration.Domain;
using Collaboration.Idempotency;
using Collaboration.Outbox;
using Collaboration.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Collaboration.Application;

/// <summary>Hard delete of one of the caller's own entries (no soft delete; the outbox row records the fact). The
/// idempotency lookup runs before the entry is loaded because after a successful delete the entry no longer exists — a
/// retry must replay the original success, not report not-found.</summary>
public sealed class DeleteCalendarEntryHandler(CollaborationDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "DeleteCalendarEntry";
    private const string ActionKeyValue = "collaboration.calendar_entry.delete";
    private const int SucceededStatus = 204;

    public async Task<DeleteCalendarEntryResult> HandleAsync(DeleteCalendarEntryCommand command, CancellationToken cancellationToken = default)
    {
        IdempotencySupport.ValidateKey(command.IdempotencyKey);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        // Coarse gate first (no resource yet): a caller without the capability gets 403 whether or not the id exists.
        var actor = new ActorContext(command.TenantId, command.Principal, command.CorrelationId);
        await AuthorizeAsync(actor, new ResourceDescriptor(nameof(CalendarEntry), null, command.Principal), null, cancellationToken);

        var requestHash = HashRequest(command);

        // The request that owns this key may commit at any point up to our own save, and after it commits the entry is gone
        // (or changed) as far as we can tell. So the key is looked up first, and again before either verdict is given.
        async Task<DeleteCalendarEntryResult?> TryReplayAsync()
        {
            var record = await IdempotencySupport.FindAsync(
                context, command.TenantId, command.Principal, Operation, command.IdempotencyKey, cancellationToken);
            if (record is null)
                return null;
            if (record.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);

            await transaction.CommitAsync(cancellationToken);
            return new DeleteCalendarEntryResult(Replayed: true);
        }

        if (await TryReplayAsync() is { } replayed)
            return replayed;

        var entry = await context.CalendarEntries.SingleOrDefaultAsync(
            e => e.TenantId == command.TenantId
                && e.Id == command.EntryId
                && e.OwnerPrincipalIssuer == command.Principal.Issuer
                && e.OwnerPrincipalSubject == command.Principal.Subject,
            cancellationToken);
        if (entry is null)
            return await TryReplayAsync() ?? throw new CalendarEntryNotFoundException(command.EntryId);

        await AuthorizeAsync(actor, new ResourceDescriptor(nameof(CalendarEntry), entry.Id, entry.Owner), entry.Id, cancellationToken);

        if (entry.RowVersion != command.ExpectedVersion)
        {
            return await TryReplayAsync()
                ?? throw new CalendarEntryConcurrencyConflictException(entry.Id, command.ExpectedVersion, entry.RowVersion);
        }

        // The deletion is the next fact about this aggregate, so it takes the next version.
        var deletedAtVersion = entry.RowVersion + 1;
        context.CalendarEntries.Remove(entry);
        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(CalendarEntry), entry.Id, deletedAtVersion,
            CalendarEntryOutbox.DeletedEventType, CalendarEntryOutbox.EventSource, CalendarEntryOutbox.Subject(entry.Id),
            command.CorrelationId, CalendarEntryOutbox.DeletedPayload(entry, deletedAtVersion)));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId, command.Principal, Operation, command.IdempotencyKey, requestHash, SucceededStatus,
            JsonSerializer.Serialize(new DeletedPayload(entry.Id, deletedAtVersion)), IdempotencySupport.Retention));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The row changed (or vanished) after we read it. An identical request that already won is a replay.
            var winner = await FindWinnerAsync(transaction, command, requestHash, cancellationToken);
            return winner ?? throw new CalendarEntryConcurrencyConflictException(command.EntryId, command.ExpectedVersion);
        }
        catch (DbUpdateException ex) when (IdempotencySupport.IsUniqueViolation(ex))
        {
            // Another request with the same idempotency key committed first between our lookup and our save.
            var winner = await FindWinnerAsync(transaction, command, requestHash, cancellationToken);
            if (winner is null)
                throw;
            return winner;
        }

        await transaction.CommitAsync(cancellationToken);
        return new DeleteCalendarEntryResult(Replayed: false);
    }

    private async Task AuthorizeAsync(ActorContext actor, ResourceDescriptor resource, long? entryId, CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey(ActionKeyValue), resource), cancellationToken);
        if (!decision.IsAllowed)
            // Without an entry this is the capability gate, whatever stage the authorizer reports.
            throw new CalendarEntryAuthorizationDeniedException(
                ActionKeyValue, decision.ReasonCode, entryId is null ? AuthorizationDenialStage.Coarse : decision.DenialStage, entryId);
    }

    /// <summary>Rolls the failed attempt back and looks for the request that beat it, in a fresh transaction (the failed one
    /// no longer holds the tenant context). Null when no request with this key committed.</summary>
    private async Task<DeleteCalendarEntryResult?> FindWinnerAsync(
        IDbContextTransaction failed, DeleteCalendarEntryCommand command, string requestHash, CancellationToken cancellationToken)
    {
        await failed.RollbackAsync(cancellationToken);
        context.ChangeTracker.Clear();

        await using var replayTransaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);
        var winner = await IdempotencySupport.FindAsync(
            context, command.TenantId, command.Principal, Operation, command.IdempotencyKey, cancellationToken);
        if (winner is null)
            return null;
        if (winner.RequestHash != requestHash)
            throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);

        await replayTransaction.CommitAsync(cancellationToken);
        return new DeleteCalendarEntryResult(Replayed: true);
    }

    private static string HashRequest(DeleteCalendarEntryCommand command) =>
        IdempotencySupport.Hash(new
        {
            operation = Operation,
            tenantId = command.TenantId.Value,
            principalIssuer = command.Principal.Issuer,
            principalSubject = command.Principal.Subject,
            entryId = command.EntryId,
            expectedVersion = command.ExpectedVersion
        });

    /// <summary>Idempotency response shape: the deleted entry's id and the version the deletion took.</summary>
    private sealed record DeletedPayload(long EntryId, long Version);
}
