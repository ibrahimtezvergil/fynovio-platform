using Collaboration.Application;
using Collaboration.Domain;
using Collaboration.Persistence;
using Contracts;
using Xunit;

namespace Collaboration.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class CalendarEntryHandlerTests(PostgresFixture fixture)
{
    [Fact]
    public async Task CreateAndGet_HappyPath()
    {
        // Setup
        var context = fixture.CreateCollaborationContext();
        await using var _ = context;
        var runtimeConnectionString = await fixture.RuntimeConnectionStringAsync();
        var runtimeContext = PostgresFixture.CreateCollaborationContext(runtimeConnectionString);
        await using var __ = runtimeContext;

        var tenantId = new TenantId(1);
        var principal = new PrincipalRef("test-issuer", "test-subject");
        var command = new CreateCalendarEntryCommand(
            tenantId, principal, "Test Entry", "Some notes", "#000000",
            false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
            null, null, null,
            "idempotency-key-1", Guid.NewGuid());

        var authorizer = StubAuthorizer.AlwaysAllow;
        var handler = new CreateCalendarEntryHandler(context, authorizer);

        // Act: Create
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.False(result.Replayed);
        Assert.True(result.Id > 0);
        Assert.Equal(1, result.RowVersion);

        // Act: Get
        var getQuery = new GetCalendarEntryQuery(tenantId, result.Id, principal, Guid.NewGuid());
        var getHandler = new GetCalendarEntryHandler(runtimeContext, authorizer);
        var getResult = await getHandler.HandleAsync(getQuery);

        // Assert
        Assert.NotNull(getResult);
        Assert.Equal(result.Id, getResult.Id);
        Assert.Equal("Test Entry", getResult.Title);
        Assert.Equal("Some notes", getResult.Notes);
        Assert.False(getResult.AllDay);
    }

    [Fact]
    public async Task Create_IdempotentOnReplay()
    {
        var context = fixture.CreateCollaborationContext();
        await using var _ = context;

        var tenantId = new TenantId(1);
        var principal = new PrincipalRef("test-issuer", "test-subject");
        var correlationId = Guid.NewGuid();
        var command = new CreateCalendarEntryCommand(
            tenantId, principal, "Test Entry", "Notes", "#000000",
            false, DateTimeOffset.UtcNow, null,
            null, null, null,
            "idempotency-key-2", correlationId);

        var authorizer = StubAuthorizer.AlwaysAllow;
        var handler = new CreateCalendarEntryHandler(context, authorizer);

        // First attempt
        var result1 = await handler.HandleAsync(command);
        Assert.False(result1.Replayed);

        // Replay with same key
        var result2 = await handler.HandleAsync(command);
        Assert.True(result2.Replayed);
        Assert.Equal(result1.Id, result2.Id);
        Assert.Equal(result1.RowVersion, result2.RowVersion);
    }

    [Fact]
    public async Task Create_ThrowsOnIdempotencyKeyReuse()
    {
        var context = fixture.CreateCollaborationContext();
        await using var _ = context;

        var tenantId = new TenantId(1);
        var principal = new PrincipalRef("test-issuer", "test-subject");
        var command1 = new CreateCalendarEntryCommand(
            tenantId, principal, "Entry 1", null, "#000000",
            false, DateTimeOffset.UtcNow, null,
            null, null, null,
            "idempotency-key-3", Guid.NewGuid());

        var command2 = new CreateCalendarEntryCommand(
            tenantId, principal, "Entry 2", null, "#ff0000",  // Different entry
            false, DateTimeOffset.UtcNow, null,
            null, null, null,
            "idempotency-key-3", Guid.NewGuid());  // Same key!

        var authorizer = StubAuthorizer.AlwaysAllow;
        var handler = new CreateCalendarEntryHandler(context, authorizer);

        await handler.HandleAsync(command1);

        // Attempt to create different entry with same key should throw
        var ex = await Assert.ThrowsAsync<IdempotencyKeyReusedException>(
            () => handler.HandleAsync(command2));
        Assert.Contains("idempotency-key-3", ex.Message);
    }

    [Fact]
    public async Task Create_ThrowsOnInvalidIdempotencyKey()
    {
        var context = fixture.CreateCollaborationContext();
        await using var _ = context;

        var tenantId = new TenantId(1);
        var principal = new PrincipalRef("test-issuer", "test-subject");

        var authorizer = StubAuthorizer.AlwaysAllow;
        var handler = new CreateCalendarEntryHandler(context, authorizer);

        // Blank key
        var command1 = new CreateCalendarEntryCommand(
            tenantId, principal, "Entry", null, "#000000",
            false, DateTimeOffset.UtcNow, null,
            null, null, null,
            "   ", Guid.NewGuid());

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(command1));

