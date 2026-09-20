using Access.Application.Authentication;
using Contracts;
using Host.Authentication;
using Host.Email;

namespace Host.Bootstrap;

/// <summary>`dotnet Host.dll bootstrap-tenant-admin --tenant-id N --email E --display-name NAME` — the only
/// production path to a tenant's first administrator. It runs instead of the web server (never over HTTP),
/// refuses unless `Bootstrap:Enabled=true` (env `Bootstrap__Enabled`) and refuses a tenant that is already
/// bootstrapped. No default credential exists: the operator gets a single-use password-setup link, printed
/// once to stdout and never logged, and hands it to the administrator.</summary>
public static class BootstrapCommand
{
    public const string Name = "bootstrap-tenant-admin";

    public const int Success = 0;
    public const int NotPermitted = 2;
    public const int AlreadyBootstrapped = 3;
    public const int BadArguments = 64;

    public static bool IsRequested(string[] args) => args.Length > 0 && args[0] == Name;

    public static async Task<int> RunAsync(
        IServiceProvider services,
        IConfiguration configuration,
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("Bootstrap:Enabled", false))
        {
            await error.WriteLineAsync("Refused: set Bootstrap:Enabled=true (environment variable Bootstrap__Enabled=true) to run this command.");
            return NotPermitted;
        }

        var options = ParseOptions(args.Skip(1).ToArray());
        if (!long.TryParse(options.GetValueOrDefault("tenant-id"), out var tenantId) || tenantId <= 0
            || string.IsNullOrWhiteSpace(options.GetValueOrDefault("email"))
            || string.IsNullOrWhiteSpace(options.GetValueOrDefault("display-name")))
        {
            await error.WriteLineAsync($"Usage: {Name} --tenant-id <positive number> --email <address> --display-name <name>");
            return BadArguments;
        }

        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<BootstrapTenantAdministratorHandler>();
        var result = await handler.HandleAsync(
            new BootstrapTenantAdministratorCommand(new TenantId(tenantId), options["email"], options["display-name"], Guid.NewGuid().ToString()),
            cancellationToken);

        switch (result.Status)
        {
            case BootstrapTenantAdministratorStatus.Completed when result.SetupToken is { } token:
                var baseUrl = scope.ServiceProvider.GetRequiredService<AuthenticationHostOptions>().PublicAppBaseUrl;
                var lifetimeHours = scope.ServiceProvider.GetRequiredService<TokenOptions>().PasswordSetupHours;
                await output.WriteLineAsync($"Tenant {tenantId} bootstrapped. Administrator account: {options["email"]}");
                await output.WriteLineAsync($"One-time password-setup link (shown once, valid {lifetimeHours} h, single use):");
                await output.WriteLineAsync(baseUrl is null
                    ? $"  {EmailRenderer.PasswordResetPath}#token={Uri.EscapeDataString(token)}   (prefix with the application URL)"
                    : $"  {baseUrl.TrimEnd('/')}{EmailRenderer.PasswordResetPath}#token={Uri.EscapeDataString(token)}");
                return Success;

            case BootstrapTenantAdministratorStatus.AlreadyBootstrapped:
                await error.WriteLineAsync($"Refused: tenant {tenantId} is already bootstrapped.");
                return AlreadyBootstrapped;

            default:
                await error.WriteLineAsync("The e-mail address is not valid.");
                return BadArguments;
        }
    }

    /// <summary>`--key value` and `--key=value`.</summary>
    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
                continue;

            var key = args[i][2..];
            var equals = key.IndexOf('=');
            if (equals >= 0)
                parsed[key[..equals]] = key[(equals + 1)..];
            else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                parsed[key] = args[++i];
        }

        return parsed;
    }
}
