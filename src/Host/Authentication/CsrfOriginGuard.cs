using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Host.Authentication;

/// <summary>CSRF protection guard for state-changing endpoints (cookie-authenticated and
/// anonymous auth endpoints).
///
/// Passes only if:
/// (a) Header X-Requested-With: fynovio is present
/// (b) Origin (or Referer fallback) is in AllowedOrigins OR matches request's own origin
/// (c) Sec-Fetch-Site is not 'cross-site' (when present)
///
/// Failure → 403 ProblemDetails with type="csrf_rejected" (no detail about which check failed).</summary>
public static class CsrfOriginGuard
{
    /// <summary>Validate CSRF checks on the request.
    /// Returns true if the request passes all checks; false (403 Forbidden) if any check fails.</summary>
    public static bool ValidateRequest(HttpContext context, string[] allowedOrigins)
    {
        // Check (a): X-Requested-With header must be present and equal to "fynovio"
        if (!context.Request.Headers.TryGetValue("X-Requested-With", out var requestedWith) ||
            requestedWith.ToString() != "fynovio")
        {
            return false;
        }

        // Check (c): Sec-Fetch-Site must not be 'cross-site'
        if (context.Request.Headers.TryGetValue("Sec-Fetch-Site", out var fetchSite))
        {
            if (fetchSite.ToString() == "cross-site")
                return false;
        }

        // Check (b): Origin or Referer must match allowed list or request's own origin
        var requestOrigin = GetRequestOrigin(context.Request);
        var origin = context.Request.Headers["Origin"].ToString();

        if (!string.IsNullOrEmpty(origin))
        {
            // Origin header present: must be in allowlist or match request origin
            if (!allowedOrigins.Contains(origin) && origin != requestOrigin)
                return false;
        }
        else if (context.Request.Headers.TryGetValue("Referer", out var referer))
        {
            // Referer fallback: extract origin and check
            if (Uri.TryCreate(referer.ToString(), UriKind.Absolute, out var refererUri))
            {
                var refererOrigin = $"{refererUri.Scheme}://{refererUri.Host}";
                if (refererUri.Port != -1)
                    refererOrigin += $":{refererUri.Port}";

                if (!allowedOrigins.Contains(refererOrigin) && refererOrigin != requestOrigin)
                    return false;
            }
        }

        return true;
    }

    /// <summary>Get the request's own origin (scheme://host[:port]).</summary>
    private static string GetRequestOrigin(HttpRequest request)
    {
        var scheme = request.Scheme;
        var host = request.Host.Host;
        var port = request.Host.Port;

        var origin = $"{scheme}://{host}";

        // Include port only if non-default for the scheme
        if (port.HasValue && !IsDefaultPort(scheme, port.Value))
            origin += $":{port}";

        return origin;
    }

    /// <summary>Check if a port is the default for its scheme.</summary>
    private static bool IsDefaultPort(string scheme, int port)
    {
        return (scheme == "http" && port == 80) || (scheme == "https" && port == 443);
    }
}

/// <summary>Response hardening helpers.</summary>
public static class ResponseHardeningExtensions
{
    /// <summary>Set Cache-Control and Pragma headers to prevent caching auth responses.</summary>
    public static void SetNoStore(this HttpResponse response)
    {
        response.Headers["Cache-Control"] = "no-store";
        response.Headers["Pragma"] = "no-cache";
    }

    /// <summary>Set Referrer-Policy header (prevents referrer leakage on token links).</summary>
    public static void SetNoReferrerPolicy(this HttpResponse response)
    {
        response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
