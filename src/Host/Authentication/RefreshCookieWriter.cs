using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Host.Authentication;

/// <summary>Writes, reads, and clears refresh token cookies with secure attributes.
/// Cookie attributes: HttpOnly (always), Secure (except when AllowInsecureCookieInDevelopment
/// and IsDevelopment), SameSite=Strict, Path (from config), no Domain.
///
/// Never logs cookie values (to prevent accidental token exposure in logs).</summary>
public sealed class RefreshCookieWriter
{
    private readonly SessionHostOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<RefreshCookieWriter> _logger;

    public RefreshCookieWriter(
        SessionHostOptions options,
        IHostEnvironment environment,
        ILogger<RefreshCookieWriter> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Write a refresh token to the response cookie.</summary>
    public void WriteToken(HttpResponse response, string tokenValue, DateTimeOffset absoluteExpiry)
    {
        var cookieOptions = BuildCookieOptions(absoluteExpiry);
        response.Cookies.Append(_options.CookieName, tokenValue, cookieOptions);

        _logger.LogInformation(
            "Refresh token cookie written (name: {CookieName}, path: {CookiePath}, secure: {Secure}, httpOnly: true)",
            _options.CookieName,
            _options.CookiePath,
            cookieOptions.Secure);
    }

    /// <summary>Clear the refresh token cookie (called on logout or session revocation).</summary>
    public void ClearToken(HttpResponse response)
    {
        var cookieOptions = BuildCookieOptions(DateTimeOffset.UtcNow.AddDays(-1)); // Expired
        response.Cookies.Delete(_options.CookieName, cookieOptions);

        _logger.LogInformation(
            "Refresh token cookie cleared (name: {CookieName}, path: {CookiePath})",
            _options.CookieName,
            _options.CookiePath);
    }

    /// <summary>Read a refresh token from request cookies.</summary>
    public string? ReadToken(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(_options.CookieName, out var value))
            return null;

        return value;
    }

    /// <summary>Build cookie options with secure attributes.</summary>
    private CookieOptions BuildCookieOptions(DateTimeOffset expires)
    {
        var secure = !(_options.AllowInsecureCookieInDevelopment && _environment.IsDevelopment());

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Path = _options.CookiePath,
            Expires = expires,
            // Domain is intentionally not set; let the browser infer it from request
        };
    }
}
