using Collaboration.Domain;
using Collaboration.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Collaboration.Application;

public sealed class GetCalendarEntryHandler(CollaborationDbContext context, IAuthorizer authorizer, ILinkTargetDirectory links)
{
    private const string ActionKeyValue = "collaboration.calendar_entry.read";

    public async Task<CalendarEntryDto?> HandleAsync(GetCalendarEntryQuery query, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);

        var entry = await context.CalendarEntries
            .SingleOrDefaultAsync(
                e => e.TenantId == query.TenantId
                    && e.Id == query.Id
                    && e.OwnerPrincipalIssuer == query.Principal.Issuer
                    && e.OwnerPrincipalSubject == query.Principal.Subject,
                cancellationToken);
        if (entry is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var actor = new ActorContext(query.TenantId, query.Principal, query.CorrelationId);
        var resource = new ResourceDescriptor(nameof(CalendarEntry), entry.Id, entry.Owner);
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey(ActionKeyValue), resource), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // Record-level denial looks identical to not-found — never 403 for "exists, not yours"
        // (architecture plan §15, binding spec §12's tenant non-leak rule).
        if (!decision.IsAllowed)
            return null;

        // Hydrated after the transaction closed, so no database connection is held while other modules are asked.
        return (await CalendarLinks.ToDtosAsync(links, actor, [entry], cancellationToken))[0];
    }
}
