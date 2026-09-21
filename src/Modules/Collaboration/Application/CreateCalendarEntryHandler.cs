using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Collaboration.Domain;
using Collaboration.Idempotency;
using Collaboration.Outbox;
using Collaboration.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Collaboration.Application;

public sealed class CreateCalendarEntryHandler(CollaborationDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "CreateCalendarEntry";
    private const string ActionKeyValue = "collaboration.calendar_entry.create";
    private const string EventType = "enterprise.collaboration.calendar-entry.created.v1";
    private const string EventSource = "/enterprise/collaboration";
    private const int SucceededStatus = 201;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    public async Task<CreateCalendarEntryResult> HandleAsync(CreateCalendarEntryCommand command, CancellationToken cancellationToken = default)
    {
        // Validate idempotency key early, before opening any transaction.
        ValidateIdempotencyKey(command.IdempotencyKey);

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
        var existing = await context.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                r => r.TenantId == command.TenantId
                    && r.PrincipalIssuer == command.Principal.Issuer
                    && r.PrincipalSubject == command.Principal.Subject
                    && r.Operation == Operation
                    && r.IdempotencyKey == command.IdempotencyKey,
                cancellationToken);

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

        var payload = new CreatedPayload(entry.Id, entry.RowVersion);
        var payloadJson = JsonSerializer.Serialize(payload);

        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(CalendarEntry), entry.Id, entry.RowVersion,
            EventType, EventSource, $"calendar-entries/{entry.Id}", command.CorrelationId, payloadJson));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId, command.Principal, Operation, command.IdempotencyKey, requestHash, SucceededStatus, payloadJson, IdempotencyRetention));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Another request with the same idempotency key committed first between our
            // lookup and our SaveChanges — replay its result instead of inventing an
            // ad-hoc lock.
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();

            await using var replayTransaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await context.SetTenantContextAsync(command.TenantId, cancellationToken);
            var winner = await context.IdempotencyRecords.AsNoTracking().SingleAsync(
                record => record.TenantId == command.TenantId
                    && record.PrincipalIssuer == command.Principal.Issuer
                    && record.PrincipalSubject == command.Principal.Subject
                    && record.Operation == Operation
                    && record.IdempotencyKey == command.IdempotencyKey,
                cancellationToken);
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

    // PostgreSQL error code 23505 = unique_violation (Npgsql exposes SqlState as this raw
    // string, not a named constant — verify against the installed Npgsql version's
    // PostgresException.SqlState docs before relying on this if it's ever unclear).
    private const string UniqueViolationSqlState = "23505";

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState };

    private static void ValidateIdempotencyKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
            throw new ArgumentException("Idempotency key must be non-blank and at most 128 characters.", nameof(key));
    }

    private static string HashRequest(CreateCalendarEntryCommand command)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.Principal}|{command.Title}|{command.Notes}|{command.Color}|{command.AllDay}|{command.StartAt:O}|{command.EndAt:O}|{command.StartDate}|{command.EndDate}|{command.Link}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed record CreatedPayload(long EntryId, long RowVersion);
}
