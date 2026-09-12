using Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<global::Worker.Worker>();

var host = builder.Build();
host.Run();
