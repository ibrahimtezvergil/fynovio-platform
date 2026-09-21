using Collaboration.Domain;
using Collaboration.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Collaboration.Application;

public sealed class ListCalendarEntriesHandler(CollaborationDbContext context, IAuthorizer authorizer)
{
    public const int MaxRangeDays = 100;
    public const int MaxResults = 500;

    public async Task<IReadOnlyList<CalendarEntryDto>> HandleAsync(ListCalendarEntriesQuery query, CancellationToken cancellationToken = default)
    {
        if (query.To <= query.From || query.To - query.From > TimeSpan.FromDays(MaxRangeDays)) throw new ArgumentOutOfRangeException(nameof(query), "Calendar range must be positive and at most 100 days.");
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);
        var allowed = await authorizer.AuthorizeAsync(new AuthorizationRequest(new ActorContext(query.TenantId, query.Principal, query.CorrelationId), new ActionKey(CollaborationActionKeys.CalendarEntryList), new ResourceDescriptor(nameof(CalendarEntry), null, query.Principal)), cancellationToken);
        if (!allowed.IsAllowed) throw new CollaborationAuthorizationDeniedException(CollaborationActionKeys.CalendarEntryList);
        var fromDate = DateOnly.FromDateTime(query.From.UtcDateTime).AddDays(-1);
        var toDate = DateOnly.FromDateTime(query.To.UtcDateTime).AddDays(1);
        return await context.CalendarEntries.AsNoTracking().Where(e => e.TenantId == query.TenantId && e.OwnerPrincipalIssuer == query.Principal.Issuer && e.OwnerPrincipalSubject == query.Principal.Subject && ((!e.AllDay && e.StartAt < query.To && (e.EndAt == null || e.EndAt > query.From)) || (e.AllDay && e.StartDate < toDate && e.EndDate > fromDate))).OrderBy(e => e.AllDay).ThenBy(e => e.StartAt).ThenBy(e => e.StartDate).Take(MaxResults).Select(e => GetCalendarEntryHandler.ToDto(e)).ToListAsync(cancellationToken);
    }
}
