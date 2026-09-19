using System.Text;
using Access.Application;
using Access.Persistence;
using Contracts;
using CRM.Application;
using CRM.Persistence;
using Host.Authentication;
using Host.Endpoints;
using MasterData.Application;
using MasterData.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
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

var jwtOptions = builder.Configuration.GetSection("Authentication:Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Authentication:Jwt configuration section is required.");
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
            NameClaimType = "sub"
        };
    });
builder.Services.AddAuthorization();

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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var accessDb = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
    var manifest = AccessActionCatalog.All
        .Concat(CrmActionCatalog.All.Select(d => new ActionRegistryDescriptor(d.ActionKey, "CRM", d.ResourceType, d.RiskClass)));
    await AccessActionCatalogSeeder.EnsureSeededAsync(accessDb, manifest);
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ActorContextMiddleware>();

app.UseExceptionHandler();
app.MapOpportunityEndpoints();

app.MapGet("/", () => "Hello World!");

app.MapGet("/health/db", async (CrmDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok("crm db reachable") : Results.StatusCode(503));

app.Run();
