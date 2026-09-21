using Collaboration.Domain;
using Collaboration.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Collaboration.Application;

public sealed class ListCalendarEntriesHandler(CollaborationDbContext context, IAuthorizer authorizer)
{
    private const string ActionKeyValue = "collaboration.calendar_entry.list";
    private const int MaxRangeDays = 100;
    private const int MaxResults = 500;

    public async Task<IReadOnlyList<CalendarEntryDto>> HandleAsync(ListCalendarEntriesQuery query, CancellationToken cancellationToken = default)
    {
        // Validate range before opening transaction.
        if (query.To <= query.From)
            throw new ArgumentException("Calendar range must have To > From.", nameof(query));

        if (query.To - query.From > TimeSpan.FromDays(MaxRangeDays))
            throw new CalendarRangeTooLargeException("Range exceeds 100 days.");

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);

        var actor = new ActorContext(query.TenantId, query.Principal, query.CorrelationId);
        var resource = new ResourceDescriptor(nameof(CalendarEntry), null, query.Principal);
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey(ActionKeyValue), resource), cancellationToken);
        if (!decision.IsAllowed)
            throw new CalendarEntryAuthorizationDeniedException(ActionKeyValue, decision.ReasonCode, decision.DenialStage);

        // Widen all-day range by one day on each side to match FullCalendar semantics.
        var fromDate = DateOnly.FromDateTime(query.From.UtcDateTime).AddDays(-1);
        var toDate = DateOnly.FromDateTime(query.To.UtcDateTime).AddDays(1);

        // Fetch MaxResults + 1 to detect if there are more results than allowed.
        var entries = await context.CalendarEntries
            .AsNoTracking()
            .Where(e =>
                e.TenantId == query.TenantId
                && e.OwnerPrincipalIssuer == query.Principal.Issuer
                && e.OwnerPrincipalSubject == query.Principal.Subject
                && (
                    // Timed entries with null end: include only if StartAt is within the range.
                    (!e.AllDay && e.EndAt == null && e.StartAt >= query.From && e.StartAt < query.To)
                    ||
                    // Timed entries with end: standard overlap check (StartAt < To && EndAt > From).
                    (!e.AllDay && e.EndAt != null && e.StartAt < query.To && e.EndAt > query.From)
                    ||
                    // All-day entries: overlap check with widened range.
                    (e.AllDay && e.StartDate < toDate && e.EndDate > fromDate)
                ))
            .OrderBy(e => e.AllDay)
            .ThenBy(e => e.StartAt)
            .ThenBy(e => e.StartDate)
            .ThenBy(e => e.Id)  // Tie-break on Id for deterministic ordering.
            .Take(MaxResults + 1)
            .ToListAsync(cancellationToken);

        if (entries.Count > MaxResults)
            throw new CalendarRangeTooLargeException("Too many results (> 500).");

        await transaction.CommitAsync(cancellationToken);

        return entries.Select(e => GetCalendarEntryHandler.ToDto(e)).ToList();
    }
}
