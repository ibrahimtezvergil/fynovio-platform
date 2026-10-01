using CRM.Persistence;
using Messaging;
using Microsoft.EntityFrameworkCore;
using Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<CrmDbContext>(options => options
    .UseNpgsql(
        builder.Configuration.GetConnectionString("Crm") ?? CrmConnectionString.Resolve(),
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CrmDbContext.Schema))
    .UseSnakeCaseNamingConvention());

builder.Services.AddMessagingRuntime(
    builder.Configuration.GetConnectionString("MessagingRelay") ?? MessagingConnectionStrings.ResolveRelay(),
    builder.Configuration.GetConnectionString("MessagingRuntime") ?? MessagingConnectionStrings.ResolveRuntime());

builder.Services.AddHostedService<global::Worker.Worker>();
builder.Services.AddHostedService<MessagingService>();

var host = builder.Build();
host.Run();
