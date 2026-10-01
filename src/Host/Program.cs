using System.Text;
using System.Threading.RateLimiting;
using Access.Application;
using Access.Application.Authentication;
using Access.Persistence;
using Contracts;
using CRM.Application;
using CRM.Persistence;
using Collaboration.Persistence;
using Collaboration.Application;
using Host.Authentication;
using Host.Bootstrap;
using Host.Email;
using Host.Endpoints;
using Host.Modules;
using MasterData.Application;
using MasterData.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using TenantLifecycle.Application;
using TenantLifecycle.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CrmDbContext>(options => options
    .UseNpgsql(
        builder.Configuration.GetConnectionString("Crm") ?? CrmConnectionString.Resolve(),
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CrmDbContext.Schema))
    .UseSnakeCaseNamingConvention());

builder.Services.AddDbContext<CollaborationDbContext>(options => options
    .UseNpgsql(
        builder.Configuration.GetConnectionString("Collaboration") ?? CollaborationConnectionString.Resolve(),
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CollaborationDbContext.Schema))
    .UseSnakeCaseNamingConvention());

builder.Services.AddDbContext<TenantLifecycleDbContext>(options => options
    .UseNpgsql(
        builder.Configuration.GetConnectionString("TenantLifecycle") ?? TenantLifecycleConnectionString.Resolve(),
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", TenantLifecycleDbContext.Schema))
    .UseSnakeCaseNamingConvention());

builder.Services.AddDbContext<MasterDataDbContext>(options => options
    .UseNpgsql(
        builder.Configuration.GetConnectionString("MasterData") ?? MasterDataConnectionString.Resolve(),
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", MasterDataDbContext.Schema))
    .UseSnakeCaseNamingConvention());

builder.Services.AddDbContext<AccessDbContext>(options => options
    .UseNpgsql(
        builder.Configuration.GetConnectionString("Access") ?? AccessConnectionString.Resolve(),
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", AccessDbContext.AccessSchema))
    .UseSnakeCaseNamingConvention()
    .AddInterceptors(new RowVersionInterceptor()));

builder.Services.AddScoped<IPartyDirectory, PartyDirectory>();
builder.Services.AddScoped<ITenantDirectory, TenantDirectory>();
builder.Services.AddScoped<IPartySearch, PartyDirectory>();
builder.Services.AddScoped<CreatePartyHandler>();
builder.Services.AddScoped<IPartyRegistration, PartyRegistration>();
builder.Services.AddScoped<IPartyIdentityResolver, PartyIdentityResolver>();

builder.Services.AddScoped<PrincipalResolver>();
builder.Services.AddScoped<IActionCatalog, AccessActionCatalogService>();
builder.Services.AddScoped<IAuthorizer, AccessAuthorizer>();
builder.Services.AddScoped<IAccessScopeResolver, AccessScopeResolver>();
builder.Services.AddScoped<IAuthorizedPrincipalDirectory, AuthorizedPrincipalDirectory>();

// Load and validate JWT options (fail-fast at startup)
var jwtOptions = builder.Configuration.GetSection("Authentication:Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Authentication:Jwt configuration section is required.");

// Validate SigningKey is at least 32 bytes
var signingKeyBytes = Encoding.UTF8.GetByteCount(jwtOptions.SigningKey);
if (signingKeyBytes < 32)
    throw new InvalidOperationException($"Authentication:Jwt:SigningKey must be at least 32 bytes when UTF-8 encoded; got {signingKeyBytes} bytes");

builder.Services.AddSingleton(jwtOptions);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            NameClaimType = "sub",
            ClockSkew = TimeSpan.FromSeconds(30) // Explicit 30s clock skew
        };
    });
builder.Services.AddAuthorization();

// Register Authentication Host options
var authOptions = builder.Configuration.GetSection("Authentication").Get<AuthenticationHostOptions>()
    ?? throw new InvalidOperationException("Authentication configuration section is required.");

