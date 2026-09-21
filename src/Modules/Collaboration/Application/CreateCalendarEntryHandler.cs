using System.Text.Json;
using Collaboration.Domain;
using Collaboration.Idempotency;
using Collaboration.Outbox;
using Collaboration.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Collaboration.Application;

public sealed class CreateCalendarEntryHandler(CollaborationDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "CreateCalendarEntry";
    private const string ActionKeyValue = "collaboration.calendar_entry.create";
    private const int SucceededStatus = 201;

    public async Task<CreateCalendarEntryResult> HandleAsync(CreateCalendarEntryCommand command, CancellationToken cancellationToken = default)
    {
        // Validate idempotency key early, before opening any transaction.
        IdempotencySupport.ValidateKey(command.IdempotencyKey);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        // CREATE-shaped resource: Id is null, owner is the actor (new entry not yet created).
        var actor = new ActorContext(command.TenantId, command.Principal, command.CorrelationId);
        var resource = new ResourceDescriptor(nameof(CalendarEntry), null, command.Principal);
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey(ActionKeyValue), resource), cancellationToken);
        if (!decision.IsAllowed)
            throw new CalendarEntryAuthorizationDeniedException(ActionKeyValue, decision.ReasonCode, decision.DenialStage);

        var requestHash = HashRequest(command);
        var existing = await IdempotencySupport.FindAsync(
            context, command.TenantId, command.Principal, Operation, command.IdempotencyKey, cancellationToken);

        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);

            await transaction.CommitAsync(cancellationToken);
            var stored = JsonSerializer.Deserialize<CreatedPayload>(existing.ResponsePayload)
                ?? throw new InvalidOperationException("Stored idempotency response is empty.");
            return new CreateCalendarEntryResult(stored.EntryId, stored.RowVersion, Replayed: true);
        }

        var entry = CalendarEntry.Create(
            command.TenantId, command.Principal, command.Title, command.Notes, command.Color,
            command.AllDay, command.StartAt, command.EndAt, command.StartDate, command.EndDate, command.Link);
        context.CalendarEntries.Add(entry);
        await context.SaveChangesAsync(cancellationToken); // assigns entry.Id

        // Idempotency response: just the entry id and row version (for idempotency table).
        var idempotencyPayload = new CreatedPayload(entry.Id, entry.RowVersion);
        var idempotencyPayloadJson = JsonSerializer.Serialize(idempotencyPayload);

        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(CalendarEntry), entry.Id, entry.RowVersion,
            CalendarEntryOutbox.CreatedEventType, CalendarEntryOutbox.EventSource, CalendarEntryOutbox.Subject(entry.Id),
            command.CorrelationId, CalendarEntryOutbox.EntryPayload(entry)));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId, command.Principal, Operation, command.IdempotencyKey, requestHash, SucceededStatus, idempotencyPayloadJson, IdempotencySupport.Retention));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IdempotencySupport.IsUniqueViolation(ex))
        {
            // Another request with the same idempotency key committed first between our
            // lookup and our SaveChanges — replay its result instead of inventing an
            // ad-hoc lock.
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();

            await using var replayTransaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await context.SetTenantContextAsync(command.TenantId, cancellationToken);
            var winner = await IdempotencySupport.FindAsync(
                context, command.TenantId, command.Principal, Operation, command.IdempotencyKey, cancellationToken);
            if (winner is null)
                throw;
            if (winner.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);
            var stored = JsonSerializer.Deserialize<CreatedPayload>(winner.ResponsePayload)
                ?? throw new InvalidOperationException("Stored idempotency response is empty.");
            await replayTransaction.CommitAsync(cancellationToken);
            return new CreateCalendarEntryResult(stored.EntryId, stored.RowVersion, Replayed: true);
        }

        await transaction.CommitAsync(cancellationToken);
        return new CreateCalendarEntryResult(entry.Id, entry.RowVersion, Replayed: false);
    }

    private static string HashRequest(CreateCalendarEntryCommand command) =>
        // Every user-supplied field participates; colour is lower-cased because the aggregate normalizes it, so
        // "#AABBCC" and "#aabbcc" are the same request. Instants are compared as UTC, dates as ISO.
        IdempotencySupport.Hash(new
        {
            operation = Operation,
            tenantId = command.TenantId.Value,
            principalIssuer = command.Principal.Issuer,
            principalSubject = command.Principal.Subject,
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

    /// <summary>Idempotency response shape: just the entry id and row version.</summary>
    private sealed record CreatedPayload(long EntryId, long RowVersion);
}
