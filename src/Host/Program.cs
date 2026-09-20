using System.Text;
using Access.Application;
using Access.Application.Authentication;
using Access.Persistence;
using Contracts;
using CRM.Application;
using CRM.Persistence;
using Host.Authentication;
using Host.Endpoints;
using MasterData.Application;
using MasterData.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CrmDbContext>(options => options
    .UseNpgsql(
        builder.Configuration.GetConnectionString("Crm") ?? CrmConnectionString.Resolve(),
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CrmDbContext.Schema))
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
builder.Services.AddScoped<IPartyIdentityResolver, PartyIdentityResolver>();

builder.Services.AddScoped<PrincipalResolver>();
builder.Services.AddScoped<IActionCatalog, AccessActionCatalogService>();
builder.Services.AddScoped<IAuthorizer, AccessAuthorizer>();
builder.Services.AddScoped<IAccessScopeResolver, AccessScopeResolver>();

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

builder.Services.AddSingleton(authOptions);
builder.Services.AddSingleton(authOptions.Session);
builder.Services.AddSingleton(authOptions.Password);
builder.Services.AddSingleton(authOptions.Lockout);

// Register authentication application handlers
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<AuthEventWriter>();
builder.Services.AddScoped<SessionValidator>();
builder.Services.AddScoped<AuthenticateHandler>();
builder.Services.AddScoped<RefreshSessionHandler>();
builder.Services.AddScoped<LogoutHandler>();
builder.Services.AddScoped<SelectTenantHandler>();
builder.Services.AddScoped<GetSessionOverviewHandler>();
builder.Services.AddScoped<ProvisionPasswordAccountHandler>();

// Register Host authentication infrastructure
builder.Services.AddScoped<AccessTokenIssuer>();
builder.Services.AddScoped<RefreshCookieWriter>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton(new IdentifierRateLimiter(
    loginPerMinute: authOptions.RateLimiting.LoginPerMinute,
    forgotPerHour: authOptions.RateLimiting.ForgotPerHour));

// Register CRM handlers
builder.Services.AddScoped<CreateOpportunityHandler>();
builder.Services.AddScoped<AddOpportunityLineHandler>();
builder.Services.AddScoped<CancelOpportunityLineHandler>();
builder.Services.AddScoped<OpenOpportunityHandler>();
builder.Services.AddScoped<ChangePipelineStageHandler>();
builder.Services.AddScoped<WinOpportunityHandler>();
builder.Services.AddScoped<LoseOpportunityHandler>();
builder.Services.AddScoped<ReassignOpportunityHandler>();
builder.Services.AddScoped<GetOpportunityHandler>();
builder.Services.AddScoped<ListOpportunitiesHandler>();
builder.Services.AddScoped<GetPipelineStagesHandler>();
builder.Services.AddScoped<GetOpportunityAvailableActionsHandler>();

builder.Services.AddExceptionHandler<CrmProblemDetailsExceptionHandler>();
builder.Services.AddProblemDetails();

// Configure CORS: explicit origins only, no wildcard
if (authOptions.AllowedOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowSpecificOrigins", policy =>
        {
            policy
                .WithOrigins(authOptions.AllowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });
}

// Configure rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth-login", config =>
    {
        config.PermitLimit = authOptions.RateLimiting.LoginPerMinute;
        config.Window = TimeSpan.FromMinutes(1);
    });

    options.AddFixedWindowLimiter("auth-refresh", config =>
    {
        config.PermitLimit = authOptions.RateLimiting.RefreshPerMinute;
        config.Window = TimeSpan.FromMinutes(1);
    });

    options.AddFixedWindowLimiter("auth-forgot", config =>
    {
        config.PermitLimit = authOptions.RateLimiting.ForgotPerHour;
        config.Window = TimeSpan.FromHours(1);
    });

    options.AddFixedWindowLimiter("auth-token", config =>
    {
        config.PermitLimit = authOptions.RateLimiting.TokenPer15Minutes;
        config.Window = TimeSpan.FromMinutes(15);
    });

    options.AddFixedWindowLimiter("auth-password", config =>
    {
        config.PermitLimit = authOptions.RateLimiting.PasswordPer15Minutes;
        config.Window = TimeSpan.FromMinutes(15);
    });

    options.AddFixedWindowLimiter("auth-public", config =>
    {
        config.PermitLimit = authOptions.RateLimiting.PublicPerMinute;
        config.Window = TimeSpan.FromMinutes(1);
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
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
    var manifest = AccessActionCatalog.All
        .Concat(CrmActionCatalog.All.Select(d => new ActionRegistryDescriptor(d.ActionKey, "CRM", d.ResourceType, d.RiskClass)));
    await AccessActionCatalogSeeder.EnsureSeededAsync(accessDb, manifest);
}

// CORS before rate limiting (so preflights aren't counted)
if (authOptions.AllowedOrigins.Length > 0)
    app.UseCors("AllowSpecificOrigins");

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ActorContextMiddleware>();

app.UseExceptionHandler();
app.MapAuthEndpoints();
app.MapOpportunityEndpoints();

app.MapGet("/", () => "Hello World!");

app.MapGet("/health/db", async (CrmDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok("crm db reachable") : Results.StatusCode(503));

app.Run();

public partial class Program { }