// Validate PublicAppBaseUrl if set
if (!string.IsNullOrEmpty(authOptions.PublicAppBaseUrl))
{
    if (!Uri.TryCreate(authOptions.PublicAppBaseUrl, UriKind.Absolute, out var uri) ||
        (uri.Scheme != "http" && uri.Scheme != "https"))
    {
        throw new InvalidOperationException(
            $"Authentication:PublicAppBaseUrl must be an absolute http(s) URL; got '{authOptions.PublicAppBaseUrl}'");
    }
}

// In non-Development, PublicAppBaseUrl is required (for email links)
if (!builder.Environment.IsDevelopment() && string.IsNullOrEmpty(authOptions.PublicAppBaseUrl))
    throw new InvalidOperationException("Authentication:PublicAppBaseUrl is required outside of Development environment");

// Self-registration: off by default. It never grants tenant access, but it does create identities for
// unverified e-mail addresses, so outside Development it needs an explicit acknowledgement.
if (authOptions.SelfRegistration.Enabled
    && !builder.Environment.IsDevelopment()
    && !authOptions.SelfRegistration.AcknowledgeUnverifiedEmail)
{
    throw new InvalidOperationException(
        "Authentication:SelfRegistration:Enabled requires Authentication:SelfRegistration:AcknowledgeUnverifiedEmail=true outside Development " +
        "(e-mail addresses are not verified before an account is created).");
}

if (authOptions.Tokens.InviteDays <= 0 || authOptions.Tokens.PasswordResetMinutes <= 0 || authOptions.Tokens.PasswordSetupHours <= 0)
    throw new InvalidOperationException("Authentication:Tokens lifetimes (InviteDays, PasswordResetMinutes, PasswordSetupHours) must be positive.");

builder.Services.AddSingleton(authOptions);
builder.Services.AddSingleton(authOptions.Tokens);
builder.Services.AddSingleton(authOptions.Session);
builder.Services.AddSingleton(authOptions.Password);
builder.Services.AddSingleton(authOptions.Lockout);

// The Access handlers take their own SessionOptions POCO (no cookie/host concerns);
// the platform issuer defaults to the JWT issuer so sub/iss stay one identity space.
builder.Services.AddSingleton(new Access.Application.Authentication.SessionOptions
{
    RefreshIdleDays = authOptions.Session.RefreshIdleDays,
    RefreshAbsoluteDays = authOptions.Session.RefreshAbsoluteDays,
    RefreshGraceSeconds = authOptions.Session.RefreshGraceSeconds,
    PlatformIssuer = authOptions.Session.PlatformIssuer ?? jwtOptions.Issuer
});

// Register authentication application handlers
builder.Services.AddScoped<PasswordService>();
builder.Services.AddSingleton<PasswordPolicy>();
builder.Services.AddScoped<AuthEventWriter>();
builder.Services.AddScoped<SessionValidator>();
builder.Services.AddScoped<AuthenticateHandler>();
builder.Services.AddScoped<RefreshSessionHandler>();
builder.Services.AddScoped<LogoutHandler>();
builder.Services.AddScoped<SelectTenantHandler>();
builder.Services.AddScoped<GetSessionOverviewHandler>();
builder.Services.AddScoped<ProvisionPasswordAccountHandler>();
builder.Services.AddScoped<BootstrapTenantAccessHandler>();
builder.Services.AddSingleton(new ModuleCapabilityCatalog(PlatformModules.CapabilityManifests));
builder.Services.AddScoped<EnableTenantModuleHandler>();

// Invitations, password lifecycle, registration, bootstrap and capabilities
builder.Services.AddScoped<AccountTokenService>();
builder.Services.AddScoped<CreateInvitationHandler>();
builder.Services.AddScoped<CancelInvitationHandler>();
builder.Services.AddScoped<ValidateInvitationHandler>();
builder.Services.AddScoped<AcceptInvitationHandler>();
builder.Services.AddScoped<RequestPasswordResetHandler>();
builder.Services.AddScoped<ResetPasswordHandler>();
builder.Services.AddScoped<ChangePasswordHandler>();
builder.Services.AddScoped<RegisterAccountHandler>();
builder.Services.AddScoped<BootstrapTenantAdministratorHandler>();
builder.Services.AddScoped<GetCapabilitiesHandler>();

