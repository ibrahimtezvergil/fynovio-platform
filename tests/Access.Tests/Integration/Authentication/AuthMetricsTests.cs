using System.Diagnostics.Metrics;
using Access.Application.Authentication;
using Access.Domain.Authentication;
using Access.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Access.Tests.Integration.Authentication;

/// <summary>Records the `Fynovio.Auth` measurements made by the current async flow only, so tests running in
/// parallel (they share the static meter) cannot count each other's failures.</summary>
internal static class MetricCapture
{
    private static readonly AsyncLocal<List<(string Name, long Value, string? Reason)>?> Current = new();
    private static readonly MeterListener Listener = Create();

    private static MeterListener Create()
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == AuthMetrics.MeterName)
                    l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            string? reason = null;
            foreach (var tag in tags)
                if (tag.Key is "reason" or "policy")
                    reason = tag.Value?.ToString();
            Current.Value?.Add((instrument.Name, value, reason));
        });
        listener.Start();
        return listener;
    }

    public static List<(string Name, long Value, string? Reason)> Begin()
    {
        _ = Listener;
        var list = new List<(string, long, string?)>();
        Current.Value = list;
        return list;
    }
}

public sealed class AuthMetricsTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture = new();
    private AccessDbContext _context = null!;

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
        _context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
    }

    public async Task DisposeAsync()
    {
        _context.Dispose();
        await _fixture.DisposeAsync();
    }

    private AuthenticateHandler NewAuthenticateHandler() => new(
        _context,
        new PasswordService(),
        new LockoutOptions { MaxFailedAttempts = 2, LockoutMinutes = 15 },
        new SessionOptions { PlatformIssuer = "https://platform.example.com" },
        new AuthEventWriter(_context),
        _fixture.TimeProvider);

    private async Task<long> ProvisionAsync(string email)
    {
        var result = await new ProvisionPasswordAccountHandler(
                _context, new PasswordService(), new PasswordPolicy(new PasswordPolicyOptions()), _fixture.TimeProvider)
            .HandleAsync(new ProvisionPasswordAccountCommand(email, "Test User", "ValidPassword12345", "https://platform.example.com"));
        return result.AccountId;
    }

    [Fact]
    public async Task Failed_sign_ins_are_counted_by_reason_and_a_success_is_not()
    {
        await ProvisionAsync("metrics@example.com");
        var handler = NewAuthenticateHandler();
        var recorded = MetricCapture.Begin();

        await handler.HandleAsync(new AuthenticateCommand("nobody@example.com", "whatever-password"));
        await handler.HandleAsync(new AuthenticateCommand("metrics@example.com", "wrong-password-1"));
        await handler.HandleAsync(new AuthenticateCommand("metrics@example.com", "wrong-password-2")); // reaches the lockout threshold
        await handler.HandleAsync(new AuthenticateCommand("metrics@example.com", "ValidPassword12345")); // locked → still a failure

        Assert.Equal(
            ["unknown_user", "bad_password", "account_locked", "account_locked"],
            recorded.Where(m => m.Name == "auth.login.failed").Select(m => m.Reason!).ToArray());
        Assert.All(recorded, m => Assert.Equal(1, m.Value));
    }

    [Fact]
    public async Task A_successful_sign_in_counts_nothing()
    {
        await ProvisionAsync("metrics-ok@example.com");
        var recorded = MetricCapture.Begin();

        var result = await NewAuthenticateHandler().HandleAsync(new AuthenticateCommand("metrics-ok@example.com", "ValidPassword12345"));

        Assert.NotEqual(AuthenticationStatus.InvalidCredentials, result.Status);
        Assert.Empty(recorded);
    }

    [Fact]
    public async Task Refresh_token_reuse_is_counted_once_per_detection()
    {
        var accountId = await ProvisionAsync("metrics-reuse@example.com");
        var now = _fixture.TimeProvider.GetUtcNow();
        var session = AuthSession.Create(accountId, now, now.AddDays(30));
        var (secret, hash) = TokenSecrets.GenerateAndHash();
        var token = RefreshToken.Create(session.Id, hash, now, now.AddDays(7));
        _context.AuthSessions.Add(session);
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync();
        var handler = new RefreshSessionHandler(
            _context,
            new SessionOptions { PlatformIssuer = "https://platform.example.com", RefreshAbsoluteDays = 30, RefreshGraceSeconds = 5 },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);
        var original = $"{token.Id}.{secret}";

        var recorded = MetricCapture.Begin();
        Assert.Equal(RefreshResult.Success, (await handler.HandleAsync(new RefreshSessionCommand(original))).Status);
        Assert.Empty(recorded); // a legitimate rotation is not reuse

        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(10));
        await handler.HandleAsync(new RefreshSessionCommand(original));

        Assert.Equal(["auth.refresh.reuse"], recorded.Select(m => m.Name).ToArray());
        Assert.True(await _context.AuthSessions.AnyAsync(s => s.Id == session.Id && s.RevokedReason == "reuse_detected"));
    }
}
