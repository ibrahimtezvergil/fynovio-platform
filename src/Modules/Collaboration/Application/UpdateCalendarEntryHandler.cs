using System.Text.Json;
using Collaboration.Domain;
using Collaboration.Idempotency;
using Collaboration.Outbox;
using Collaboration.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Collaboration.Application;

/// <summary>Full replace of one of the caller's own entries. The idempotency lookup runs before the entry is loaded so a
/// retry of a call that already succeeded replays its result even when the entry has since changed.</summary>
public sealed class UpdateCalendarEntryHandler(CollaborationDbContext context, IAuthorizer authorizer, ILinkTargetDirectory links)
{
    private const string Operation = "UpdateCalendarEntry";
    private const string ActionKeyValue = "collaboration.calendar_entry.update";
    private const int SucceededStatus = 200;

    public async Task<UpdateCalendarEntryResult> HandleAsync(UpdateCalendarEntryCommand command, CancellationToken cancellationToken = default)
    {
        IdempotencySupport.ValidateKey(command.IdempotencyKey);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        // Coarse gate first (no resource yet): a caller without the capability gets 403 whether or not the id exists.
        var actor = new ActorContext(command.TenantId, command.Principal, command.CorrelationId);
        await AuthorizeAsync(actor, new ResourceDescriptor(nameof(CalendarEntry), null, command.Principal), null, cancellationToken);

        var requestHash = HashRequest(command);

        // The request that owns this key may commit at any point up to our own save, and after it commits the entry looks
        // changed (new version) to us. So the key is looked up first, and again before either "changed" verdict is given.
        async Task<UpdateCalendarEntryResult?> TryReplayAsync()
        {
            var record = await IdempotencySupport.FindAsync(
                context, command.TenantId, command.Principal, Operation, command.IdempotencyKey, cancellationToken);
            if (record is null)
                return null;
            if (record.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);

            await transaction.CommitAsync(cancellationToken);
            return Replay(record);
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

        // Only a new or changed link is resolved. Re-sending the stored link discloses nothing (the caller already reads it
        // back) and must not make an entry uneditable once its target has become unavailable.
        if (command.Link is { } link && link != entry.Link)
            await CalendarLinks.EnsureResolvableAsync(links, actor, link, cancellationToken);

        entry.Replace(
            command.Title, command.Notes, command.Color, command.AllDay, command.StartAt, command.EndAt,
            command.StartDate, command.EndDate, command.Link);

        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(CalendarEntry), entry.Id, entry.RowVersion,
            CalendarEntryOutbox.UpdatedEventType, CalendarEntryOutbox.EventSource, CalendarEntryOutbox.Subject(entry.Id),
            command.CorrelationId, CalendarEntryOutbox.EntryPayload(entry)));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId, command.Principal, Operation, command.IdempotencyKey, requestHash, SucceededStatus,
            JsonSerializer.Serialize(new UpdatedPayload(entry.Id, entry.RowVersion)), IdempotencySupport.Retention));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The row changed after we read it. If an identical request (same key) already won, that is a replay,
            // otherwise this caller genuinely lost the race.
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
        return new UpdateCalendarEntryResult(entry.Id, entry.RowVersion, Replayed: false);
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
    private async Task<UpdateCalendarEntryResult?> FindWinnerAsync(
        IDbContextTransaction failed, UpdateCalendarEntryCommand command, string requestHash, CancellationToken cancellationToken)
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
        return Replay(winner);
    }

    private static UpdateCalendarEntryResult Replay(IdempotencyRecord record)
    {
        var stored = JsonSerializer.Deserialize<UpdatedPayload>(record.ResponsePayload)
            ?? throw new InvalidOperationException("Stored idempotency response is empty.");
        return new UpdateCalendarEntryResult(stored.EntryId, stored.RowVersion, Replayed: true);
    }

    private static string HashRequest(UpdateCalendarEntryCommand command) =>
        IdempotencySupport.Hash(new
        {
            operation = Operation,
            tenantId = command.TenantId.Value,
            principalIssuer = command.Principal.Issuer,
            principalSubject = command.Principal.Subject,
            entryId = command.EntryId,
            expectedVersion = command.ExpectedVersion,
            title = command.Title,
            notes = command.Notes,
            color = command.Color?.ToLowerInvariant(),
            allDay = command.AllDay,
            startAt = IdempotencySupport.Instant(command.StartAt),
            endAt = IdempotencySupport.Instant(command.EndAt),
            startDate = IdempotencySupport.Date(command.StartDate),
            endDate = IdempotencySupport.Date(command.EndDate),
            linkBoundedContext = command.Link?.BoundedContext,
            linkEntityType = command.Link?.EntityType,
            linkEntityId = command.Link?.Id
        });

    /// <summary>Idempotency response shape: the entry id and the row version the update produced.</summary>
    private sealed record UpdatedPayload(long EntryId, long RowVersion);
}