        // Key > 128 chars
        var command2 = new CreateCalendarEntryCommand(
            tenantId, principal, "Entry", null, "#000000",
            false, DateTimeOffset.UtcNow, null,
            null, null, null,
            new string('x', 129), Guid.NewGuid());

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(command2));
    }

    [Fact]
    public async Task Create_ThrowsOnAuthorizationDenial()
    {
        var context = fixture.CreateCollaborationContext();
        await using var _ = context;

        var tenantId = new TenantId(1);
        var principal = new PrincipalRef("test-issuer", "test-subject");
        var command = new CreateCalendarEntryCommand(
            tenantId, principal, "Entry", null, "#000000",
            false, DateTimeOffset.UtcNow, null,
            null, null, null,
            "idempotency-key-4", Guid.NewGuid());

        var authorizer = StubAuthorizer.AlwaysDeny;
        var handler = new CreateCalendarEntryHandler(context, authorizer);

        var ex = await Assert.ThrowsAsync<CalendarEntryAuthorizationDeniedException>(
            () => handler.HandleAsync(command));
        Assert.Equal("collaboration.calendar_entry.create", ex.ActionKey);
        Assert.Equal(AuthorizationDenialStage.Coarse, ex.DenialStage);
    }

    [Fact]
    public async Task Get_ReturnsNullForNonOwner()
    {
        var context = fixture.CreateCollaborationContext();
        await using var _ = context;
        var runtimeConnectionString = await fixture.RuntimeConnectionStringAsync();
        var runtimeContext = PostgresFixture.CreateCollaborationContext(runtimeConnectionString);
        await using var __ = runtimeContext;

        var tenantId = new TenantId(1);
        var owner = new PrincipalRef("test-issuer", "owner-subject");
        var nonOwner = new PrincipalRef("test-issuer", "other-subject");

        // Create as owner
        var command = new CreateCalendarEntryCommand(
            tenantId, owner, "Entry", null, "#000000",
            false, DateTimeOffset.UtcNow, null,
            null, null, null,
            "idempotency-key-5", Guid.NewGuid());

        var authorizer = StubAuthorizer.AlwaysAllow;
        var createHandler = new CreateCalendarEntryHandler(context, authorizer);
        var result = await createHandler.HandleAsync(command);

        // Try to get as non-owner
        var getQuery = new GetCalendarEntryQuery(tenantId, result.Id, nonOwner, Guid.NewGuid());
        var getHandler = new GetCalendarEntryHandler(runtimeContext, authorizer);
        var getResult = await getHandler.HandleAsync(getQuery);

        // Should return null (not found to avoid leaking existence)
        Assert.Null(getResult);
    }

    [Fact]
    public async Task Get_ReturnsNullWhenAuthorizationDenied()
    {
        var context = fixture.CreateCollaborationContext();
        await using var _ = context;
        var runtimeConnectionString = await fixture.RuntimeConnectionStringAsync();
        var runtimeContext = PostgresFixture.CreateCollaborationContext(runtimeConnectionString);
        await using var __ = runtimeContext;

        var tenantId = new TenantId(1);
        var principal = new PrincipalRef("test-issuer", "test-subject");

        // Create entry
        var command = new CreateCalendarEntryCommand(
            tenantId, principal, "Entry", null, "#000000",
            false, DateTimeOffset.UtcNow, null,
            null, null, null,
            "idempotency-key-6", Guid.NewGuid());

        var allowAuthorizer = StubAuthorizer.AlwaysAllow;
        var createHandler = new CreateCalendarEntryHandler(context, allowAuthorizer);
        var result = await createHandler.HandleAsync(command);

        // Try to get with record-level denial
        var getQuery = new GetCalendarEntryQuery(tenantId, result.Id, principal, Guid.NewGuid());
        var getHandler = new GetCalendarEntryHandler(runtimeContext, StubAuthorizer.RecordDenied);
        var getResult = await getHandler.HandleAsync(getQuery);

        // Should return null (not found)
        Assert.Null(getResult);
    }

    [Fact]
    public async Task List_WithinRange_Timed()
    {
        var context = fixture.CreateCollaborationContext();
        await using var _ = context;
        var runtimeConnectionString = await fixture.RuntimeConnectionStringAsync();
        var runtimeContext = PostgresFixture.CreateCollaborationContext(runtimeConnectionString);
        await using var __ = runtimeContext;

        var tenantId = new TenantId(1001);  // Use unique tenant to avoid cross-test pollution
        var principal = new PrincipalRef("test-issuer", "list-timed-subject");
        var baseTime = DateTimeOffset.UtcNow;

        var entries = new[]
        {
            // Entry that starts inside the range
            new CreateCalendarEntryCommand(
                tenantId, principal, "Entry 1", null, "#000000",
                false, baseTime.AddHours(1), baseTime.AddHours(2),
                null, null, null,
                "key-list-1", Guid.NewGuid()),
            // Entry that ends inside the range
            new CreateCalendarEntryCommand(
                tenantId, principal, "Entry 2", null, "#000000",
                false, baseTime.AddHours(-2), baseTime.AddHours(-1),
                null, null, null,
                "key-list-2", Guid.NewGuid()),
            // Entry that spans the range
            new CreateCalendarEntryCommand(
                tenantId, principal, "Entry 3", null, "#000000",
                false, baseTime.AddHours(-1), baseTime.AddHours(1),
                null, null, null,
                "key-list-3", Guid.NewGuid()),
        };

        var authorizer = StubAuthorizer.AlwaysAllow;
        var createHandler = new CreateCalendarEntryHandler(context, authorizer);

        foreach (var entry in entries)
        {
            await createHandler.HandleAsync(entry);
        }

        // List entries within the range (baseTime to baseTime + 3 hours)
        var listQuery = new ListCalendarEntriesQuery(
            tenantId, principal, baseTime, baseTime.AddHours(3), Guid.NewGuid());

        var listHandler = new ListCalendarEntriesHandler(runtimeContext, authorizer);
        var results = await listHandler.HandleAsync(listQuery);

        // Entry 1: starts at baseTime+1h, ends at baseTime+2h → should be included
        // Entry 2: ends at baseTime-1h → should NOT be included (ends before range starts)
        // Entry 3: starts at baseTime-1h, ends at baseTime+1h → should be included (spans range)
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Title == "Entry 1");
        Assert.Contains(results, r => r.Title == "Entry 3");
        Assert.DoesNotContain(results, r => r.Title == "Entry 2");
    }

    [Fact]
    public async Task List_RangeValidation()
    {
        var context = fixture.CreateCollaborationContext();
        await using var _ = context;

        var tenantId = new TenantId(1);
        var principal = new PrincipalRef("test-issuer", "test-subject");
        var baseTime = DateTimeOffset.UtcNow;

        var authorizer = StubAuthorizer.AlwaysAllow;
        var listHandler = new ListCalendarEntriesHandler(context, authorizer);

        // Range with To <= From
        var badQuery1 = new ListCalendarEntriesQuery(
            tenantId, principal, baseTime, baseTime, Guid.NewGuid());
        await Assert.ThrowsAsync<ArgumentException>(
            () => listHandler.HandleAsync(badQuery1));

        // Range > 100 days
        var badQuery2 = new ListCalendarEntriesQuery(
            tenantId, principal, baseTime, baseTime.AddDays(101), Guid.NewGuid());
        await Assert.ThrowsAsync<CalendarRangeTooLargeException>(
            () => listHandler.HandleAsync(badQuery2));
    }

    [Fact]
    public async Task List_ThrowsOnTooManyResults()
    {
        var context = fixture.CreateCollaborationContext();
        await using var _ = context;
        var runtimeConnectionString = await fixture.RuntimeConnectionStringAsync();
        var runtimeContext = PostgresFixture.CreateCollaborationContext(runtimeConnectionString);
        await using var __ = runtimeContext;

        var tenantId = new TenantId(1002);  // Use unique tenant
        var principal = new PrincipalRef("test-issuer", "list-overflow-subject");
        var baseTime = DateTimeOffset.UtcNow;

        // Create 501 entries
        var authorizer = StubAuthorizer.AlwaysAllow;
        var createHandler = new CreateCalendarEntryHandler(context, authorizer);

        for (int i = 0; i <= 500; i++)
        {
            var command = new CreateCalendarEntryCommand(
                tenantId, principal, $"Entry {i}", null, "#000000",
                false, baseTime.AddHours(i * 0.1), baseTime.AddHours(i * 0.1 + 0.05),
                null, null, null,
                $"key-overflow-{i}", Guid.NewGuid());
            await createHandler.HandleAsync(command);
        }

        // List should throw because we have 501 results
        var listQuery = new ListCalendarEntriesQuery(
            tenantId, principal, baseTime, baseTime.AddDays(5), Guid.NewGuid());

        var listHandler = new ListCalendarEntriesHandler(runtimeContext, authorizer);
        await Assert.ThrowsAsync<CalendarRangeTooLargeException>(
            () => listHandler.HandleAsync(listQuery));
    }

    [Fact]
    public async Task List_ThrowsOnAuthorizationDenial()
    {
        var context = fixture.CreateCollaborationContext();
        await using var _ = context;

        var tenantId = new TenantId(1);
        var principal = new PrincipalRef("test-issuer", "test-subject");
        var baseTime = DateTimeOffset.UtcNow;

        var listQuery = new ListCalendarEntriesQuery(
            tenantId, principal, baseTime, baseTime.AddHours(1), Guid.NewGuid());

        var listHandler = new ListCalendarEntriesHandler(context, StubAuthorizer.AlwaysDeny);
        var ex = await Assert.ThrowsAsync<CalendarEntryAuthorizationDeniedException>(
            () => listHandler.HandleAsync(listQuery));
        Assert.Equal("collaboration.calendar_entry.list", ex.ActionKey);
    }
}
