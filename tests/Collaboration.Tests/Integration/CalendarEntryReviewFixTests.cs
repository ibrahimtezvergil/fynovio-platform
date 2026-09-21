using Collaboration.Application;
using Collaboration.Domain;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Collaboration.Tests.Integration;

/// <summary>What the independent S1 review found: input the database cannot store must be a validation error, never a
/// database error, and a denial must never degrade into an existence oracle.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class CalendarEntryReviewFixTests(PostgresFixture fixture)
{
    private readonly Harness _harness = new(fixture);

    [Theory]
    [InlineData("nul\0title", null)]
    [InlineData("Title", "nul\0notes")]
    [InlineData("vertical\u000Btab", null)]
    [InlineData("Title", "bell\u0007notes")]
    public async Task Text_the_database_cannot_hold_is_a_validation_error_on_create_and_never_a_database_error(string title, string? notes)
    {
        var tenant = TestData.NextTenant();

        var ex = await Record.ExceptionAsync(() => _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-text", title: title, notes: notes)));

        Assert.IsType<ArgumentException>(ex);
        Assert.Equal(0, await _harness.CountEntriesAsync(tenant));
        Assert.Equal(0, await _harness.CountIdempotencyAsync(tenant));
    }

    [Theory]
    [InlineData("nul\0title", null)]
    [InlineData("Title", "nul\0notes")]
    public async Task The_same_text_is_a_validation_error_on_update_and_the_entry_is_untouched(string title, string? notes)
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "seed", title: "Original"));

        var ex = await Record.ExceptionAsync(() =>
            _harness.UpdateAsync(Commands.Update(tenant, created.Id, Harness.Alice, 1, "k-text", title: title, notes: notes)));

        Assert.IsType<ArgumentException>(ex);
        Assert.IsNotType<DbUpdateException>(ex);
        Assert.Equal("Original", (await _harness.ReadEntryAsync(tenant, created.Id))!.Title);
    }

    [Fact]
    public async Task An_end_less_than_a_microsecond_after_the_start_is_a_validation_error_not_a_check_violation()
    {
        var tenant = TestData.NextTenant();

        var ex = await Record.ExceptionAsync(() => _harness.CreateAsync(
            Commands.Timed(tenant, Harness.Alice, "k-ticks", start: Commands.Noon, end: Commands.Noon.AddTicks(5))));

        Assert.IsType<ArgumentException>(ex);
        Assert.Equal(0, await _harness.CountEntriesAsync(tenant));
    }

    [Fact]
    public async Task The_request_hash_treats_sub_microsecond_differences_as_the_same_request()
    {
        var tenant = TestData.NextTenant();
        var first = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-hash", start: Commands.Noon));

        var retry = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-hash", start: Commands.Noon.AddTicks(5)));

        Assert.True(retry.Replayed);
        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(Commands.Noon, (await _harness.ReadEntryAsync(tenant, first.Id))!.StartAt);
        Assert.Equal(1, await _harness.CountEntriesAsync(tenant));
    }

    [Fact]
    public async Task The_same_holds_for_update_hashing()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "seed"));
        var first = await _harness.UpdateAsync(Commands.Update(tenant, created.Id, Harness.Alice, 1, "k-hash-u", start: Commands.Noon.AddHours(2)));

        var retry = await _harness.UpdateAsync(Commands.Update(tenant, created.Id, Harness.Alice, 1, "k-hash-u", start: Commands.Noon.AddHours(2).AddTicks(9)));

        Assert.True(retry.Replayed);
        Assert.Equal(first.RowVersion, retry.RowVersion);
    }

    [Fact]
    public void A_record_stage_denial_without_an_entry_id_cannot_be_constructed()
    {
        Assert.Throws<System.Diagnostics.UnreachableException>(() =>
            new CalendarEntryAuthorizationDeniedException("collaboration.calendar_entry.update", "not_owner", AuthorizationDenialStage.Record));
        Assert.Throws<System.Diagnostics.UnreachableException>(() =>
            new CalendarEntryAuthorizationDeniedException("collaboration.calendar_entry.update", "not_owner", AuthorizationDenialStage.Record, entryId: null));

        Assert.Equal(7, new CalendarEntryAuthorizationDeniedException("k", "r", AuthorizationDenialStage.Record, 7).EntryId);
        Assert.Null(new CalendarEntryAuthorizationDeniedException("k", "r", AuthorizationDenialStage.Coarse).EntryId);
    }

    [Fact]
    public async Task A_record_stage_answer_to_a_capability_check_is_reported_as_a_coarse_denial_on_every_command()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "seed"));
        var window = (From: Commands.Noon.AddDays(-1), To: Commands.Noon.AddDays(1));

        var create = await Assert.ThrowsAsync<CalendarEntryAuthorizationDeniedException>(() =>
            _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-c"), StubAuthorizer.RecordDenied));
        var list = await Assert.ThrowsAsync<CalendarEntryAuthorizationDeniedException>(() =>
            _harness.ListAsync(tenant, Harness.Alice, window.From, window.To, StubAuthorizer.RecordDenied));
        var update = await Assert.ThrowsAsync<CalendarEntryAuthorizationDeniedException>(() =>
            _harness.UpdateAsync(Commands.Update(tenant, created.Id, Harness.Alice, 1, "k-u"), StubAuthorizer.RecordDenied));
        var delete = await Assert.ThrowsAsync<CalendarEntryAuthorizationDeniedException>(() =>
            _harness.DeleteAsync(Commands.Delete(tenant, created.Id, Harness.Alice, 1, "k-d"), StubAuthorizer.RecordDenied));

        Assert.All(new[] { create, list, update, delete }, ex =>
        {
            Assert.Equal(AuthorizationDenialStage.Coarse, ex.DenialStage);
            Assert.Null(ex.EntryId);
        });
    }
}
