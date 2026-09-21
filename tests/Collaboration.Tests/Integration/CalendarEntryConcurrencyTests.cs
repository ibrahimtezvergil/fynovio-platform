using Collaboration.Domain;
using Collaboration.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Collaboration.Tests.Integration;

/// <summary>Optimistic concurrency token (RowVersion) enforcement.
/// The CalendarEntry aggregate increments RowVersion before persistence; EF Core's
/// update predicate includes the previous value, so a stale token never overwrites.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class CalendarEntryConcurrencyTests
{
    private readonly PostgresFixture _fixture;

    public CalendarEntryConcurrencyTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Two_concurrent_updates_second_throws_DbUpdateConcurrencyException()
    {
        var tenant = TestData.NextTenant();
        var principal = new PrincipalRef("issuer", "subject");

        // Seed an entry
        await using var seedContext = _fixture.CreateAdminContext();
        var entry = CalendarEntry.Create(
            tenant, principal, "Original Title", null, "#000000",
            false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
            null, null, null);
        seedContext.CalendarEntries.Add(entry);
        await seedContext.SaveChangesAsync();
        var entryId = entry.Id;

        // Load the entry in context 1 and context 2
        await using var context1 = _fixture.CreateAdminContext();
        var entry1 = await context1.CalendarEntries.SingleAsync(e => e.Id == entryId);

        await using var context2 = _fixture.CreateAdminContext();
        var entry2 = await context2.CalendarEntries.SingleAsync(e => e.Id == entryId);

        // Update in context 1
        entry1.Replace("Updated by Context 1", null, "#000000",
            false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2),
            null, null, null);
        await context1.SaveChangesAsync();

        // Try to update in context 2 — should fail because entry2's RowVersion is stale
        entry2.Replace("Updated by Context 2", null, "#ff0000",
            false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(3),
            null, null, null);

        var exception = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => context2.SaveChangesAsync());

        // Verify the title is still from context 1's update
        await using var verifyContext = _fixture.CreateAdminContext();
        var verifyEntry = await verifyContext.CalendarEntries.SingleAsync(e => e.Id == entryId);
        Assert.Equal("Updated by Context 1", verifyEntry.Title);
    }

    [Fact]
    public async Task Stale_row_version_prevents_update()
    {
        var tenant = TestData.NextTenant();
        var principal = new PrincipalRef("issuer", "subject");

        // Seed an entry
        await using var seedContext = _fixture.CreateAdminContext();
        var entry = CalendarEntry.Create(
            tenant, principal, "Initial Title", null, "#000000",
            false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
            null, null, null);
        seedContext.CalendarEntries.Add(entry);
        await seedContext.SaveChangesAsync();
        var entryId = entry.Id;

        // Load and update twice to advance RowVersion
        await using var updateContext1 = _fixture.CreateAdminContext();
        var entryV1 = await updateContext1.CalendarEntries.SingleAsync(e => e.Id == entryId);
        entryV1.Replace("Update 1", null, "#000000",
            false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
            null, null, null);
        await updateContext1.SaveChangesAsync();

        await using var updateContext2 = _fixture.CreateAdminContext();
        var entryV2 = await updateContext2.CalendarEntries.SingleAsync(e => e.Id == entryId);
        entryV2.Replace("Update 2", null, "#000000",
            false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
            null, null, null);
        await updateContext2.SaveChangesAsync();

        // Now try to use the stale v1 entry (which still has RowVersion = 1 in memory)
        entryV1.Replace("Stale Attempt", null, "#000000",
            false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
            null, null, null);

        var exception = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => updateContext1.SaveChangesAsync());

        Assert.NotNull(exception);
    }
}
