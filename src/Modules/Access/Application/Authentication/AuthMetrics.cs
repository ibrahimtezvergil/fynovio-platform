using System.Diagnostics.Metrics;

namespace Access.Application.Authentication;

/// <summary>Counters for the security-relevant auth outcomes, on the `Fynovio.Auth` meter, so a future
/// OpenTelemetry exporter can pick them up (no exporter package is part of this phase). Tags are short,
/// low-cardinality codes — never an address, account id, IP or token.</summary>
public static class AuthMetrics
{
    public const string MeterName = "Fynovio.Auth";

    private static readonly Meter Meter = new(MeterName);

    /// <summary>Tag `reason`: `unknown_user`, `account_locked`, `bad_password`.</summary>
    public static readonly Counter<long> LoginFailed = Meter.CreateCounter<long>("auth.login.failed", description: "Failed sign-in attempts");

    /// <summary>A rotated refresh token was replayed after its grace window: the whole session was revoked.</summary>
    public static readonly Counter<long> RefreshReuse = Meter.CreateCounter<long>("auth.refresh.reuse", description: "Refresh-token reuse detections");

    /// <summary>Tag `policy`: the rate-limit policy (or `login-identifier`) that refused the request.</summary>
    public static readonly Counter<long> RateLimitRejected = Meter.CreateCounter<long>("auth.ratelimit.rejected", description: "Requests refused by a rate limit");
}
