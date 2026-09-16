using Contracts;
using CRM.Persistence;
using MasterData.Application;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;

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

builder.Services.AddScoped<IPartyDirectory, PartyDirectory>();
builder.Services.AddScoped<IPartyIdentityResolver, PartyIdentityResolver>();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapGet("/health/db", async (CrmDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok("crm db reachable") : Results.StatusCode(503));

app.Run();