// E-mail: the app talks to IEmailSender; delivery happens off the request path (see EmailDispatcher).
// Nothing leaves the process unless Email:Smtp:Enabled=true. Development also records every message
// in an in-memory mailbox (GET /dev/mailbox).
var emailOptions = builder.Configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();
if (emailOptions.Smtp.Enabled && string.IsNullOrWhiteSpace(emailOptions.Smtp.Host))
    throw new InvalidOperationException("Email:Smtp:Host is required when Email:Smtp:Enabled is true.");

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<EmailOutbox>();
if (emailOptions.Smtp.Enabled)
    builder.Services.AddSingleton<IEmailTransport, SmtpEmailTransport>();
else
    builder.Services.AddSingleton<IEmailTransport, UndeliveredEmailTransport>();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<DevMailbox>();
    builder.Services.AddSingleton<IInvitationPreviewRecorder, DevInvitationPreviewRecorder>();
}
builder.Services.AddSingleton<IEmailSender, EmailDispatcher>();
builder.Services.AddHostedService<EmailDeliveryService>();
var invitationKeyRingPath = builder.Configuration["Email:InvitationKeyRingPath"];
if (emailOptions.Smtp.Enabled && !builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(invitationKeyRingPath))
    throw new InvalidOperationException("Email:InvitationKeyRingPath is required for durable invitation delivery.");
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("fynovio-platform");
if (!string.IsNullOrWhiteSpace(invitationKeyRingPath))
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(invitationKeyRingPath));
builder.Services.AddSingleton<IInvitationTokenProtector, InvitationTokenProtector>();
builder.Services.AddHostedService<InvitationDeliveryService>();

