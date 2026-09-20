using Access.Application;
using Access.Application.Authentication;
using Contracts;
using Host.Authentication;

namespace Host.Endpoints;

/// <summary>Authentication endpoints: login, refresh, logout, tenant selection, config, and me.
/// All responses include Cache-Control: no-store. Cookie-authenticated endpoints require CSRF validation.</summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth").WithName("Auth");

        group.MapGet("/config", GetConfig)
            .AllowAnonymous()
            .RequireRateLimiting("auth-public");

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-login");

        group.MapPost("/refresh", RefreshAsync)
            .RequireRateLimiting("auth-refresh");

        group.MapPost("/logout", LogoutAsync)
            .RequireRateLimiting("auth-refresh");

        group.MapPost("/tenants/select", SelectTenantAsync)
            .RequireRateLimiting("auth-refresh");

        group.MapGet("/me", GetMeAsync)
            .RequireAuthorization();
    }

    private static IResult GetConfig(AuthenticationHostOptions options)
    {
        var response = new
        {
            selfRegistrationEnabled = false,
            passwordPolicy = new
            {
                minLength = options.Password.MinLength,
                maxLength = options.Password.MaxLength
            }
        };
        return Results.Ok(response);
    }

    private static async Task<IResult> LoginAsync(
        HttpContext context,
        AuthenticateHandler handler,
        AccessTokenIssuer issuer,
        RefreshCookieWriter cookieWriter,
        IdentifierRateLimiter rateLimiter,
        AuthenticationHostOptions options,
        CancellationToken cancellationToken)
    {
        context.Response.SetNoStore();

        if (!CsrfOriginGuard.ValidateRequest(context, options.AllowedOrigins))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        // Get request body
        var body = await context.Request.ReadFromJsonAsync<LoginRequest>(cancellationToken);
        if (body?.Email == null || body.Password == null)
            return Results.BadRequest(new { type = "validation_error", errors = new { } });

        var errors = ValidateLoginRequest(body);
        if (errors.Count > 0)
            return Results.BadRequest(new { type = "validation_error", errors });

        var normalizedEmail = EmailNormalizer.Normalize(body.Email);
        if (!rateLimiter.TryAcquire("login", normalizedEmail))
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);

        var command = new AuthenticateCommand(body.Email, body.Password);
        var result = await handler.HandleAsync(command, cancellationToken);

        if (result.Status == AuthenticationStatus.InvalidCredentials)
            return Results.Unauthorized();

        if (result.RefreshCookie is not null && result.SessionId.HasValue)
        {
            var sessionAbsoluteExpiry = DateTimeOffset.UtcNow.AddDays(options.Session.RefreshAbsoluteDays);
            cookieWriter.WriteToken(context.Response, result.RefreshCookie, sessionAbsoluteExpiry);
        }

        var memberships = result.MembershipTenantIds?
            .Select(t => new { tenantId = t })
            .Cast<object>()
            .ToList() ?? new List<object>();

        var responseBody = new
        {
            status = result.Status.ToString().ToLowerInvariant(),
            accessToken = (string?)null,
            expiresIn = (int?)null,
            account = new { id = result.AccountId, email = body.Email, displayName = result.DisplayName, locale = "en-US" },
            activeTenant = result.SelectedTenantId.HasValue ? (object)new { tenantId = result.SelectedTenantId } : null,
            memberships
        };

        if (result.Status == AuthenticationStatus.Authenticated && result.SelectedTenantId.HasValue && result.SessionId.HasValue && result.Principal.HasValue)
        {
            var (token, expiresInSeconds) = issuer.IssueToken(
                result.Principal.Value.Subject,
                result.SelectedTenantId.Value,
                result.SessionId.Value);

            return Results.Ok(new
            {
                status = "authenticated",
                accessToken = token,
                expiresIn = expiresInSeconds,
                account = new { id = result.AccountId, email = body.Email, displayName = result.DisplayName, locale = "en-US" },
                activeTenant = (object)new { tenantId = result.SelectedTenantId },
                memberships
            });
        }

        return Results.Ok(responseBody);
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext context,
        RefreshSessionHandler handler,
        AccessTokenIssuer issuer,
        RefreshCookieWriter cookieWriter,
        AuthenticationHostOptions options,
        CancellationToken cancellationToken)
    {
        context.Response.SetNoStore();

        if (!CsrfOriginGuard.ValidateRequest(context, options.AllowedOrigins))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var cookieValue = cookieWriter.ReadToken(context.Request);
        if (string.IsNullOrEmpty(cookieValue))
            return Results.Unauthorized();

        var command = new RefreshSessionCommand(cookieValue);
        var result = await handler.HandleAsync(command, cancellationToken);

        if (result.Status == RefreshResult.SessionInvalid)
        {
            cookieWriter.ClearToken(context.Response);
            return Results.Unauthorized();
        }

        if (result.Status == RefreshResult.RefreshConflict)
            return Results.StatusCode(StatusCodes.Status409Conflict);

        if (result.RefreshCookie is not null && result.SessionId.HasValue)
        {
            var sessionAbsoluteExpiry = DateTimeOffset.UtcNow.AddDays(options.Session.RefreshAbsoluteDays);
            cookieWriter.WriteToken(context.Response, result.RefreshCookie, sessionAbsoluteExpiry);
        }

        var (token, expiresInSeconds) = issuer.IssueToken(
            result.Principal!.Value.Subject,
            result.SelectedTenantId!.Value,
            result.SessionId!.Value);

        var memberships = result.MembershipTenantIds?
            .Select(t => new { tenantId = t })
            .Cast<object>()
            .ToList() ?? new List<object>();

        return Results.Ok(new
        {
            status = "authenticated",
            accessToken = token,
            expiresIn = expiresInSeconds,
            account = new { id = result.AccountId, email = "unknown", displayName = result.DisplayName, locale = "en-US" },
            activeTenant = (object)new { tenantId = result.SelectedTenantId },
            memberships
        });
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        LogoutHandler handler,
        RefreshCookieWriter cookieWriter,
        AuthenticationHostOptions options,
        CancellationToken cancellationToken)
    {
        context.Response.SetNoStore();

        if (!CsrfOriginGuard.ValidateRequest(context, options.AllowedOrigins))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var cookieValue = cookieWriter.ReadToken(context.Request);
        if (!string.IsNullOrEmpty(cookieValue))
        {
            await handler.HandleAsync(cookieValue, null, cancellationToken);
        }

        cookieWriter.ClearToken(context.Response);
        return Results.NoContent();
    }

    private static async Task<IResult> SelectTenantAsync(
        HttpContext context,
        SelectTenantHandler handler,
        AccessTokenIssuer issuer,
        RefreshCookieWriter cookieWriter,
        AuthenticationHostOptions options,
        CancellationToken cancellationToken)
    {
        context.Response.SetNoStore();

        if (!CsrfOriginGuard.ValidateRequest(context, options.AllowedOrigins))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var cookieValue = cookieWriter.ReadToken(context.Request);
        if (string.IsNullOrEmpty(cookieValue))
            return Results.Unauthorized();

        var body = await context.Request.ReadFromJsonAsync<SelectTenantRequest>(cancellationToken);
        if (body?.TenantId == null)
            return Results.BadRequest();

        var command = new SelectTenantCommand(cookieValue, new TenantId(body.TenantId.Value));
        var result = await handler.HandleAsync(command, cancellationToken);

        if (result.Status == TenantSelectionStatus.SessionInvalid)
        {
            cookieWriter.ClearToken(context.Response);
            return Results.Unauthorized();
        }

        if (result.Status == TenantSelectionStatus.TenantNotPermitted)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var (token, expiresInSeconds) = issuer.IssueToken(
            "unknown",
            result.SelectedTenantId!.Value,
            result.SessionId!.Value);

        return Results.Ok(new
        {
            accessToken = token,
            expiresIn = expiresInSeconds,
            activeTenant = new { tenantId = result.SelectedTenantId }
        });
    }

    private static async Task<IResult> GetMeAsync(
        HttpContext context,
        GetSessionOverviewHandler handler,
        CancellationToken cancellationToken)
    {
        context.Response.SetNoStore();

        var sessionIdClaim = context.User.FindFirst("sid")?.Value;
        if (string.IsNullOrEmpty(sessionIdClaim) || !Guid.TryParse(sessionIdClaim, out var sessionId))
            return Results.Unauthorized();

        var overview = await handler.HandleAsync(sessionId, cancellationToken);
        if (overview is null)
            return Results.Unauthorized();

        var memberships = overview.MembershipTenantIds
            .Select(t => new { tenantId = t })
            .Cast<object>()
            .ToList();

        return Results.Ok(new
        {
            account = new { id = overview.AccountId, email = "unknown", displayName = overview.DisplayName, locale = "en-US" },
            activeTenant = overview.SelectedTenantId.HasValue ? (object)new { tenantId = overview.SelectedTenantId } : null,
            memberships
        });
    }

    private static Dictionary<string, string[]> ValidateLoginRequest(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email))
            errors["email"] = new[] { "Email is required" };
        else if (request.Email.Length > 255)
            errors["email"] = new[] { "Email is too long" };

        if (string.IsNullOrWhiteSpace(request.Password))
            errors["password"] = new[] { "Password is required" };
        else if (request.Password.Length < 8 || request.Password.Length > 1024)
            errors["password"] = new[] { "Password length is invalid" };

        return errors;
    }

    private record LoginRequest(string? Email, string? Password);
    private record SelectTenantRequest(long? TenantId);
}
