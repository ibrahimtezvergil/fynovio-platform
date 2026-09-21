using Collaboration.Application;
using Collaboration.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Collaboration.Tests.Integration;

/// <summary>Runs the handlers exactly as production does: through the unprivileged runtime role
/// (subject to RLS), one fresh DbContext per call. Seeding and row counting use the admin role.</summary>
internal sealed class Harness(PostgresFixture fixture)
{
    public static readonly PrincipalRef Alice = new("test-issuer", "alice");
    public static readonly PrincipalRef Bob = new("test-issuer", "bob");

    public async Task<CollaborationDbContext> RuntimeContextAsync() =>
        PostgresFixture.CreateContext(await fixture.RuntimeConnectionStringAsync());

    public async Task<CreateCalendarEntryResult> CreateAsync(CreateCalendarEntryCommand command, IAuthorizer? authorizer = null)
    {
        await using var context = await RuntimeContextAsync();
        return await new CreateCalendarEntryHandler(context, authorizer ?? StubAuthorizer.AlwaysAllow).HandleAsync(command);
    }

    public async Task<CalendarEntryDto?> GetAsync(TenantId tenant, long id, PrincipalRef principal, IAuthorizer? authorizer = null)
    {
        await using var context = await RuntimeContextAsync();
        return await new GetCalendarEntryHandler(context, authorizer ?? StubAuthorizer.AlwaysAllow)
            .HandleAsync(new GetCalendarEntryQuery(tenant, id, principal, Guid.NewGuid()));
    }

    public async Task<IReadOnlyList<CalendarEntryDto>> ListAsync(
        TenantId tenant, PrincipalRef principal, DateTimeOffset from, DateTimeOffset to, IAuthorizer? authorizer = null)
    {
        await using var context = await RuntimeContextAsync();
        return await new ListCalendarEntriesHandler(context, authorizer ?? StubAuthorizer.AlwaysAllow)
            .HandleAsync(new ListCalendarEntriesQuery(tenant, principal, from, to, Guid.NewGuid()));
    }

    public async Task<int> CountEntriesAsync(TenantId tenant)
    {
        await using var context = fixture.CreateAdminContext();
        return await context.CalendarEntries.CountAsync(e => e.TenantId == tenant);
    }

    public async Task<int> CountOutboxAsync(TenantId tenant)
    {
        await using var context = fixture.CreateAdminContext();
        return await context.OutboxMessages.CountAsync(m => m.TenantId == tenant);
    }

    public async Task<int> CountIdempotencyAsync(TenantId tenant)
    {
        await using var context = fixture.CreateAdminContext();
        return await context.IdempotencyRecords.CountAsync(r => r.TenantId == tenant);
    }
}

internal static class Commands
{
    public static readonly DateTimeOffset Noon = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    public static CreateCalendarEntryCommand Timed(
        TenantId tenant, PrincipalRef owner, string key, string title = "Entry", string? notes = null,
        string color = "#336699", DateTimeOffset? start = null, DateTimeOffset? end = null, EntityRef? link = null,
        Guid? correlationId = null) =>
        new(tenant, owner, title, notes, color, false, start ?? Noon, end, null, null, link, key, correlationId ?? Guid.NewGuid());

    public static CreateCalendarEntryCommand AllDay(
        TenantId tenant, PrincipalRef owner, string key, DateOnly start, DateOnly end, string title = "All day") =>
        new(tenant, owner, title, null, "#336699", true, null, null, start, end, null, key, Guid.NewGuid());
}