// Register Host authentication infrastructure
builder.Services.AddScoped<AccessTokenIssuer>();
builder.Services.AddScoped<RefreshCookieWriter>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton(serviceProvider => new IdentifierRateLimiter(
    loginPerMinute: authOptions.RateLimiting.LoginPerIdentifierPerMinute,
    forgotPerHour: authOptions.RateLimiting.ForgotPerIdentifierPerHour,
    timeProvider: serviceProvider.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<ClientFingerprint>();

// Link targets: Collaboration stores links and asks this directory who may see them; the owning modules answer.
// Scoped — the resolvers use the request's CrmDbContext. `masterdata/party` is answered by CRM under L-3 (interim).
builder.Services.AddScoped<ILinkTargetResolver, OpportunityLinkTargetResolver>();
builder.Services.AddScoped<ILinkTargetResolver, PartyLinkTargetResolver>();
builder.Services.AddScoped<ILinkTargetDirectory, LinkTargetDirectory>();

// Register CRM handlers
builder.Services.AddScoped<CreateCalendarEntryHandler>();
builder.Services.AddScoped<GetCalendarEntryHandler>();
builder.Services.AddScoped<ListCalendarEntriesHandler>();
builder.Services.AddScoped<UpdateCalendarEntryHandler>();
builder.Services.AddScoped<DeleteCalendarEntryHandler>();
builder.Services.AddScoped<ProvisionTenantProfileHandler>();
builder.Services.AddScoped<GetCompanySettingsHandler>();
builder.Services.AddScoped<UpdateCompanySettingsHandler>();
builder.Services.AddScoped<GetTenantAccessOverviewHandler>();
builder.Services.AddScoped<GrantRoleAssignmentHandler>();
builder.Services.AddScoped<RevokeRoleAssignmentHandler>();
builder.Services.AddScoped<ManageTenantRoleHandler>();
builder.Services.AddScoped<CreateOpportunityHandler>();
builder.Services.AddScoped<AddOpportunityLineHandler>();
builder.Services.AddScoped<CancelOpportunityLineHandler>();
builder.Services.AddScoped<OpenOpportunityHandler>();
builder.Services.AddScoped<ChangePipelineStageHandler>();
builder.Services.AddScoped<MoveOpportunityToPipelineHandler>();
builder.Services.AddScoped<WinOpportunityHandler>();
builder.Services.AddScoped<LoseOpportunityHandler>();
builder.Services.AddScoped<ReassignOpportunityHandler>();
builder.Services.AddScoped<UpdateOpportunityCustomFieldsHandler>();
builder.Services.AddScoped<SetOpportunityArchiveHandler>();
builder.Services.AddScoped<ListAssignablePrincipalsHandler>();
builder.Services.AddScoped<ProvisionPipelineHandler>(); // operator command `provision-crm-pipeline` and the Development seed
builder.Services.AddScoped<SearchPartyReferencesHandler>();
builder.Services.AddScoped<CreatePartyReferenceHandler>();
builder.Services.AddScoped<GetOpportunityHandler>();
builder.Services.AddScoped<GetOpportunityActivityHandler>();
builder.Services.AddScoped<ListOpportunitiesHandler>();
builder.Services.AddScoped<GetPipelineStagesHandler>();
builder.Services.AddScoped<GetDefaultPipelineStagesHandler>();
builder.Services.AddScoped<GetCrmSettingsHandler>();
builder.Services.AddScoped<EnsureTenantAdministratorActionsHandler>();
builder.Services.AddScoped<UpdateCrmSettingsHandler>();
builder.Services.AddScoped<CreatePipelineDraftHandler>();
builder.Services.AddScoped<PublishPipelineVersionHandler>();
builder.Services.AddScoped<DiscardPipelineDraftHandler>();
builder.Services.AddScoped<ValidatePipelineDraftHandler>();
builder.Services.AddScoped<SetPipelineLifecycleHandler>();
builder.Services.AddScoped<ManageCrmCatalogHandler>();
builder.Services.AddScoped<ManageCustomFieldDefinitionHandler>();
builder.Services.AddScoped<ListCustomFieldDefinitionsHandler>();
builder.Services.AddScoped<GetCustomFieldImpactHandler>();
builder.Services.AddScoped<GetOpportunityAvailableActionsHandler>();

builder.Services.AddExceptionHandler<AccessProblemDetailsExceptionHandler>();
builder.Services.AddExceptionHandler<CrmProblemDetailsExceptionHandler>();
builder.Services.AddExceptionHandler<CollaborationProblemDetailsExceptionHandler>();
builder.Services.AddExceptionHandler<TenantLifecycleProblemDetailsExceptionHandler>();
builder.Services.AddProblemDetails();

// Configure CORS: explicit origins only, no wildcard
// Blank entries are ignored (lets an environment variable empty out a list configured in a
// JSON file); every remaining entry must be a bare origin (scheme://host[:port]) — the value
// is compared verbatim with the browser's Origin header, so a path or trailing slash would
// silently never match.
var corsOrigins = authOptions.AllowedOrigins.Where(origin => !string.IsNullOrWhiteSpace(origin)).ToArray();
foreach (var origin in corsOrigins)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
        || (originUri.Scheme != Uri.UriSchemeHttp && originUri.Scheme != Uri.UriSchemeHttps)
        || origin != $"{originUri.Scheme}://{originUri.Authority}")
    {
        throw new InvalidOperationException(
            $"Authentication:AllowedOrigins entries must be bare http(s) origins (scheme://host[:port]); got '{origin}'.");
    }
}

if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowSpecificOrigins", policy =>
        {
            policy
                .WithOrigins(corsOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });
}

