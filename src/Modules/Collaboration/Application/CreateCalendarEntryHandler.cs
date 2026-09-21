using System.Security.Cryptography;
using System.Text;
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
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    public async Task<CreateCalendarEntryResult> HandleAsync(CreateCalendarEntryCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);
        var actor = new ActorContext(command.TenantId, command.Principal, command.CorrelationId);
        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(actor, new ActionKey(CollaborationActionKeys.CalendarEntryCreate), new ResourceDescriptor(nameof(CalendarEntry), null, command.Principal)), cancellationToken);
        if (!decision.IsAllowed) throw new CollaborationAuthorizationDeniedException(CollaborationActionKeys.CalendarEntryCreate);
        var hash = Hash(command);
        var existing = await context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(r => r.TenantId == command.TenantId && r.PrincipalIssuer == command.Principal.Issuer && r.PrincipalSubject == command.Principal.Subject && r.Operation == Operation && r.IdempotencyKey == command.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != hash) throw new InvalidOperationException("Idempotency key was reused with a different request.");
            var replay = JsonSerializer.Deserialize<CreateCalendarEntryResult>(existing.ResponsePayload)!;
            return replay with { Replayed = true };
        }
        var entry = CalendarEntry.Create(command.TenantId, command.Principal, command.Title, command.Notes, command.Color, command.AllDay, command.StartAt, command.EndAt, command.StartDate, command.EndDate, command.Link);
        context.CalendarEntries.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
        var result = new CreateCalendarEntryResult(entry.Id, entry.RowVersion, false);
        var payload = JsonSerializer.Serialize(new { entry.Id, Owner = command.Principal.ToString(), entry.AllDay, entry.StartAt, entry.EndAt, entry.StartDate, entry.EndDate, Link = entry.Link?.ToString() });
        context.OutboxMessages.Add(OutboxMessage.Create(command.TenantId, nameof(CalendarEntry), entry.Id, entry.RowVersion, "enterprise.collaboration.calendar-entry.created.v1", "/enterprise/collaboration", $"calendar-entries/{entry.Id}", command.CorrelationId, payload));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, command.Principal, Operation, command.IdempotencyKey, hash, 201, JsonSerializer.Serialize(result), Retention));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static string Hash(CreateCalendarEntryCommand command) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{Operation}|{command.TenantId}|{command.Principal}|{command.Title}|{command.Notes}|{command.Color}|{command.AllDay}|{command.StartAt:O}|{command.EndAt:O}|{command.StartDate}|{command.EndDate}|{command.Link}")));
}
