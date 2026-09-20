using System.Text.Json;
using Access.Application;
using Access.Application.Authentication;
using Contracts;
using Host.Authentication;

namespace Host.Endpoints;

/// <summary>Authentication endpoints: config, login, refresh, logout, tenant selection and me (the account lifecycle — invitations, passwords, registration — is in <see cref="AccountLifecycleEndpoints"/>).
/// Every response is `Cache-Control: no-store`. Endpoints that take the anonymous credential or
/// the refresh cookie are CSRF-guarded (custom header + Origin allow-list + Fetch metadata).
/// Errors are ProblemDetails with a short `type` code (<see cref="AuthProblems"/>).</summary>
public static class AuthEndpoints
{
    private const int MaxEmailLength = 255;
    private const int MaxPasswordLength = 1024;

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth");
        group.AddEndpointFilter(async (invocation, next) =>
        {
            invocation.HttpContext.Response.SetNoStore();
            return await next(invocation);
        });

        group.MapGet("/config", GetConfig)
            .AllowAnonymous()
            .RequireRateLimiting("auth-public");

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-login")
            .RequireCsrfProtection();

        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-refresh")
            .RequireCsrfProtection();

        group.MapPost("/logout", LogoutAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-refresh")
            .RequireCsrfProtection();

        group.MapPost("/tenants/select", SelectTenantAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-refresh")
            .RequireCsrfProtection();

        group.MapGet("/me", GetMeAsync)
            .RequireAuthorization();
    }

    private static IResult GetConfig(AuthenticationHostOptions options) =>
        Results.Ok(new
        {
            selfRegistrationEnabled = options.SelfRegistration.Enabled,
            passwordPolicy = new
            {
                minLength = options.Password.MinLength,
                maxLength = options.Password.MaxLength
            }
        });

    private static async Task<IResult> LoginAsync(
        HttpContext context,
        AuthenticateHandler handler,
        AccessTokenIssuer issuer,
        RefreshCookieWriter cookieWriter,
        IdentifierRateLimiter identifierLimiter,
        ClientFingerprint fingerprint,
        AuthenticationHostOptions options,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var body = await TryReadJsonAsync<LoginRequest>(context, cancellationToken);
        var errors = ValidateLogin(body);
        if (errors.Count > 0)
            return AuthProblems.Validation(errors);

        var email = body!.Email!;
        if (!identifierLimiter.TryAcquire("login", EmailNormalizer.Normalize(email)))
        {
            context.Response.Headers.RetryAfter = "60";
            return AuthProblems.RateLimited();
        }

        var result = await handler.HandleAsync(
            new AuthenticateCommand(
                email,
                body.Password!,
                CorrelationId(context),
                fingerprint.HashIp(context),
                fingerprint.HashUserAgent(context)),
            cancellationToken);

        if (result.Status == AuthenticationStatus.InvalidCredentials || result.RefreshCookie is null)
            return AuthProblems.InvalidCredentials();

        return StartSession(context, result, issuer, cookieWriter, options, timeProvider);
    }

    /// <summary>Sets the refresh cookie and answers with the login-shaped body. Used by login and by
    /// invitation acceptance, which both end with a freshly created session.</summary>
    internal static IResult StartSession(
        HttpContext context,
        AuthenticateResult result,
        AccessTokenIssuer issuer,
        RefreshCookieWriter cookieWriter,
        AuthenticationHostOptions options,
        TimeProvider timeProvider)
    {
        if (result.RefreshCookie is null)
            return AuthProblems.SessionInvalid();

        cookieWriter.WriteToken(
            context.Response,
            result.RefreshCookie,
            result.SessionExpiresAt ?? timeProvider.GetUtcNow().AddDays(options.Session.RefreshAbsoluteDays));

        return SessionResponse(issuer, result.Account, result.MembershipTenantIds, result.SelectedTenantId, result.Principal, result.SessionId);
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext context,
        RefreshSessionHandler handler,
        AccessTokenIssuer issuer,
        RefreshCookieWriter cookieWriter,
        ClientFingerprint fingerprint,
        AuthenticationHostOptions options,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var cookieValue = cookieWriter.ReadToken(context.Request);
        if (string.IsNullOrEmpty(cookieValue))
            return AuthProblems.SessionInvalid();

        var result = await handler.HandleAsync(
            new RefreshSessionCommand(cookieValue, CorrelationId(context), fingerprint.HashIp(context)),
            cancellationToken);

        if (result.Status == RefreshResult.RefreshConflict)
            return AuthProblems.RefreshConflict();

        if (result.Status != RefreshResult.Success || result.RefreshCookie is null)
        {
            cookieWriter.ClearToken(context.Response);
            return AuthProblems.SessionInvalid();
        }

        cookieWriter.WriteToken(
            context.Response,
            result.RefreshCookie,
            result.SessionExpiresAt ?? timeProvider.GetUtcNow().AddDays(options.Session.RefreshAbsoluteDays));

        return SessionResponse(issuer, result.Account, result.MembershipTenantIds, result.SelectedTenantId, result.Principal, result.SessionId);
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        LogoutHandler handler,
        RefreshCookieWriter cookieWriter,
        ClientFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        var cookieValue = cookieWriter.ReadToken(context.Request);
        if (!string.IsNullOrEmpty(cookieValue))
            await handler.HandleAsync(cookieValue, CorrelationId(context), cancellationToken, fingerprint.HashIp(context));

        cookieWriter.ClearToken(context.Response);
        return Results.NoContent();
    }

    private static async Task<IResult> SelectTenantAsync(
        HttpContext context,
        SelectTenantHandler handler,
        AccessTokenIssuer issuer,
        RefreshCookieWriter cookieWriter,
        ClientFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        var cookieValue = cookieWriter.ReadToken(context.Request);
        if (string.IsNullOrEmpty(cookieValue))
            return AuthProblems.SessionInvalid();

        var body = await TryReadJsonAsync<SelectTenantRequest>(context, cancellationToken);
        if (body?.TenantId is null or <= 0)
            return AuthProblems.Validation(new Dictionary<string, string[]> { ["tenantId"] = ["A tenant id is required."] });

        var result = await handler.HandleAsync(
            new SelectTenantCommand(cookieValue, new TenantId(body.TenantId.Value), CorrelationId(context), fingerprint.HashIp(context)),
            cancellationToken);

        switch (result.Status)
        {
            case TenantSelectionStatus.TenantNotPermitted:
                return AuthProblems.TenantNotPermitted();

            case TenantSelectionStatus.Success when result.Principal is { } principal
                                                   && result.SelectedTenantId is { } tenantId
                                                   && result.SessionId is { } sessionId:
                var (token, expiresIn) = issuer.IssueToken(principal.Subject, tenantId, sessionId);
                return Results.Ok(new
                {
                    accessToken = token,
                    expiresIn,
                    activeTenant = new { tenantId }
                });

            default:
                cookieWriter.ClearToken(context.Response);
                return AuthProblems.SessionInvalid();
        }
    }

    private static async Task<IResult> GetMeAsync(
        HttpContext context,
        GetSessionOverviewHandler handler,
        GetCapabilitiesHandler capabilitiesHandler,
        CancellationToken cancellationToken)
    {
        var actor = context.GetActorContext();
        if (!Guid.TryParse(context.User.FindFirst("sid")?.Value, out var sessionId))
            return AuthProblems.SessionInvalid();

        var overview = await handler.HandleAsync(sessionId, cancellationToken);
        if (overview?.Account is null)
            return AuthProblems.SessionInvalid();

        var capabilities = await capabilitiesHandler.HandleAsync(actor, cancellationToken);

        return Results.Ok(new
        {
            account = AccountBody(overview.Account),
            // The tenant the *token* is scoped to (validated by ActorContextMiddleware), not the session's latest selection.
            activeTenant = new { tenantId = actor.TenantId.Value },
            memberships = overview.MembershipTenantIds.Select(id => new { tenantId = id }).ToList(),
            // UX hints from the PDP; the backend authorises the action itself again when it is attempted.
            capabilities = new { canInviteMembers = capabilities.CanInviteMembers }
        });
    }

    /// <summary>The shared body of login and refresh: `authenticated` (with an access token for
    /// the selected tenant), `tenant_selection_required` or `no_membership` (no token).</summary>
    private static IResult SessionResponse(
        AccessTokenIssuer issuer,
        AccountSummary? account,
        IReadOnlyList<long>? membershipTenantIds,
        long? selectedTenantId,
        PrincipalRef? principal,
        Guid? sessionId)
    {
        if (account is null)
            return AuthProblems.SessionInvalid();

        var memberships = (membershipTenantIds ?? []).Select(id => new { tenantId = id }).ToList();
        var body = new Dictionary<string, object?>
        {
            ["account"] = AccountBody(account),
            ["memberships"] = memberships
        };

        if (selectedTenantId is { } tenantId && principal is { } subjectOwner && sessionId is { } session)
        {
            var (token, expiresIn) = issuer.IssueToken(subjectOwner.Subject, tenantId, session);
            body["status"] = "authenticated";
            body["accessToken"] = token;
            body["expiresIn"] = expiresIn;
            body["activeTenant"] = new { tenantId };
        }
        else
        {
            body["status"] = memberships.Count == 0 ? "no_membership" : "tenant_selection_required";
            body["activeTenant"] = null;
        }

        return Results.Ok(body);
    }

    internal static object AccountBody(AccountSummary account) =>
        new { id = account.Id, email = account.Email, displayName = account.DisplayName, locale = account.Locale };

    private static Dictionary<string, string[]> ValidateLogin(LoginRequest? request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request is null || string.IsNullOrWhiteSpace(request.Email))
            errors["email"] = ["Email is required."];
        else if (request.Email.Length > MaxEmailLength)
            errors["email"] = ["Email is too long."];

        if (request is null || string.IsNullOrEmpty(request.Password))
            errors["password"] = ["Password is required."];
        else if (request.Password.Length > MaxPasswordLength)
            errors["password"] = ["Password is too long."];

        return errors;
    }

    internal static async Task<T?> TryReadJsonAsync<T>(HttpContext context, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return await context.Request.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or BadHttpRequestException)
        {
            return null;
        }
    }

    /// <summary>Reuses a valid `X-Correlation-Id` or generates one, and echoes it back.</summary>
    internal static string CorrelationId(HttpContext context)
    {
        var correlationId = Guid.TryParse(context.Request.Headers["X-Correlation-Id"].ToString(), out var supplied)
            ? supplied
            : Guid.NewGuid();
        context.Response.Headers["X-Correlation-Id"] = correlationId.ToString();
        return correlationId.ToString();
    }

    private sealed record LoginRequest(string? Email, string? Password);

    private sealed record SelectTenantRequest(long? TenantId);
}