// Configure rate limiting: every policy is partitioned per client IP (a shared, unpartitioned
// limiter would let one client exhaust the budget for everybody).
builder.Services.AddRateLimiter(options =>
{
    static string ClientKey(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    void AddPerClientPolicy(string name, int permitLimit, TimeSpan window) =>
        options.AddPolicy(name, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            $"{name}:{ClientKey(httpContext)}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    AddPerClientPolicy("auth-login", authOptions.RateLimiting.LoginPerMinute, TimeSpan.FromMinutes(1));
    AddPerClientPolicy("auth-refresh", authOptions.RateLimiting.RefreshPerMinute, TimeSpan.FromMinutes(1));
    AddPerClientPolicy("auth-forgot", authOptions.RateLimiting.ForgotPerHour, TimeSpan.FromHours(1));
    AddPerClientPolicy("auth-token", authOptions.RateLimiting.TokenPer15Minutes, TimeSpan.FromMinutes(15));
    AddPerClientPolicy("auth-password", authOptions.RateLimiting.PasswordPer15Minutes, TimeSpan.FromMinutes(15));
    AddPerClientPolicy("auth-public", authOptions.RateLimiting.PublicPerMinute, TimeSpan.FromMinutes(1));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (rejected, cancellationToken) =>
    {
        var retryAfterSeconds = rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? (int)Math.Ceiling(retryAfter.TotalSeconds)
            : 60;
        AuthMetrics.RateLimitRejected.Add(1, new KeyValuePair<string, object?>(
            "policy", rejected.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName ?? "unknown"));
        rejected.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await AuthProblems.RateLimited().ExecuteAsync(rejected.HttpContext);
    };
});

var app = builder.Build();

// Forwarded headers: only when KnownProxies is configured (prevent host-header poisoning)
var knownProxies = builder.Configuration.GetSection("Authentication:KnownProxies").Get<string[]>();
if (knownProxies?.Length > 0)
{
    var forwardedHeadersOptions = new ForwardedHeadersOptions();
    foreach (var proxy in knownProxies)
    {
        if (System.Net.IPAddress.TryParse(proxy, out var ipAddress))
            forwardedHeadersOptions.KnownProxies.Add(ipAddress);
    }
    app.UseForwardedHeaders(forwardedHeadersOptions);
}

// HTTPS hardening in non-Development
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

using (var scope = app.Services.CreateScope())
{
    var accessDb = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
    await AccessActionCatalogSeeder.EnsureSeededAsync(accessDb, PlatformModules.ActionRegistry);
}

// Operator command: runs instead of the web server (never over HTTP), after the action registry is seeded.
if (BootstrapCommand.IsRequested(args))
    return await BootstrapCommand.RunAsync(app.Services, app.Configuration, args, Console.Out, Console.Error);
if (EnableModuleCommand.IsRequested(args))
    return await EnableModuleCommand.RunAsync(app.Services, app.Configuration, args, Console.Out, Console.Error);
if (ProvisionCrmPipelineCommand.IsRequested(args))
    return await ProvisionCrmPipelineCommand.RunAsync(app.Services, app.Configuration, args, Console.Out, Console.Error);
if (BackfillCrmPipelinesCommand.IsRequested(args))
    return await BackfillCrmPipelinesCommand.RunAsync(app.Services, app.Configuration, args, Console.Out, Console.Error);

if (!app.Environment.IsDevelopment() && !emailOptions.Smtp.Enabled)
    app.Logger.LogWarning("Email:Smtp:Enabled is false: invitation and password-reset e-mails will not be delivered.");

// Local-development seed: only when DevSeed:Enabled AND the environment is Development.
await DevSeeder.RunAsync(
    app.Services,
    app.Environment,
    builder.Configuration.GetSection("DevSeed").Get<DevSeedOptions>() ?? new DevSeedOptions(),
    app.Services.GetRequiredService<Access.Application.Authentication.SessionOptions>().PlatformIssuer,
    app.Logger);

// CORS before rate limiting (so preflights aren't counted)
if (corsOrigins.Length > 0)
    app.UseCors("AllowSpecificOrigins");

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ActorContextMiddleware>();

app.UseExceptionHandler();
app.MapAuthEndpoints();
app.MapAccountLifecycleEndpoints(authOptions.SelfRegistration.Enabled);
app.MapOpportunityEndpoints();
app.MapCrmSettingsEndpoints();
app.MapCalendarEndpoints();
app.MapCompanySettingsEndpoints();
if (app.Environment.IsDevelopment())
    app.MapDevEndpoints();

app.MapGet("/", () => "Hello World!");

app.MapGet("/health/db", async (CrmDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok("crm db reachable") : Results.StatusCode(503));

app.Run();
return 0;

public partial class Program { }
