using Host.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Host.Tests.Authentication;

public sealed class CsrfOriginGuardTests
{
    private static readonly string[] Allowed = ["http://localhost:5173"];

    private static DefaultHttpContext Request(
        string? requestedWith = "fynovio",
        string? origin = null,
        string? referer = null,
        string? fetchSite = null,
        string scheme = "https",
        string host = "app.fynovio.test")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        if (requestedWith is not null) context.Request.Headers["X-Requested-With"] = requestedWith;
        if (origin is not null) context.Request.Headers["Origin"] = origin;
        if (referer is not null) context.Request.Headers["Referer"] = referer;
        if (fetchSite is not null) context.Request.Headers["Sec-Fetch-Site"] = fetchSite;
        return context;
    }

    [Fact]
    public void Missing_custom_header_is_rejected() =>
        Assert.False(CsrfOriginGuard.ValidateRequest(Request(requestedWith: null), Allowed));

    [Fact]
    public void Wrong_custom_header_value_is_rejected() =>
        Assert.False(CsrfOriginGuard.ValidateRequest(Request(requestedWith: "XMLHttpRequest"), Allowed));

    [Fact]
    public void Foreign_origin_is_rejected() =>
        Assert.False(CsrfOriginGuard.ValidateRequest(Request(origin: "https://evil.example"), Allowed));

    [Fact]
    public void Opaque_null_origin_is_rejected() =>
        Assert.False(CsrfOriginGuard.ValidateRequest(Request(origin: "null"), Allowed));

    [Fact]
    public void Allow_listed_origin_passes() =>
        Assert.True(CsrfOriginGuard.ValidateRequest(Request(origin: "http://localhost:5173"), Allowed));

    [Fact]
    public void Same_origin_passes_without_being_allow_listed() =>
        Assert.True(CsrfOriginGuard.ValidateRequest(Request(origin: "https://app.fynovio.test"), []));

    [Fact]
    public void No_origin_and_no_referer_passes_when_the_custom_header_is_present() =>
        Assert.True(CsrfOriginGuard.ValidateRequest(Request(), Allowed));

    [Fact]
    public void Cross_site_fetch_metadata_is_rejected() =>
        Assert.False(CsrfOriginGuard.ValidateRequest(Request(fetchSite: "cross-site"), Allowed));

    [Theory]
    [InlineData("same-origin")]
    [InlineData("same-site")]
    [InlineData("none")]
    public void Same_site_fetch_metadata_passes(string fetchSite) =>
        Assert.True(CsrfOriginGuard.ValidateRequest(Request(fetchSite: fetchSite), Allowed));

    [Fact]
    public void Foreign_referer_is_rejected_when_there_is_no_origin() =>
        Assert.False(CsrfOriginGuard.ValidateRequest(Request(referer: "https://evil.example/page"), Allowed));

    [Fact]
    public void Allow_listed_referer_passes_when_there_is_no_origin() =>
        Assert.True(CsrfOriginGuard.ValidateRequest(Request(referer: "http://localhost:5173/login"), Allowed));

    [Fact]
    public void Unparseable_referer_is_rejected() =>
        Assert.False(CsrfOriginGuard.ValidateRequest(Request(referer: "not a url"), Allowed));
}

public sealed class RefreshCookieWriterTests
{
    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Host";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static RefreshCookieWriter Writer(SessionHostOptions options, string environment) =>
        new(options, new FakeEnvironment(environment), NullLogger<RefreshCookieWriter>.Instance);

    private static string SetCookie(DefaultHttpContext context) => context.Response.Headers.SetCookie.ToString();

    private static readonly DateTimeOffset Expiry = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Cookie_is_httponly_secure_samesite_strict_with_configured_name_path_and_expiry_and_no_domain()
    {
        var context = new DefaultHttpContext();
        Writer(new SessionHostOptions { CookieName = "fynovio_rt", CookiePath = "/api/auth" }, "Production")
            .WriteToken(context.Response, "abc.def", Expiry);

        var cookie = SetCookie(context);

        Assert.StartsWith("fynovio_rt=abc.def", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Insecure_cookie_is_allowed_only_in_development_and_only_when_opted_in()
    {
        var options = new SessionHostOptions { AllowInsecureCookieInDevelopment = true };

        var development = new DefaultHttpContext();
        Writer(options, "Development").WriteToken(development.Response, "v", Expiry);
        Assert.DoesNotContain("secure", SetCookie(development), StringComparison.OrdinalIgnoreCase);

        // The opt-in flag alone must never weaken a non-development environment.
        var production = new DefaultHttpContext();
        Writer(options, "Production").WriteToken(production.Response, "v", Expiry);
        Assert.Contains("secure", SetCookie(production), StringComparison.OrdinalIgnoreCase);

        // Development without the opt-in stays Secure.
        var developmentWithoutOptIn = new DefaultHttpContext();
        Writer(new SessionHostOptions(), "Development").WriteToken(developmentWithoutOptIn.Response, "v", Expiry);
        Assert.Contains("secure", SetCookie(developmentWithoutOptIn), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Clearing_expires_the_cookie_with_the_same_path_and_flags()
    {
        var context = new DefaultHttpContext();
        Writer(new SessionHostOptions { CookiePath = "/api/auth" }, "Production").ClearToken(context.Response);

        var cookie = SetCookie(context);

        Assert.StartsWith("fynovio_rt=;", cookie);
        Assert.Contains("path=/api/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=Thu, 01 Jan 1970", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Token_is_read_back_from_the_request_cookie_and_absent_is_null()
    {
        var writer = Writer(new SessionHostOptions(), "Production");

        var withCookie = new DefaultHttpContext();
        withCookie.Request.Headers.Cookie = "fynovio_rt=abc.def; other=1";
        Assert.Equal("abc.def", writer.ReadToken(withCookie.Request));

        Assert.Null(writer.ReadToken(new DefaultHttpContext().Request));
    }

    [Fact]
    public void Cookie_value_never_reaches_the_logs()
    {
        var logger = new CapturingLogger<RefreshCookieWriter>();
        var writer = new RefreshCookieWriter(new SessionHostOptions(), new FakeEnvironment("Production"), logger);
        var context = new DefaultHttpContext();

        writer.WriteToken(context.Response, "super-secret-token-value", Expiry);
        writer.ClearToken(context.Response);

        Assert.NotEmpty(logger.Messages);
        Assert.DoesNotContain(logger.Messages, m => m.Contains("super-secret-token-value"));
    }

    private sealed class CapturingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
