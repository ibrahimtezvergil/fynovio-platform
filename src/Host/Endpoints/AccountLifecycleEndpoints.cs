using Access.Application;
using Access.Application.Authentication;
using Contracts;
using Host.Authentication;

namespace Host.Endpoints;

/// <summary>Invitation, password and registration endpoints. Same conventions as <see cref="AuthEndpoints"/>:
/// `Cache-Control: no-store`, ProblemDetails with a short `type`, CSRF guard on everything that changes
/// state, per-client rate limits. Anything that takes a token answers every bad token identically.</summary>
public static class AccountLifecycleEndpoints
{
    private const int MaxEmailLength = 255;
    private const int MaxDisplayNameLength = 200;
    private const int MaxPasswordLength = 1024;
    private const int MaxTokenLength = 512;

    public static void MapAccountLifecycleEndpoints(this WebApplication app, bool selfRegistrationEnabled)
    {
        var group = app.MapGroup("/auth");
        group.AddEndpointFilter(async (invocation, next) =>
        {
            invocation.HttpContext.Response.SetNoStore();
            return await next(invocation);
        });

        // Read-only preview of an invitation; changes nothing and reads no cookie, so it needs no CSRF guard.
        group.MapPost("/invitations/validate", ValidateInvitationAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-token");

        group.MapPost("/invitations/accept", AcceptInvitationAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-token")
            .RequireCsrfProtection();

        group.MapPost("/password/forgot", ForgotPasswordAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-forgot")
            .RequireCsrfProtection();

        group.MapPost("/password/reset", ResetPasswordAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-token")
            .RequireCsrfProtection();

        group.MapPost("/password/change", ChangePasswordAsync)
            .RequireAuthorization()
            .RequireRateLimiting("auth-password")
            .RequireCsrfProtection();

        // Not mapped at all while registration is off: the route simply does not exist (404).
        if (selfRegistrationEnabled)
        {
            group.MapPost("/register", RegisterAsync)
                .AllowAnonymous()
                .RequireRateLimiting("auth-token")
                .RequireCsrfProtection();
        }

        app.MapPost("/tenants/{tenantId:long}/invitations", CreateInvitationAsync)
            .RequireAuthorization()
            .RequireRateLimiting("auth-password")
            .RequireCsrfProtection();
    }

    private static async Task<IResult> ValidateInvitationAsync(
        HttpContext context,
        ValidateInvitationHandler handler,
        CancellationToken cancellationToken)
    {
        var body = await AuthEndpoints.TryReadJsonAsync<TokenRequest>(context, cancellationToken);
        if (!IsPlausibleToken(body?.Token))
            return AuthProblems.InvalidOrExpiredToken();

        var result = await handler.HandleAsync(body!.Token, cancellationToken);
        if (result.Status != ValidateInvitationStatus.Valid)
            return AuthProblems.InvalidOrExpiredToken();

        return Results.Ok(new
        {
            valid = true,
            email = result.MaskedEmail,
            tenantId = result.TenantId,
            accountHasCredential = result.AccountHasCredential,
            expiresAt = result.ExpiresAt,
            displayName = result.DisplayName
        });
    }

    private static async Task<IResult> AcceptInvitationAsync(
        HttpContext context,
        AcceptInvitationHandler handler,
        AccessTokenIssuer issuer,
        RefreshCookieWriter cookieWriter,
        ClientFingerprint fingerprint,
        AuthenticationHostOptions options,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var body = await AuthEndpoints.TryReadJsonAsync<AcceptInvitationRequest>(context, cancellationToken);
        var errors = new Dictionary<string, string[]>();
        RequireToken(body?.Token, errors);
        RequirePassword(body?.Password, "password", errors);
        if (body?.DisplayName is { Length: > MaxDisplayNameLength })
            errors["displayName"] = ["Name is too long."];
        if (errors.Count > 0)
            return AuthProblems.Validation(errors);

        var result = await handler.HandleAsync(
            new AcceptInvitationCommand(
                body!.Token!,
                body.Password!,
                body.DisplayName,
                AuthEndpoints.CorrelationId(context),
                fingerprint.HashIp(context),
                fingerprint.HashUserAgent(context)),
            cancellationToken);

        return result.Status switch
        {
            AcceptInvitationStatus.Accepted when result.Session is { } session =>
                AuthEndpoints.StartSession(context, session, issuer, cookieWriter, options, timeProvider),
            AcceptInvitationStatus.InvalidCredentials => AuthProblems.InvalidCredentials(),
            AcceptInvitationStatus.PolicyViolation => AuthProblems.PasswordPolicyViolation(result.PolicyViolations),
            _ => AuthProblems.InvalidOrExpiredToken()
        };
    }

    private static async Task<IResult> ForgotPasswordAsync(
        HttpContext context,
        RequestPasswordResetHandler handler,
        IdentifierRateLimiter identifierLimiter,
        ClientFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        var body = await AuthEndpoints.TryReadJsonAsync<ForgotPasswordRequest>(context, cancellationToken);
        if (string.IsNullOrWhiteSpace(body?.Email))
            return AuthProblems.Validation(new Dictionary<string, string[]> { ["email"] = ["Email is required."] });
        if (body.Email.Length > MaxEmailLength)
            return AuthProblems.Validation(new Dictionary<string, string[]> { ["email"] = ["Email is too long."] });

        // Over the per-address budget the answer stays the neutral 202 — only nothing is sent — so the
        // limiter cannot be used to tell which addresses have accounts, nor to flood a mailbox.
        if (identifierLimiter.TryAcquire("forgot", EmailNormalizer.Normalize(body.Email)))
        {
            await handler.HandleAsync(
                new RequestPasswordResetCommand(body.Email, AuthEndpoints.CorrelationId(context), fingerprint.HashIp(context)),
                cancellationToken);
        }

        return Results.Accepted(value: new { status = "accepted" });
    }

    private static async Task<IResult> ResetPasswordAsync(
        HttpContext context,
        ResetPasswordHandler handler,
        RefreshCookieWriter cookieWriter,
        ClientFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        var body = await AuthEndpoints.TryReadJsonAsync<ResetPasswordRequest>(context, cancellationToken);
        var errors = new Dictionary<string, string[]>();
        RequireToken(body?.Token, errors);
        RequirePassword(body?.NewPassword, "newPassword", errors);
        if (errors.Count > 0)
            return AuthProblems.Validation(errors);

        var result = await handler.HandleAsync(
            new ResetPasswordCommand(body!.Token!, body.NewPassword!, AuthEndpoints.CorrelationId(context), fingerprint.HashIp(context)),
            cancellationToken);

        switch (result.Status)
        {
            case ResetPasswordStatus.Completed:
                cookieWriter.ClearToken(context.Response); // every session of the account was just revoked, this browser's included
                return Results.NoContent();
            case ResetPasswordStatus.PolicyViolation:
                return AuthProblems.PasswordPolicyViolation(result.PolicyViolations);
            default:
                return AuthProblems.InvalidOrExpiredToken();
        }
    }

    private static async Task<IResult> ChangePasswordAsync(
        HttpContext context,
        ChangePasswordHandler handler,
        PrincipalResolver principalResolver,
        ClientFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        // Who is changing what comes from the validated token, never from the body.
        var actor = context.GetActorContext();
        if (!Guid.TryParse(context.User.FindFirst("sid")?.Value, out var sessionId))
            return AuthProblems.SessionInvalid();

        var accountId = await principalResolver.ResolveAccountIdAsync(actor.Principal, cancellationToken);
        if (accountId is null)
            return AuthProblems.SessionInvalid();

        var body = await AuthEndpoints.TryReadJsonAsync<ChangePasswordRequest>(context, cancellationToken);
        var errors = new Dictionary<string, string[]>();
        RequirePassword(body?.CurrentPassword, "currentPassword", errors);
        RequirePassword(body?.NewPassword, "newPassword", errors);
        if (errors.Count > 0)
            return AuthProblems.Validation(errors);

        var result = await handler.HandleAsync(
            new ChangePasswordCommand(
                accountId.Value,
                sessionId,
                body!.CurrentPassword!,
                body.NewPassword!,
                AuthEndpoints.CorrelationId(context),
                fingerprint.HashIp(context)),
            cancellationToken);

        return result.Status switch
        {
            ChangePasswordStatus.Completed => Results.NoContent(),
            ChangePasswordStatus.InvalidCredentials => AuthProblems.InvalidCurrentPassword(),
            ChangePasswordStatus.PolicyViolation => AuthProblems.PasswordPolicyViolation(result.PolicyViolations),
            _ => AuthProblems.Conflict()
        };
    }

    private static async Task<IResult> RegisterAsync(
        HttpContext context,
        RegisterAccountHandler handler,
        ClientFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        var body = await AuthEndpoints.TryReadJsonAsync<RegisterRequest>(context, cancellationToken);
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(body?.Email))
            errors["email"] = ["Email is required."];
        else if (body.Email.Length > MaxEmailLength)
            errors["email"] = ["Email is too long."];
        if (string.IsNullOrWhiteSpace(body?.DisplayName))
            errors["displayName"] = ["Name is required."];
        else if (body.DisplayName.Length > MaxDisplayNameLength)
            errors["displayName"] = ["Name is too long."];
        RequirePassword(body?.Password, "password", errors);
        if (errors.Count > 0)
            return AuthProblems.Validation(errors);

        var result = await handler.HandleAsync(
            new RegisterAccountCommand(
                body!.Email!,
                body.DisplayName!,
                body.Password!,
                body.Locale,
                AuthEndpoints.CorrelationId(context),
                fingerprint.HashIp(context)),
            cancellationToken);

        return result.Status switch
        {
            RegisterAccountStatus.Accepted => Results.Accepted(value: new { status = "accepted" }),
            RegisterAccountStatus.PolicyViolation => AuthProblems.PasswordPolicyViolation(result.PolicyViolations),
            RegisterAccountStatus.InvalidEmail => AuthProblems.Validation(new Dictionary<string, string[]> { ["email"] = ["Email is not valid."] }),
            _ => AuthProblems.Validation(new Dictionary<string, string[]> { ["displayName"] = ["Name is not valid."] })
        };
    }

    private static async Task<IResult> CreateInvitationAsync(
        long tenantId,
        HttpContext context,
        CreateInvitationHandler handler,
        ClientFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        // The tenant in the URL is only a consistency check; the actor's tenant comes from the token.
        var actor = context.GetActorContext();
        if (tenantId != actor.TenantId.Value)
            return AuthProblems.TenantNotPermitted();

        var body = await AuthEndpoints.TryReadJsonAsync<CreateInvitationRequest>(context, cancellationToken);
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(body?.Email))
            errors["email"] = ["Email is required."];
        else if (body.Email.Length > MaxEmailLength)
            errors["email"] = ["Email is too long."];
        if (body?.DisplayName is { Length: > MaxDisplayNameLength })
            errors["displayName"] = ["Name is too long."];
        if (errors.Count > 0)
            return AuthProblems.Validation(errors);

        var result = await handler.HandleAsync(
            new CreateInvitationCommand(actor, body!.Email!, body.DisplayName, body.Locale, fingerprint.HashIp(context)),
            cancellationToken);

        return result.Status switch
        {
            CreateInvitationStatus.Accepted => Results.Accepted(value: new { status = "accepted" }),
            CreateInvitationStatus.Forbidden => AuthProblems.Forbidden(),
            _ => AuthProblems.Validation(new Dictionary<string, string[]> { ["email"] = ["Email is not valid."] })
        };
    }

    private static bool IsPlausibleToken(string? token) => !string.IsNullOrWhiteSpace(token) && token.Length <= MaxTokenLength;

    private static void RequireToken(string? token, Dictionary<string, string[]> errors)
    {
        if (!IsPlausibleToken(token))
            errors["token"] = ["A token is required."];
    }

    private static void RequirePassword(string? password, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrEmpty(password))
            errors[field] = ["Password is required."];
        else if (password.Length > MaxPasswordLength)
            errors[field] = ["Password is too long."];
    }

    private sealed record TokenRequest(string? Token);

    private sealed record AcceptInvitationRequest(string? Token, string? Password, string? DisplayName);

    private sealed record ForgotPasswordRequest(string? Email);

    private sealed record ResetPasswordRequest(string? Token, string? NewPassword);

    private sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

    private sealed record RegisterRequest(string? Email, string? DisplayName, string? Password, string? Locale);

    private sealed record CreateInvitationRequest(string? Email, string? DisplayName, string? Locale);
}
