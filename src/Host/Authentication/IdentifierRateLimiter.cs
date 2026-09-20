using System.Collections.Concurrent;

namespace Host.Authentication;

/// <summary>Per-identifier rate limiter for authentication endpoints (login, forgot).
/// Tracks login and forgot attempts per normalized email, with separate windows
/// (1 minute for login, 1 hour for forgot). Thread-safe, in-memory, singleton.
/// Configurable thresholds via IdentifierRateLimitingOptions.</summary>
public sealed class IdentifierRateLimiter
{
    private readonly int _loginPerMinute;
    private readonly int _forgotPerHour;
    private readonly TimeProvider _timeProvider;

    // Key: purpose (login/forgot) + normalized identifier (e.g., "login:test@example.com")
    // Value: list of (timestamp, count) for each window
    private readonly ConcurrentDictionary<string, IdentifierWindow> _windows = new();

    public IdentifierRateLimiter(
        int loginPerMinute,
        int forgotPerHour,
        TimeProvider? timeProvider = null)
    {
        _loginPerMinute = loginPerMinute;
        _forgotPerHour = forgotPerHour;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Try to acquire a slot for the given purpose and identifier.
    /// Returns true if within limit, false if limit exceeded.</summary>
    public bool TryAcquire(string purpose, string normalisedIdentifier)
    {
        if (string.IsNullOrEmpty(purpose) || string.IsNullOrEmpty(normalisedIdentifier))
            throw new ArgumentException("Purpose and identifier cannot be empty");

        var key = $"{purpose}:{normalisedIdentifier}";
        var now = _timeProvider.GetUtcNow();
        var (limit, window) = GetLimitAndWindow(purpose);

        return _windows.AddOrUpdate(
            key,
            _ => new IdentifierWindow(now, 1, limit, window),
            (_, existingWindow) =>
            {
                // Check if the window has expired
                if (now >= existingWindow.WindowStart.Add(existingWindow.WindowDuration))
                {
                    // Window expired, reset
                    return new IdentifierWindow(now, 1, limit, window);
                }

                // Still in window
                if (existingWindow.Count >= existingWindow.Limit)
                {
                    // Over limit, don't increment
                    return existingWindow;
                }

                // Within limit, increment and allow
                existingWindow.Count++;
                return existingWindow;
            }).Count < limit;
    }

    private (int limit, TimeSpan window) GetLimitAndWindow(string purpose) => purpose switch
    {
        "login" => (_loginPerMinute, TimeSpan.FromMinutes(1)),
        "forgot" => (_forgotPerHour, TimeSpan.FromHours(1)),
        _ => throw new ArgumentException($"Unknown purpose: {purpose}")
    };

    /// <summary>Tracks state for a specific (purpose, identifier) pair.</summary>
    private sealed class IdentifierWindow
    {
        public DateTimeOffset WindowStart { get; }
        public int Count { get; set; }
        public int Limit { get; }
        public TimeSpan WindowDuration { get; }

        public IdentifierWindow(DateTimeOffset windowStart, int count, int limit, TimeSpan windowDuration)
        {
            WindowStart = windowStart;
            Count = count;
            Limit = limit;
            WindowDuration = windowDuration;
        }
    }
}
