using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Host.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Host.Tests.Authentication;

/// <summary>Request/response helpers shared by the invitation, password and e-mail HTTP tests.</summary>
internal static class LifecycleTestSupport
{
    public const string AllowedOrigin = "http://localhost:5173";
    public const string CookieName = "fynovio_rt";
    public const string NewPassword = "Brand-New-Passw0rd-42";
    public const string InviteAction = "identity.membership.invite";

    public static HttpRequestMessage Post(
        string path,
        object? body = null,
        string? bearer = null,
        string? cookie = null,
        bool csrfHeader = true,
        string? origin = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        if (csrfHeader)
            request.Headers.Add("X-Requested-With", "fynovio");
        if (origin is not null)
            request.Headers.Add("Origin", origin);
        if (cookie is not null)
            request.Headers.Add("Cookie", cookie);
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return request;
    }

    public static HttpRequestMessage Get(string path, string? bearer = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return request;
    }

    public static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    /// <summary>ProblemDetails carry a per-request `traceId`; everything else must be identical.</summary>
    public static async Task<string> StableBody(HttpResponseMessage response)
    {
        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        node.Remove("traceId");
        return node.ToJsonString();
    }

    public static async Task<string> ProblemType(HttpResponseMessage response) =>
        (await Json(response)).GetProperty("type").GetString()!;

    public static string? SetCookieHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(CookieName + "=", StringComparison.Ordinal))
            : null;

    /// <summary>`fynovio_rt=&lt;value&gt;` as a request `Cookie` header.</summary>
    public static string CookieHeader(HttpResponseMessage response) =>
        (SetCookieHeader(response) ?? throw new InvalidOperationException("No refresh cookie was set.")).Split(';')[0];

    public static Task<HttpResponseMessage> Login(HttpClient client, string email, string password) =>
        client.SendAsync(Post("/auth/login", new { email, password }));

    public static Task<HttpResponseMessage> Refresh(HttpClient client, string cookie) =>
        client.SendAsync(Post("/auth/refresh", cookie: cookie));

    public static async Task<string> AccessTokenAsync(HttpClient client, string email, string password)
    {
        var response = await Login(client, email, password);
        response.EnsureSuccessStatusCode();
        return (await Json(response)).GetProperty("accessToken").GetString()!;
    }

    /// <summary>A member of `tenantId` who holds the invite action, signed in; the host must already be running
    /// (its start-up registers the action).</summary>
    public static async Task<(SeededUser User, string AccessToken)> InviterAsync(AuthApiFixture fixture, HttpClient client, long tenantId)
    {
        var user = await fixture.SeedUserAsync(tenantIds: tenantId);
        await fixture.GrantAsync(user.AccountId, tenantId, InviteAction);
        return (user, await AccessTokenAsync(client, user.Email, AuthApiFixture.Password));
    }

    public static Task<HttpResponseMessage> Invite(HttpClient client, long tenantId, string bearer, string email, string? displayName = null, string? locale = null) =>
        client.SendAsync(Post($"/tenants/{tenantId}/invitations", new { email, displayName, locale }, bearer));

    public sealed record MailboxEntry(string To, string TemplateId, string Subject, string Link, string Token);

    public static async Task<List<MailboxEntry>> MailboxAsync(HttpClient client, string? to = null)
    {
        var response = await client.GetAsync(to is null ? "/dev/mailbox" : $"/dev/mailbox?to={Uri.EscapeDataString(to)}");
        response.EnsureSuccessStatusCode();
        return (await Json(response)).EnumerateArray()
            .Select(e => new MailboxEntry(
                e.GetProperty("to").GetString()!,
                e.GetProperty("templateId").GetString()!,
                e.GetProperty("subject").GetString()!,
                e.GetProperty("link").GetString()!,
                e.GetProperty("token").GetString()!))
            .ToList();
    }

    /// <summary>The newest message of `template` for `to`.</summary>
    public static async Task<MailboxEntry> MailAsync(HttpClient client, string to, string template) =>
        (await MailboxAsync(client, to)).First(m => m.TemplateId == template);

    public static Task<HttpResponseMessage> Validate(HttpClient client, string? token) =>
        client.SendAsync(Post("/auth/invitations/validate", new { token }, csrfHeader: false));

    public static Task<HttpResponseMessage> Accept(HttpClient client, string? token, string? password, string? displayName = null) =>
        client.SendAsync(Post("/auth/invitations/accept", new { token, password, displayName }));

    public static Task<HttpResponseMessage> Forgot(HttpClient client, string email) =>
        client.SendAsync(Post("/auth/password/forgot", new { email }));

    public static Task<HttpResponseMessage> Reset(HttpClient client, string? token, string? newPassword) =>
        client.SendAsync(Post("/auth/password/reset", new { token, newPassword }));

    public static Task<HttpResponseMessage> Change(HttpClient client, string bearer, string? currentPassword, string? newPassword) =>
        client.SendAsync(Post("/auth/password/change", new { currentPassword, newPassword }, bearer));

    public static Task<AuthApiHost> StartWithClockAsync(
        AuthApiFixture fixture,
        AdvanceableTimeProvider clock,
        Dictionary<string, string?>? settings = null,
        Microsoft.Extensions.Logging.ILoggerProvider? logProvider = null) =>
        fixture.StartHostAsync(settings, services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }, logProvider);

    /// <summary>Settings that make the host behave like Production (no dev routes, no seed).</summary>
    public static Dictionary<string, string?> Production(Dictionary<string, string?>? extra = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["Authentication__Jwt__Issuer"] = "https://prod.example",
            ["Authentication__Jwt__Audience"] = "fynovio-platform",
            ["Authentication__Jwt__SigningKey"] = new string('k', 48),
            ["Authentication__PublicAppBaseUrl"] = "https://app.prod.example",
        };
        foreach (var (key, value) in extra ?? [])
            settings[key] = value;
        return settings;
    }
}
