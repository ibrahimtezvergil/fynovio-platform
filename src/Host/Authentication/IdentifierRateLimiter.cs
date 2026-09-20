using System.Collections.Concurrent;

namespace Host.Authentication;

/// <summary>Per-identifier rate limiter for authentication endpoints (login, forgot).
/// Fixed windows per (purpose, normalised identifier): a limit of N allows N attempts per
/// window and rejects the N+1st until the window expires (1 minute for login, 1 hour for
/// forgot). Thread-safe, in-memory, singleton.
///
/// In-memory means per-instance: a multi-instance deployment needs a shared store to get a
/// global limit (recorded in the Phase 2.5A plan, §15). The per-IP `RateLimiter` policies
/// and the account lockout still apply on every instance.</summary>
public sealed class IdentifierRateLimiter
{
    private const int SweepEveryNCalls = 1024;

    private readonly int _loginPerMinute;
    private readonly int _forgotPerHour;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();
    private int _callsSinceSweep;

    public IdentifierRateLimiter(
        int loginPerMinute,
        int forgotPerHour,
        TimeProvider? timeProvider = null)
    {
        if (loginPerMinute < 1)
            throw new ArgumentOutOfRangeException(nameof(loginPerMinute), "Limit must be at least 1.");
        if (forgotPerHour < 1)
            throw new ArgumentOutOfRangeException(nameof(forgotPerHour), "Limit must be at least 1.");

        _loginPerMinute = loginPerMinute;
        _forgotPerHour = forgotPerHour;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Returns true when the attempt is within the limit (and counts it), false when
    /// the limit for this purpose/identifier is exhausted for the current window.</summary>
    public bool TryAcquire(string purpose, string normalisedIdentifier)
    {
        if (string.IsNullOrEmpty(purpose) || string.IsNullOrEmpty(normalisedIdentifier))
            throw new ArgumentException("Purpose and identifier cannot be empty");

        var (limit, window) = GetLimitAndWindow(purpose);
        var now = _timeProvider.GetUtcNow();
        SweepOccasionally(now);

        var bucket = _buckets.GetOrAdd($"{purpose}:{normalisedIdentifier}", _ => new Bucket(window));
        lock (bucket)
        {
            if (now >= bucket.WindowStart + bucket.Window)
            {
                bucket.WindowStart = now;
                bucket.Count = 0;
            }

            if (bucket.Count >= limit)
                return false;

            bucket.Count++;
            return true;
        }
    }

    private (int Limit, TimeSpan Window) GetLimitAndWindow(string purpose) => purpose switch
    {
        "login" => (_loginPerMinute, TimeSpan.FromMinutes(1)),
        "forgot" => (_forgotPerHour, TimeSpan.FromHours(1)),
        _ => throw new ArgumentException($"Unknown purpose: {purpose}")
    };

    /// <summary>Drops expired buckets so a stream of distinct identifiers cannot grow the
    /// dictionary without bound.</summary>
    private void SweepOccasionally(DateTimeOffset now)
    {
        if (Interlocked.Increment(ref _callsSinceSweep) < SweepEveryNCalls)
            return;

        Interlocked.Exchange(ref _callsSinceSweep, 0);
        foreach (var (key, bucket) in _buckets)
        {
            lock (bucket)
            {
                if (now >= bucket.WindowStart + bucket.Window)
                    _buckets.TryRemove(key, out _);
            }
        }
    }

    private sealed class Bucket(TimeSpan window)
    {
        public TimeSpan Window { get; } = window;
        public DateTimeOffset WindowStart { get; set; } = DateTimeOffset.MinValue;
        public int Count { get; set; }
    }
}
