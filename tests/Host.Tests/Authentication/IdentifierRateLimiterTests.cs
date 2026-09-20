using Host.Authentication;
using Xunit;

namespace Host.Tests.Authentication;

public sealed class IdentifierRateLimiterTests
{
    [Fact]
    public void TryAcquire_succeeds_within_limit()
    {
        var limiter = new IdentifierRateLimiter(
            loginPerMinute: 3,
            forgotPerHour: 2);

        var result1 = limiter.TryAcquire("login", "test@example.com");
        var result2 = limiter.TryAcquire("login", "test@example.com");
        var result3 = limiter.TryAcquire("login", "test@example.com");

        Assert.True(result1);
        Assert.True(result2);
        Assert.True(result3);
    }

    [Fact]
    public void TryAcquire_fails_over_limit()
    {
        var limiter = new IdentifierRateLimiter(
            loginPerMinute: 2,
            forgotPerHour: 3);

        limiter.TryAcquire("login", "test@example.com");
        limiter.TryAcquire("login", "test@example.com");
        var result = limiter.TryAcquire("login", "test@example.com");

        Assert.False(result);
    }

    [Fact]
    public void TryAcquire_different_identifiers_independent()
    {
        var limiter = new IdentifierRateLimiter(
            loginPerMinute: 2,
            forgotPerHour: 3);

        limiter.TryAcquire("login", "alice@example.com");
        limiter.TryAcquire("login", "alice@example.com");
        var aliceBlocked = limiter.TryAcquire("login", "alice@example.com");

        var bobAllow1 = limiter.TryAcquire("login", "bob@example.com");
        var bobAllow2 = limiter.TryAcquire("login", "bob@example.com");

        Assert.False(aliceBlocked);
        Assert.True(bobAllow1);
        Assert.True(bobAllow2);
    }

    [Fact]
    public void TryAcquire_different_purposes_independent()
    {
        var limiter = new IdentifierRateLimiter(
            loginPerMinute: 1,
            forgotPerHour: 1);

        var loginAllow = limiter.TryAcquire("login", "test@example.com");
        var loginBlock = limiter.TryAcquire("login", "test@example.com");
        var forgotAllow = limiter.TryAcquire("forgot", "test@example.com");

        Assert.True(loginAllow);
        Assert.False(loginBlock);
        Assert.True(forgotAllow);
    }

    [Fact]
    public void TryAcquire_window_reset_with_injectable_clock()
    {
        var clock = new TestTimeProvider();
        var limiter = new IdentifierRateLimiter(
            loginPerMinute: 1,
            forgotPerHour: 2,
            timeProvider: clock);

        limiter.TryAcquire("login", "test@example.com");
        var blockedInWindow = limiter.TryAcquire("login", "test@example.com");

        // Advance past the minute window
        clock.Advance(TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(1)));
        var allowedAfterWindow = limiter.TryAcquire("login", "test@example.com");

        Assert.False(blockedInWindow);
        Assert.True(allowedAfterWindow);
    }

    [Fact]
    public void TryAcquire_hour_window_reset_with_injectable_clock()
    {
        var clock = new TestTimeProvider();
        var limiter = new IdentifierRateLimiter(
            loginPerMinute: 5,
            forgotPerHour: 2,
            timeProvider: clock);

        limiter.TryAcquire("forgot", "test@example.com");
        limiter.TryAcquire("forgot", "test@example.com");
        var blockedInWindow = limiter.TryAcquire("forgot", "test@example.com");

        // Advance past the hour window
        clock.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1)));
        var allowedAfterWindow = limiter.TryAcquire("forgot", "test@example.com");

        Assert.False(blockedInWindow);
        Assert.True(allowedAfterWindow);
    }
}

/// <summary>Mock TimeProvider for testing window resets.</summary>
internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _currentTime = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _currentTime;

    public void Advance(TimeSpan duration)
    {
        _currentTime = _currentTime.Add(duration);
    }
}
