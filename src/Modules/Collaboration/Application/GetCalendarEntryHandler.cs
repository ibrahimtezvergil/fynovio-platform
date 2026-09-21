using Collaboration.Domain;
using Collaboration.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Collaboration.Application;

public sealed class GetCalendarEntryHandler(CollaborationDbContext context, IAuthorizer authorizer)
{
    public async Task<CalendarEntryDto?> HandleAsync(GetCalendarEntryQuery query, CancellationToken cancellationToken = default)
    {
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);
        var entry = await context.CalendarEntries.SingleOrDefaultAsync(e => e.TenantId == query.TenantId && e.Id == query.Id && e.OwnerPrincipalIssuer == query.Principal.Issuer && e.OwnerPrincipalSubject == query.Principal.Subject, cancellationToken);
        if (entry is null) return null;
        var allowed = await authorizer.AuthorizeAsync(new AuthorizationRequest(new ActorContext(query.TenantId, query.Principal, query.CorrelationId), new ActionKey(CollaborationActionKeys.CalendarEntryRead), new ResourceDescriptor(nameof(CalendarEntry), entry.Id, entry.Owner)), cancellationToken);
        return allowed.IsAllowed ? ToDto(entry) : null;
    }

    internal static CalendarEntryDto ToDto(CalendarEntry entry) => new(entry.Id, entry.RowVersion, entry.Title, entry.Notes, entry.Color, entry.AllDay, entry.StartAt, entry.EndAt, entry.StartDate, entry.EndDate, entry.Link);
}
